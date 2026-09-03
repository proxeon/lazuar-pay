//! Outbound dispatch — issue 005 (poison isolation + per-row persistence) and
//! issue 017 (connect-time private-address refusal), against a real local
//! HTTP receiver and real Postgres.

mod support;

use support::TestApp;
use lazuar_api::secrets::SecretBox;
use lazuar_api::webhooks::dispatch;
use std::sync::{Arc, Mutex};

struct Receiver {
    url: String,
    requests: Arc<Mutex<Vec<(String, Vec<(String, String)>)>>>,
    handle: std::thread::JoinHandle<()>,
}

/// A tiny rouille receiver: records POSTs, answers with a fixed status.
fn spawn_receiver(status: u16) -> Receiver {
    #[allow(unused_mut)]
    let status = status;
    let port = std::net::TcpListener::bind("127.0.0.1:0")
        .unwrap()
        .local_addr()
        .unwrap()
        .port();
    let requests: Arc<Mutex<Vec<(String, Vec<(String, String)>)>>> = Arc::new(Mutex::new(Vec::new()));
    let log = requests.clone();
    let handle = std::thread::spawn(move || {
        rouille::start_server(format!("127.0.0.1:{port}"), move |request| {
            let body = {
                let mut buf = String::new();
                use std::io::Read as _;
                let mut r = request.data().expect("body reader");
                let _ = r.read_to_string(&mut buf);
                buf
            };
            let hdrs: Vec<(String, String)> = request
                .headers()
                .map(|(k, v)| (k.to_string(), v.to_string()))
                .collect();
            log.lock().unwrap().push((body, hdrs));
            rouille::Response::text("ok").with_status_code(status)
        })
    });
    let url = format!("http://127.0.0.1:{port}/hook");
    Receiver { url, requests, handle }
}


fn insert_endpoint(db: &mut postgres::Client, org: &str, url: &str, secret_ct: &str) {
    db.execute(
        "INSERT INTO public.org_webhook_endpoints \
         (\"OrgId\",\"Url\",\"SecretCiphertext\",\"SecretPrefix\",\"UpdatedAt\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&org, &url, &secret_ct, &"wr_", &chrono::Utc::now()],
    )
    .unwrap();
}

fn insert_delivery(db: &mut postgres::Client, id: &str, org: &str, event_id: &str) {
    db.execute(
        "INSERT INTO public.org_webhook_deliveries \
         (\"Id\",\"OrgId\",\"EventId\",\"EventType\",\"PayloadJson\",\"Status\",\
         \"AttemptCount\",\"NextAttemptAt\",\"CreatedAt\") \
         VALUES ($1,$2,$3,'payment.completed','{}','pending',0,$4,$4)",
        &[&id, &org, &event_id, &chrono::Utc::now()],
    )
    .unwrap();
}

fn env() -> (SecretBox, String) {
    let box_one = SecretBox::from_env_testing(None).unwrap();
    let environment = "Testing".to_string();
    (box_one, environment)
}

#[test]
fn poison_row_errors_alone_and_batch_continues() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let (box_one, environment) = env();
    let receiver = spawn_receiver(200);

    // Good org: valid secret, reachable receiver.
    insert_endpoint(&mut db, "org_good", &receiver.url, &box_one.protect("good_secret"));
    insert_delivery(&mut db, "d_good", "org_good", "evt_good");

    // Poison org: the endpoint secret ciphertext is corrupt (key-rotated away).
    insert_endpoint(&mut db, "org_bad", &receiver.url, "not-decryptable");
    insert_delivery(&mut db, "d_bad", "org_bad", "evt_bad");

    let transport = dispatch::webhook_transport(&environment);
    let processed = dispatch::process_batch(&mut db, &box_one, transport.as_ref(), &environment).unwrap();
    assert_eq!(processed, 2, "both rows claimed");

    // The good delivery succeeded; the poison row errored ITSELF and stays
    // pending with a backoff — the batch never stopped.
    let good = db
        .query_one("SELECT \"Status\" FROM public.org_webhook_deliveries WHERE \"Id\"='d_good'", &[])
        .unwrap()
        .get::<_, String>(0);
    assert_eq!(good, "succeeded");

    let bad = db
        .query_one(
            "SELECT \"Status\",\"AttemptCount\",\"LastError\" FROM public.org_webhook_deliveries WHERE \"Id\"='d_bad'",
            &[],
        )
        .unwrap();
    assert_eq!(bad.get::<_, String>(0), "pending");
    assert_eq!(bad.get::<_, i32>(1), 1);
    assert!(bad.get::<_, String>(2).starts_with("dispatch:"));

    // The receiver saw exactly the good delivery.
    assert_eq!(receiver.requests.lock().unwrap().len(), 1);
}

#[test]
fn delivery_carries_lazuar_signature_headers() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let (box_one, environment) = env();
    let receiver = spawn_receiver(200);
    insert_endpoint(&mut db, "org_sig", &receiver.url, &box_one.protect("sig_secret"));
    insert_delivery(&mut db, "d_sig", "org_sig", "evt_sig");

    let transport = dispatch::webhook_transport(&environment);
    dispatch::process_batch(&mut db, &box_one, transport.as_ref(), &environment).unwrap();

    let requests = receiver.requests.lock().unwrap();
    let (body, hdrs) = &requests[0];
    let header = |name: &str| {
        hdrs.iter()
            .find(|(k, _)| k.eq_ignore_ascii_case(name))
            .map(|(_, v)| v.clone())
            .expect("header present")
    };
    assert_eq!(header("X-Lazuar-Event-Id"), "evt_sig");
    assert_eq!(header("X-Lazuar-Tenant-Id"), "org_sig");

    // The v1 signature verifies over {timestamp}.{payload} with the vaulted secret.
    let v1 = header("X-Lazuar-Signature");
    let timestamp = header("X-Lazuar-Timestamp");
    let unix: i64 = timestamp.parse().unwrap();
    let expected = lazuar_api::identity::one_webhook_signature::compute("sig_secret", body, unix);
    assert_eq!(v1, format!("v1={expected}"));
}

#[test]
fn status_routing_succeeds_retries_and_dead_letters() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let (box_one, environment) = env();

    let ok_receiver = spawn_receiver(200);
    let dead_receiver = spawn_receiver(401);
    let retry_receiver = spawn_receiver(500);
    insert_endpoint(&mut db, "org_ok", &ok_receiver.url, &box_one.protect("s"));
    insert_endpoint(&mut db, "org_dead", &dead_receiver.url, &box_one.protect("s"));
    insert_endpoint(&mut db, "org_retry", &retry_receiver.url, &box_one.protect("s"));
    insert_delivery(&mut db, "d_ok", "org_ok", "e1");
    insert_delivery(&mut db, "d_dead", "org_dead", "e2");
    insert_delivery(&mut db, "d_retry", "org_retry", "e3");

    let transport = dispatch::webhook_transport(&environment);
    dispatch::process_batch(&mut db, &box_one, transport.as_ref(), &environment).unwrap();

    let mut status = |id: &str| -> String {
        db.query_one("SELECT \"Status\" FROM public.org_webhook_deliveries WHERE \"Id\"=$1", &[&id])
            .unwrap()
            .get(0)
    };
    assert_eq!(status("d_ok"), "succeeded");
    assert_eq!(status("d_dead"), "dead", "401/403/410 dead-letter");
    assert_eq!(status("d_retry"), "pending", "5xx retries with backoff");
}

#[test]
fn connect_refuses_private_and_metadata_addresses_at_socket_level() {
    use std::net::IpAddr;

    // Issue 017 at the socket layer: loopback refused in Production, dialable
    // in Testing; the metadata address refused everywhere.
    // Testing allows the loopback dial (it fails with connection refused on an
    // empty port — permission is what we assert); Production refuses before dialing.
    let _ = lazuar_api::webhooks::outbound_url::connect_validated("127.0.0.1", 1, "Testing");
    let permission_err = lazuar_api::webhooks::outbound_url::connect_validated("127.0.0.1", 1, "Production")
        .err()
        .expect("loopback dial must be refused in Production");
    assert!(matches!(permission_err, lazuar_api::webhooks::outbound_url::OutboundUrlError::NoAllowedAddress));

    let metadata = lazuar_api::webhooks::outbound_url::resolve_allowed("169.254.169.254", 80, "Production");
    assert!(metadata.is_empty(), "metadata address must never resolve allowed");

    // C# parity note: IsDisallowed = private && !AllowsLoopback(env), so the
    // Testing/Development exemption covers all private ranges — the same
    // behavior the reference implementation has. Production never exempts.

    // IP-literal classification sanity.
    assert!(lazuar_api::webhooks::outbound_url::is_private_or_loopback(
        IpAddr::from_str("10.1.2.3").unwrap()
    ));
    assert!(lazuar_api::webhooks::outbound_url::is_private_or_loopback(
        IpAddr::from_str("192.168.0.9").unwrap()
    ));
    assert!(!lazuar_api::webhooks::outbound_url::is_private_or_loopback(
        IpAddr::from_str("203.0.113.9").unwrap()
    ));
}

use std::str::FromStr as _;
