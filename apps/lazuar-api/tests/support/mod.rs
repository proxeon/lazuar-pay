//! Test support — the Rust analogue of `PayApiFactory` + `FakePspHandler`.
#![allow(dead_code)]
//!
//! Differences from C#, both deliberate:
//! - D008: there is no InMemory provider. Every test runs against a real,
//!   uniquely-named Postgres database (created from
//!   `migrations/0001_reference_schema.sql`, dropped on drop). The C# suite's
//!   weakest point was 96%-InMemory; the port does not inherit it.
//! - The server is a real rouille instance on a reserved loopback port, so
//!   tests make real HTTP calls end to end.

use std::sync::{Arc, Mutex};

use lazuar_api::app::{self, State};
use lazuar_api::config::Config;
use lazuar_api::transport::{OutRequest, OutResponse, Transport};

// ---------------------------------------------------------------------------
// FakeTransport — FakePspHandler / FakeOneHandler semantics
// ---------------------------------------------------------------------------

pub fn secretbox_testing() -> lazuar_api::secrets::SecretBox {
    lazuar_api::secrets::SecretBox::from_env_testing(None).unwrap()
}

pub fn b64_decode(s: &str) -> Vec<u8> {
    use base64::Engine as _;
    base64::engine::general_purpose::STANDARD.decode(s).unwrap()
}

#[derive(Debug, Clone)]
pub struct RecordedRequest {
    pub method: String,
    pub url: String,
    pub headers: Vec<(String, String)>,
    pub body: Option<String>,
}

type Responder = Box<dyn Fn(&RecordedRequest) -> OutResponse + Send + Sync>;

#[derive(Default)]
struct Recordings {
    count: usize,
    last: Option<RecordedRequest>,
    all: Vec<RecordedRequest>,
}

/// Records every send; delegates to the installed responder; defaults to 404
/// exactly like `FakePspHandler` with no `Responder` set.
pub struct FakeTransport {
    name: &'static str,
    recordings: Mutex<Recordings>,
    responder: Mutex<Option<Responder>>,
}

impl FakeTransport {
    pub fn new(name: &'static str) -> Self {
        Self { name, recordings: Mutex::new(Recordings::default()), responder: Mutex::new(None) }
    }

    /// Install the canned behavior. Replaces any previous responder.
    pub fn respond_with(&self, f: impl Fn(&RecordedRequest) -> OutResponse + Send + Sync + 'static) {
        *self.responder.lock().unwrap() = Some(Box::new(f));
    }

    pub fn send_count(&self) -> usize {
        self.recordings.lock().unwrap().count
    }

    pub fn last(&self) -> Option<RecordedRequest> {
        self.recordings.lock().unwrap().last.clone()
    }

    pub fn all(&self) -> Vec<RecordedRequest> {
        self.recordings.lock().unwrap().all.clone()
    }
}

impl Transport for FakeTransport {
    fn send(&self, request: OutRequest) -> Result<OutResponse, TransportError> {
        {
            let mut r = self.recordings.lock().unwrap();
            r.count += 1;
            let rec = RecordedRequest {
                method: request.method.clone(),
                url: request.url.clone(),
                headers: request.headers.clone(),
                body: request.body.clone(),
            };
            r.last = Some(rec.clone());
            r.all.push(rec);
        }
        let responder = self.responder.lock().unwrap();
        match responder.as_ref() {
            Some(f) => {
                let recorded = RecordedRequest {
                    method: request.method,
                    url: request.url,
                    headers: request.headers,
                    body: request.body,
                };
                Ok(f(&recorded))
            }
            None => Ok(OutResponse { status: 404, body: "{}".to_string() }),
        }
    }
}

use lazuar_api::transport::TransportError;

// ---------------------------------------------------------------------------
// TestApp — PayApiFactory semantics
// ---------------------------------------------------------------------------

pub struct TestApp {
    pub base_url: String,
    pub db_name: String,
    pub config: Config,
    pub psp: Arc<FakeTransport>,
    pub one: Arc<FakeTransport>,
    pub pool: app::PgPool,
    admin: postgres::Client,
}

fn admin_connection_string() -> String {
    std::env::var("PAY_TEST_POSTGRES")
        .unwrap_or_else(|_| "host=localhost port=5435 user=postgres password=postgres dbname=postgres".to_string())
}

const REFERENCE_SCHEMA: &str = include_str!("../../migrations/0001_reference_schema.sql");

impl TestApp {
    /// Boot a full app instance: unique database, reference schema, real HTTP
    /// server on a reserved loopback port, fake transports wired in.
    pub fn spawn() -> Self {
        Self::spawn_with(|config| config)
    }

    /// Same, with config overrides applied before assembly.
    pub fn spawn_with(override_config: impl FnOnce(Config) -> Config) -> Self {
        let mut admin = postgres::Client::connect(&admin_connection_string(), postgres::NoTls)
            .expect("connect to admin postgres (set PAY_TEST_POSTGRES to override)");

        let db_name = format!("paytest_{}", uuid::Uuid::new_v4().simple());
        admin
            .batch_execute(&format!("CREATE DATABASE \"{db_name}\""))
            .expect("create per-test database");

        let mut test_db_config = admin_connection_string()
            .parse::<postgres::Config>()
            .expect("parse base cs");
        test_db_config.dbname(&db_name);
        let manager = r2d2_postgres::PostgresConnectionManager::new(
            test_db_config.clone(),
            lazuar_api::db::tls_connector(),
        );
        let pool = r2d2::Pool::builder()
            // Stay well under local Postgres `max_connections` (100) when cargo
            // runs several TestApp binaries in parallel. Concurrent HTTP tests
            // need two checkouts plus one assertion connection.
            .max_size(4)
            .build(manager)
            .expect("build test pool");

        {
            let mut conn = pool.get().expect("test db connection");
            conn.batch_execute(REFERENCE_SCHEMA)
                .expect("apply reference schema");
        }

        let config = override_config(Config::from_env());

        let psp = Arc::new(FakeTransport::new("psp"));
        let one = Arc::new(FakeTransport::new("one"));

        let state = Arc::new(State {
            config: config.clone(),
            started_at: chrono::Utc::now(),
            pool: Some(pool.clone()),
            psp: psp.clone(),
            one: one.clone(),
            one_client: lazuar_api::identity::one_client::OneClient {
                base_url: config.one_base_url.clone(),
                timeout_secs: 2,
            },
            secret_box: lazuar_api::secrets::SecretBox::from_env_testing(None)
                .expect("dev secret box"),
            fulfill_gates: Default::default(),
            start_gates: Default::default(),
            link_gates: Default::default(),
            limiter: Default::default(),
            whoami_cache: Arc::new(lazuar_api::identity::whoami_cache::OneWhoamiCache::new()),
        });

        let port = reserve_port();
        let addr = format!("127.0.0.1:{port}");
        {
            let state = state.clone();
            let addr = addr.clone();
            std::thread::spawn(move || app::serve(addr, state));
        }
        wait_healthy(&format!("http://{addr}/health"));

        Self { base_url: format!("http://{addr}"), db_name, config, psp, one, pool, admin }
    }

    /// A dedicated connection to the per-test database, for assertions.
    pub fn db(&self) -> postgres::Client {
        self.test_db_config()
            .connect(postgres::NoTls)
            .expect("connect to test db")
    }

    pub fn test_db_config(&self) -> postgres::Config {
        let mut config = admin_connection_string()
            .parse::<postgres::Config>()
            .expect("parse base cs");
        config.dbname(&self.db_name);
        config
    }
}

impl Drop for TestApp {
    fn drop(&mut self) {
        let _ = self
            .admin
            .batch_execute(&format!("DROP DATABASE \"{}\" WITH (FORCE)", self.db_name));
    }
}

/// Insert a minimal checkout row with a given status, for transition tests.
pub fn insert_checkout_org(
    db: &mut postgres::Client,
    id: uuid::Uuid,
    org_id: &str,
    status: &str,
) {
    db.execute(
        "INSERT INTO public.checkouts \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\"CreatedAt\",\"Provider\") \
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)",
        &[
            &id.to_string(),
            &org_id,
            &format!("tok_{id}"),
            &rust_decimal::Decimal::new(990, 2),
            &"MYR",
            &status,
            &"mo",
            &chrono::Utc::now(),
            &"test",
        ],
    )
    .expect("insert checkout");
}

/// Insert a checkout under the default test org.
pub fn insert_checkout(db: &mut postgres::Client, id: uuid::Uuid, status: &str) {
    insert_checkout_org(db, id, "org_test", status);
}

/// Read back a checkout's status.
pub fn checkout_status(db: &mut postgres::Client, id: uuid::Uuid) -> String {
    db.query_one("SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1", &[&id.to_string()])
        .expect("checkout exists")
        .get(0)
}

/// Insert a charge row for refund tests. Status "succeeded" = money took.
pub fn insert_charge(
    db: &mut postgres::Client,
    charge_id: &str,
    org_id: &str,
    checkout_id: &str,
    amount: rust_decimal::Decimal,
    currency: &str,
    status: &str,
    provider: &str,
    provider_ref: Option<&str>,
) {
    db.execute(
        "INSERT INTO public.charges \
         (\"Id\",\"OrgId\",\"CheckoutId\",\"Provider\",\"ProviderRef\",\"Amount\",\"Currency\",\"Status\") \
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8)",
        &[
            &charge_id,
            &org_id,
            &checkout_id,
            &provider,
            &provider_ref,
            &amount,
            &currency,
            &status,
        ],
    )
    .expect("insert charge");
}

/// A checkout + its succeeded charge, ready for refund tests.
pub fn insert_charged_checkout(db: &mut postgres::Client, org: &str, amount: rust_decimal::Decimal) -> String {
    let checkout_id = uuid::Uuid::new_v4().to_string();
    insert_checkout_org(db, uuid::Uuid::parse_str(&checkout_id).unwrap(), org, "paid");
    let charge_id = format!("ch_{}", uuid::Uuid::new_v4().simple());
    insert_charge(
        db,
        &charge_id,
        org,
        &checkout_id,
        amount,
        "MYR",
        "succeeded",
        "test",
        Some(&format!("{}_ref", charge_id)),
    );
    checkout_id
}

fn reserve_port() -> u16 {
    std::net::TcpListener::bind("127.0.0.1:0")
        .expect("reserve loopback port")
        .local_addr()
        .expect("reserved port addr")
        .port()
}

fn wait_healthy(url: &str) {
    for _ in 0..100 {
        if ureq::get(url).call().is_ok() {
            return;
        }
        std::thread::sleep(std::time::Duration::from_millis(20));
    }
    panic!("server at {url} never became healthy");
}

/// Owner/admin One responder used by C# `PayTest.Owner`.
pub fn owner_one(app: &TestApp) {
    role_one(app, "owner");
}

/// Member One responder — writer routes 403, member routes 200.
pub fn member_one(app: &TestApp) {
    role_one(app, "member");
}

pub fn role_one(app: &TestApp, role: &str) {
    let role = role.to_string();
    app.one.respond_with(move |req| {
        if req.url.contains("/me") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: format!(
                    r#"{{"user_id":"u1","email":"ada@acme.test","name":"Ada","is_platform_admin":false,"tenants":[{{"id":"t1","role":"{role}","status":"active"}}]}}"#
                ),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":true}"#.into(),
            }
        }
    });
}

/// C# `CheckoutTests.Allow(orgId)` — membership only for that org.
pub fn allow_org(app: &TestApp, org_id: &str) {
    allow_org_role(app, org_id, "owner");
}

pub fn allow_org_role(app: &TestApp, org_id: &str, role: &str) {
    let org = org_id.to_string();
    let role = role.to_string();
    app.one.respond_with(move |req| {
        if req.url.contains("/me") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: format!(
                    r#"{{"user_id":"u1","email":"ada@acme.test","name":"Ada","is_platform_admin":false,"tenants":[{{"id":"{org}","role":"{role}","status":"active"}}]}}"#
                ),
            }
        } else if req.url.contains(&format!("/tenants/{org}/authz/check")) {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":true}"#.into(),
            }
        } else if req.url.contains("/authz/check") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":false}"#.into(),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 404,
                body: "{}".into(),
            }
        }
    });
}

/// C# `PayTest.Key` — machine-key /me with a bound active tenant.
pub fn machine_one(app: &TestApp) {
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"user_id":"key-1","is_platform_admin":false,"tenants":[{"id":"t1","role":"member","status":"active"}]}"#.into(),
    });
}

pub const MACHINE_KEY: &str = "lzr_sk_testfixture";

/// CHIP vault PUT needs a ≥2048-bit RSA SubjectPublicKeyInfo PEM.
pub fn chip_pem() -> String {
    use rsa::pkcs8::EncodePublicKey;
    use rsa::{RsaPrivateKey, RsaPublicKey};
    static PEM: std::sync::OnceLock<String> = std::sync::OnceLock::new();
    PEM.get_or_init(|| {
        let mut rng = rand::thread_rng();
        let key = RsaPrivateKey::new(&mut rng, 2048).expect("rsa 2048");
        RsaPublicKey::from(&key)
            .to_public_key_pem(rsa::pkcs8::LineEnding::LF)
            .expect("chip pem")
    })
    .clone()
}

pub fn call(req: ureq::Request) -> ureq::Response {
    match req.call() {
        Ok(r) => r,
        Err(ureq::Error::Status(_, r)) => r,
        Err(e) => panic!("{e}"),
    }
}

pub fn send(req: ureq::Request, body: &str) -> ureq::Response {
    match req.send_string(body) {
        Ok(r) => r,
        Err(ureq::Error::Status(_, r)) => r,
        Err(e) => panic!("{e}"),
    }
}

pub fn auth_get(app: &TestApp, path: &str) -> ureq::Response {
    call(ureq::get(&format!("{}{path}", app.base_url)).set("Authorization", "Bearer jwt"))
}

pub fn auth_put(app: &TestApp, path: &str, body: &str) -> ureq::Response {
    send(
        ureq::put(&format!("{}{path}", app.base_url)).set("Authorization", "Bearer jwt"),
        body,
    )
}

pub fn auth_post(app: &TestApp, path: &str, body: &str) -> ureq::Response {
    send(
        ureq::post(&format!("{}{path}", app.base_url)).set("Authorization", "Bearer jwt"),
        body,
    )
}

pub fn put_gateway(app: &TestApp, body: &str) -> ureq::Response {
    auth_put(app, "/v1/orgs/t1/gateway", body)
}

pub fn put_chip(app: &TestApp) -> ureq::Response {
    let pem = chip_pem();
    let body = serde_json::json!({
        "provider": "chip",
        "secret": "chip_sk",
        "webhook_secret": pem,
        "public_merchant_id": "brand_1",
    });
    put_gateway(app, &body.to_string())
}

/// C# `PayTest.SeedCheckout` — mint a checkout, return `(public_token, id)`.
pub fn seed_checkout(app: &TestApp, provider: &str, currency: Option<&str>) -> (String, String) {
    let mut body = serde_json::json!({
        "org_id": "t1",
        "amount": 10,
        "provider": provider,
    });
    if let Some(currency) = currency {
        body["currency"] = serde_json::Value::String(currency.to_string());
    }
    let resp = auth_post(app, "/v1/checkouts", &body.to_string());
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let v: serde_json::Value = serde_json::from_str(&raw).unwrap();
    (
        v["public_token"].as_str().unwrap().to_string(),
        v["id"].as_str().unwrap().to_string(),
    )
}

/// C# `PayTest.SeedPaymentLink`.
pub fn seed_payment_link(app: &TestApp, provider: &str, max_payers: Option<i32>) -> (String, String) {
    let mut body = serde_json::json!({
        "org_id": "t1",
        "amount": 10,
        "provider": provider,
        "unlimited": max_payers.is_none(),
    });
    if let Some(max) = max_payers {
        body["max_payers"] = serde_json::json!(max);
    }
    let resp = auth_post(app, "/v1/payment-links", &body.to_string());
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let v: serde_json::Value = serde_json::from_str(&raw).unwrap();
    (
        v["public_token"].as_str().unwrap().to_string(),
        v["id"].as_str().unwrap().to_string(),
    )
}

pub fn start_pay(app: &TestApp, token: &str, body: &str) -> ureq::Response {
    start_pay_at(&app.base_url, token, body)
}

/// Same as [`start_pay`] against an already-known base URL — for concurrent threads.
pub fn start_pay_at(base_url: &str, token: &str, body: &str) -> ureq::Response {
    send(
        ureq::post(&format!("{base_url}/v1/pay/{token}/start")).set("Authorization", "Bearer jwt"),
        body,
    )
}

/// C# `OccupancyRaceTests.TestWebhookPaid` — signed test-rail paid webhook.
pub fn test_webhook_paid(app: &TestApp, event_id: &str, checkout_id: &str) -> ureq::Response {
    test_webhook_paid_at(&app.base_url, &app.config.test_webhook_secret, event_id, checkout_id)
}

pub fn test_webhook_paid_at(
    base_url: &str,
    secret: &str,
    event_id: &str,
    checkout_id: &str,
) -> ureq::Response {
    let body = format!(
        r#"{{"id":"{event_id}","checkout_id":"{checkout_id}","amount_total":1000,"currency":"myr"}}"#
    );
    let mac = lazuar_api::rails::test_webhook::test_hmac_hex(secret, &body);
    send(
        ureq::post(&format!("{base_url}/v1/webhooks/test/t1")).set("X-Pay-Test-Signature", &mac),
        &body,
    )
}
