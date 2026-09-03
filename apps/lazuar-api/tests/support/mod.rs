//! Test support — the Rust analogue of `PayApiFactory` + `FakePspHandler`.
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

#[derive(Debug, Clone)]
pub struct RecordedRequest {
    pub method: String,
    pub url: String,
    pub body: Option<String>,
}

type Responder = Box<dyn Fn(&RecordedRequest) -> OutResponse + Send + Sync>;

#[derive(Default)]
struct Recordings {
    count: usize,
    last: Option<RecordedRequest>,
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
}

impl Transport for FakeTransport {
    fn send(&self, request: OutRequest) -> Result<OutResponse, TransportError> {
        {
            let mut r = self.recordings.lock().unwrap();
            r.count += 1;
            r.last = Some(RecordedRequest {
                method: request.method.clone(),
                url: request.url.clone(),
                body: request.body.clone(),
            });
        }
        let responder = self.responder.lock().unwrap();
        match responder.as_ref() {
            Some(f) => {
                let recorded = RecordedRequest {
                    method: request.method,
                    url: request.url,
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
    pool: app::PgPool,
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
            postgres::NoTls,
        );
        let pool = r2d2::Pool::builder()
            .max_size(8)
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
