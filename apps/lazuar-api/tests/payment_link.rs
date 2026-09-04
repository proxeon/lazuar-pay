//! Port of C# `PaymentLinks/PaymentLinkTests.cs` HTTP occupancy.

mod support;

use rust_decimal::Decimal;
use lazuar_api::publicpay::occupancy;
use support::{
    allow_org, auth_get, auth_post, auth_put, call, member_one, owner_one, put_chip, put_gateway,
    seed_payment_link, start_pay, TestApp,
};

fn create_link(app: &TestApp, body: &str) -> ureq::Response {
    auth_post(app, "/v1/payment-links", body)
}

#[test]
fn create_defaults_to_one_payer() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = create_link(&app, r#"{"org_id":"t1","amount":10,"provider":"test"}"#);
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(doc["max_payers"], 1);
    assert_eq!(doc["unlimited"], false);
    assert_eq!(doc["status"], "open");
    assert_eq!(doc["remaining"], 1);
    assert!(doc["public_token"].as_str().is_some_and(|s| !s.is_empty()));
}

#[test]
fn create_unlimited_has_null_max() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", None);
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "open");
    assert!(doc["max_payers"].is_null(), "{doc}");
    assert!(doc["remaining"].is_null(), "{doc}");
}

#[test]
fn create_max_zero_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = create_link(
        &app,
        r#"{"org_id":"t1","amount":10,"provider":"test","max_payers":0}"#,
    );
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("max_payers"), "{body}");
}

#[test]
fn create_without_bearer_is_401() {
    let app = TestApp::spawn();
    let resp = support::send(
        ureq::post(&format!("{}/v1/payment-links", app.base_url)),
        r#"{"org_id":"t1","amount":10,"provider":"test"}"#,
    );
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn list_returns_newest_first_with_capacity() {
    let app = TestApp::spawn();
    owner_one(&app);
    seed_payment_link(&app, "test", Some(1));
    seed_payment_link(&app, "test", Some(3));
    let resp = auth_get(&app, "/v1/orgs/t1/payment-links");
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let items = doc["items"].as_array().expect("items");
    assert_eq!(items.len(), 2);
    assert_eq!(items[0]["max_payers"], 3);
    assert_eq!(items[0]["remaining"], 3);
    assert_eq!(items[1]["max_payers"], 1);
}

#[test]
fn list_other_org_is_403() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let resp = auth_get(&app, "/v1/orgs/t2/payment-links");
    assert_eq!(resp.status(), 403);
}

#[test]
fn two_people_can_pay_a_link_of_two() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", Some(2));
    let a = start_pay(&app, &token, r#"{"name":"Ada","slot_key":"slot-aaa-1"}"#);
    assert_eq!(a.status(), 200, "{}", a.into_string().unwrap_or_default());
    let b = start_pay(&app, &token, r#"{"name":"Bob","slot_key":"slot-bbb-2"}"#);
    assert_eq!(b.status(), 200, "{}", b.into_string().unwrap_or_default());
    let c = start_pay(&app, &token, r#"{"name":"Cid","slot_key":"slot-ccc-3"}"#);
    assert_eq!(c.status(), 409);
    let body = c.into_string().unwrap_or_default();
    assert!(body.contains("full"), "{body}");

    let paid = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-aaa-1",
        app.base_url
    )));
    let paid_doc: serde_json::Value = paid.into_json().unwrap();
    assert_eq!(paid_doc["status"], "paid");

    let other = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-ccc-3",
        app.base_url
    )));
    let other_doc: serde_json::Value = other.into_json().unwrap();
    assert_eq!(other_doc["status"], "full");
    assert_eq!(other_doc["remaining"], 0);
}

#[test]
fn same_slot_start_twice_does_not_take_two_seats() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_payment_link(&app, "chip", Some(2));
    let body = r#"{"name":"Ada","email":"ada@acme.test","slot_key":"slot-same-1"}"#;
    let first = start_pay(&app, &token, body);
    assert_eq!(first.status(), 200, "{}", first.into_string().unwrap_or_default());
    let second = start_pay(&app, &token, body);
    assert_eq!(second.status(), 200, "{}", second.into_string().unwrap_or_default());
    assert_eq!(app.psp.send_count(), 1);
    let listed = auth_get(&app, "/v1/orgs/t1/payment-links");
    let doc: serde_json::Value = listed.into_json().unwrap();
    assert_eq!(doc["items"][0]["taken_count"], 1);
    assert_eq!(doc["items"][0]["remaining"], 1);
}

#[test]
fn unlimited_accepts_three_payers() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", None);
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Ada","slot_key":"slot-unl-01"}"#).status(),
        200
    );
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Bob","slot_key":"slot-unl-02"}"#).status(),
        200
    );
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Cid","slot_key":"slot-unl-03"}"#).status(),
        200
    );
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "open");
    assert_eq!(doc["paid_count"], 3);
    assert!(doc["remaining"].is_null(), "{doc}");
}

#[test]
fn one_person_link_shows_already_paid_without_slot_after_pay() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", Some(1));
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Ada","slot_key":"slot-only-1"}"#).status(),
        200
    );
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "already_paid");
    assert_eq!(doc["mine"], false);
    assert_eq!(doc["started"], false);
}

#[test]
fn one_person_link_shows_paid_with_payer_slot_after_pay() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", Some(1));
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Ada","slot_key":"slot-only-1"}"#).status(),
        200
    );
    let get = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-only-1",
        app.base_url
    )));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "paid");
    assert_eq!(doc["mine"], true);
}

#[test]
fn start_link_without_slot_key_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", Some(1));
    let resp = start_pay(&app, &token, r#"{"name":"Ada"}"#);
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("slot_key"), "{body}");
}

#[test]
fn public_get_does_not_need_bearer() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", Some(1));
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    assert_eq!(get.status(), 200);
    let after = app.one.send_count();
    assert!(after > 0);
    let again = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    assert_eq!(again.status(), 200);
    assert_eq!(app.one.send_count(), after);
}

#[test]
fn member_cannot_create_payment_link() {
    let app = TestApp::spawn();
    member_one(&app);
    let resp = create_link(&app, r#"{"org_id":"t1","amount":10,"provider":"test"}"#);
    assert_eq!(resp.status(), 403);
}

#[test]
fn admin_can_create_payment_link() {
    let app = TestApp::spawn();
    support::role_one(&app, "admin");
    let resp = create_link(&app, r#"{"org_id":"t1","amount":10,"provider":"test"}"#);
    assert_eq!(resp.status(), 201, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn suspended_writer_cannot_create_payment_link() {
    let app = TestApp::spawn();
    app.one.respond_with(|req| {
        if req.url.contains("/me") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"suspended"}]}"#.into(),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":true}"#.into(),
            }
        }
    });
    let resp = create_link(&app, r#"{"org_id":"t1","amount":10,"provider":"test"}"#);
    assert_eq!(resp.status(), 403);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.to_lowercase().contains("suspend"), "{body}");
}

#[test]
fn child_public_token_loads_parent_occupancy() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, link_id) = seed_payment_link(&app, "chip", Some(2));
    let started = start_pay(
        &app,
        &token,
        r#"{"name":"Ada","email":"ada@acme.test","slot_key":"slot-alias-1"}"#,
    );
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let mut db = app.db();
    let child: String = db
        .query_one(
            "SELECT \"PublicToken\" FROM public.checkouts WHERE \"PaymentLinkId\" = $1",
            &[&link_id],
        )
        .unwrap()
        .get(0);
    drop(db);
    let get = call(ureq::get(&format!("{}/v1/pay/{child}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["remaining"], 1);
    assert_eq!(doc["max_payers"], 2);
    assert_eq!(doc["taken_count"], 1);
}

#[test]
fn pause_expires_open_reservations() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_payment_link(&app, "chip", Some(1));
    let started = start_pay(
        &app,
        &token,
        r#"{"name":"Ada","email":"ada@acme.test","slot_key":"slot-pause-1"}"#,
    );
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let mut db = app.db();
    db.execute(
        "UPDATE public.org_settings SET \"ChargesPaused\" = TRUE WHERE \"OrgId\" = 't1'",
        &[],
    )
    .unwrap();
    drop(db);
    let get = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-other-2",
        app.base_url
    )));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "open");
    assert_eq!(doc["remaining"], 1);
    assert_eq!(doc["taken_count"], 0);
}

#[test]
fn two_chip_starts_hold_open_seats_on_a_link_of_two() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_payment_link(&app, "chip", Some(2));
    assert_eq!(
        start_pay(
            &app,
            &token,
            r#"{"name":"Ada","email":"ada@acme.test","slot_key":"slot-open-a"}"#
        )
        .status(),
        200
    );
    assert_eq!(
        start_pay(
            &app,
            &token,
            r#"{"name":"Bob","email":"bob@acme.test","slot_key":"slot-open-b"}"#
        )
        .status(),
        200
    );
    assert_eq!(
        start_pay(
            &app,
            &token,
            r#"{"name":"Cid","email":"cid@acme.test","slot_key":"slot-open-c"}"#
        )
        .status(),
        409
    );
    let get = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-open-c",
        app.base_url
    )));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "full");
    assert_eq!(doc["paid_count"], 0);
    assert_eq!(doc["taken_count"], 2);
}

#[test]
fn start_rate_limit_is_429() {
    let app = TestApp::spawn_with(|mut c| {
        c.start_max_per_minute = 2;
        c
    });
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", None);
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Ada","slot_key":"slot-lim-01"}"#).status(),
        200
    );
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Bob","slot_key":"slot-lim-02"}"#).status(),
        200
    );
    let third = start_pay(&app, &token, r#"{"name":"Cid","slot_key":"slot-lim-03"}"#);
    assert_eq!(third.status(), 429);
}

#[test]
fn chip_start_without_email_does_not_occupy_the_only_seat() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_payment_link(&app, "chip", Some(1));
    let missing = start_pay(&app, &token, r#"{"name":"Ada","slot_key":"slot-ghost-1"}"#);
    assert_eq!(missing.status(), 400);
    let body = missing.into_string().unwrap_or_default();
    assert!(body.contains("email is required"), "{body}");
    assert_eq!(app.psp.send_count(), 0);
    let other = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-other-2",
        app.base_url
    )));
    let open_doc: serde_json::Value = other.into_json().unwrap();
    assert_eq!(open_doc["status"], "open");
    assert_eq!(open_doc["remaining"], 1);
    let ok = start_pay(
        &app,
        &token,
        r#"{"name":"Ada","email":"ada@acme.test","slot_key":"slot-other-2"}"#,
    );
    assert_eq!(ok.status(), 200, "{}", ok.into_string().unwrap_or_default());
    assert_eq!(app.psp.send_count(), 1);
}

#[test]
fn billplz_localhost_callback_400_frees_the_seat() {
    let app = TestApp::spawn_with(|mut c| {
        c.public_base_url = "http://localhost:8081".into();
        c
    });
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_payment_link(&app, "billplz", Some(1));
    let first = start_pay(
        &app,
        &token,
        r#"{"name":"Ada","email":"ada@acme.test","slot_key":"slot-bp-fail"}"#,
    );
    assert_eq!(first.status(), 400);
    let body = first.into_string().unwrap_or_default();
    assert!(body.contains("callback base"), "{body}");
    let other = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-bp-next",
        app.base_url
    )));
    let doc: serde_json::Value = other.into_json().unwrap();
    assert_eq!(doc["status"], "open");
    assert_eq!(doc["remaining"], 1);
    assert_eq!(doc["taken_count"], 0);
}

#[test]
fn concurrent_start_on_one_person_link_admits_one_psp() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| {
        std::thread::sleep(std::time::Duration::from_millis(120));
        lazuar_api::transport::OutResponse {
            status: 200,
            body: r#"{"id":"purch_race","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
        }
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_payment_link(&app, "chip", Some(1));
    let base = app.base_url.clone();
    let email = r#"{"name":"Ada","email":"ada@acme.test","slot_key":"SLOT"}"#;
    let (first, second) = std::thread::scope(|scope| {
        let a = scope.spawn(|| {
            support::start_pay_at(
                &base,
                &token,
                &email.replace("SLOT", "slot-race-a1"),
            )
        });
        let b = scope.spawn(|| {
            support::start_pay_at(
                &base,
                &token,
                &email.replace("SLOT", "slot-race-b2"),
            )
        });
        (a.join().unwrap().status(), b.join().unwrap().status())
    });
    let codes = [first, second];
    assert!(codes.contains(&200), "expected one 200, got {codes:?}");
    assert!(codes.contains(&409), "expected one 409, got {codes:?}");
    assert_eq!(app.psp.send_count(), 1);
    let mut db = app.pool.get().expect("pool");
    let docs: i64 = db.query_one("SELECT count(*) FROM public.documents", &[]).unwrap().get(0);
    assert_eq!(docs, 0);
    let open: i64 = db
        .query_one("SELECT count(*) FROM public.checkouts WHERE \"Status\" = 'open'", &[])
        .unwrap()
        .get(0);
    assert_eq!(open, 1);
}

#[test]
fn concurrent_test_start_on_one_person_link_mints_one_receipt() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", Some(1));
    let base = app.base_url.clone();
    let (first, second) = std::thread::scope(|scope| {
        let a = scope.spawn(|| {
            support::start_pay_at(&base, &token, r#"{"name":"Ada","slot_key":"slot-t-race-a"}"#)
        });
        let b = scope.spawn(|| {
            support::start_pay_at(&base, &token, r#"{"name":"Ada","slot_key":"slot-t-race-b"}"#)
        });
        (a.join().unwrap().status(), b.join().unwrap().status())
    });
    let codes = [first, second];
    assert!(codes.contains(&200), "expected one 200, got {codes:?}");
    assert!(codes.contains(&409), "expected one 409, got {codes:?}");
    let mut db = app.pool.get().expect("pool");
    let docs: i64 = db
        .query_one(
            "SELECT count(*) FROM public.documents WHERE \"Title\" = 'Official Receipt'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(docs, 1);
    let paid: i64 = db
        .query_one("SELECT count(*) FROM public.checkouts WHERE \"Status\" = 'paid'", &[])
        .unwrap()
        .get(0);
    assert_eq!(paid, 1);
}

#[test]
fn abandoned_open_reservation_expires_and_second_slot_can_start() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_old","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, link_id) = seed_payment_link(&app, "chip", Some(1));
    let start = start_pay(
        &app,
        &token,
        r#"{"name":"Ada","email":"ada@acme.test","slot_key":"slot-stale-1"}"#,
    );
    assert_eq!(start.status(), 200, "{}", start.into_string().unwrap_or_default());

    let mut db = app.pool.get().expect("pool");
    let expired_id: String = db
        .query_one(
            "SELECT \"Id\" FROM public.checkouts WHERE \"PaymentLinkId\" = $1 AND \"SlotKey\" = $2",
            &[&link_id, &"slot-stale-1"],
        )
        .unwrap()
        .get(0);
    db.execute(
        "UPDATE public.checkouts SET \"CreatedAt\" = $1 WHERE \"Id\" = $2",
        &[&(chrono::Utc::now() - chrono::Duration::minutes(31)), &expired_id],
    )
    .unwrap();
    drop(db);

    let stale_get = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-stale-1",
        app.base_url
    )));
    let stale_doc: serde_json::Value = stale_get.into_json().unwrap();
    assert_eq!(stale_doc["status"], "expired");

    let next = start_pay(
        &app,
        &token,
        r#"{"name":"Bob","email":"bob@acme.test","slot_key":"slot-fresh-2"}"#,
    );
    assert_eq!(next.status(), 200, "{}", next.into_string().unwrap_or_default());

    let mut db = app.pool.get().expect("pool");
    let mut tx = db.transaction().unwrap();
    let gates = lazuar_api::money::fulfillment::CheckoutGates::default();
    lazuar_api::money::fulfillment::fulfill_paid(&mut tx, &gates, &expired_id, "chip", Some("purch_old"))
        .unwrap();
    tx.commit().unwrap();
    let docs: i64 = db.query_one("SELECT count(*) FROM public.documents", &[]).unwrap().get(0);
    assert_eq!(docs, 0);
}

#[test]
fn second_fulfill_on_max_one_link_does_not_mint_a_second_receipt() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, link_id) = seed_payment_link(&app, "test", Some(1));
    let first_id = uuid::Uuid::new_v4().to_string();
    let extra_id = uuid::Uuid::new_v4().to_string();
    let mut db = app.pool.get().expect("pool");
    for (id, slot) in [(&first_id, "slot-over-a"), (&extra_id, "slot-over-b")] {
        db.execute(
            "INSERT INTO public.checkouts \
             (\"Id\",\"OrgId\",\"Provider\",\"PaymentLinkId\",\"SlotKey\",\"PublicToken\",\
             \"Amount\",\"Currency\",\"Status\",\"Interval\",\"CreatedAt\") \
             VALUES ($1,'t1','test',$2,$3,$4,$5,'MYR','open','one_off',$6)",
            &[
                id,
                &link_id,
                &slot,
                &format!("tok_{slot}"),
                &Decimal::from(10),
                &chrono::Utc::now(),
            ],
        )
        .unwrap();
    }
    let gates = lazuar_api::money::fulfillment::CheckoutGates::default();
    {
        let mut tx = db.transaction().unwrap();
        lazuar_api::money::fulfillment::fulfill_paid(&mut tx, &gates, &first_id, "test", Some("ref-a"))
            .unwrap();
        tx.commit().unwrap();
    }
    {
        let mut tx = db.transaction().unwrap();
        lazuar_api::money::fulfillment::fulfill_paid(&mut tx, &gates, &extra_id, "test", Some("ref-b"))
            .unwrap();
        tx.commit().unwrap();
    }
    let docs: i64 = db
        .query_one(
            "SELECT count(*) FROM public.documents WHERE \"Title\" = 'Official Receipt'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(docs, 1);
    let charges: i64 = db.query_one("SELECT count(*) FROM public.charges", &[]).unwrap().get(0);
    assert_eq!(charges, 1);
    let extra_status: String = db
        .query_one("SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1", &[&extra_id])
        .unwrap()
        .get(0);
    assert_eq!(extra_status, "expired");
}

#[test]
fn list_over_admit_is_over_capacity_not_silent_full() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, link_id) = seed_payment_link(&app, "test", Some(1));
    let mut db = app.db();
    for slot in ["slot-over-a", "slot-over-b"] {
        db.execute(
            "INSERT INTO public.checkouts \
             (\"Id\",\"OrgId\",\"Provider\",\"PaymentLinkId\",\"SlotKey\",\"PublicToken\",\
             \"Amount\",\"Currency\",\"Status\",\"Interval\",\"CreatedAt\") \
             VALUES ($1,'t1','test',$2,$3,$4,$5,'MYR','paid','one_off',$6)",
            &[
                &uuid::Uuid::new_v4().simple().to_string(),
                &link_id,
                &slot,
                &format!("tok_{slot}"),
                &Decimal::from(10),
                &chrono::Utc::now(),
            ],
        )
        .unwrap();
    }
    drop(db);
    let listed = auth_get(&app, "/v1/orgs/t1/payment-links");
    let doc: serde_json::Value = listed.into_json().unwrap();
    assert_eq!(doc["items"][0]["taken_count"], 2);
    assert_eq!(doc["items"][0]["max_payers"], 1);
    assert_eq!(doc["items"][0]["remaining"], -1);
    assert_eq!(doc["items"][0]["status"], "over_capacity");
}

fn seed_race_link(app: &TestApp, link_id: &str, token: &str, max_payers: Option<i32>) {
    let mut db = app.pool.get().expect("pool");
    db.execute(
        "INSERT INTO public.payment_links \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"Amount\",\"Currency\",\"MaxPayers\",\"CreatedAt\") \
         VALUES ($1,'t1',$2,'test',$3,'MYR',$4,$5)",
        &[
            &link_id,
            &token,
            &Decimal::from(10),
            &max_payers,
            &chrono::Utc::now(),
        ],
    )
    .expect("seed payment link");
}

fn seed_race_child(
    app: &TestApp,
    id: &str,
    link_id: &str,
    slot: &str,
    created_at: chrono::DateTime<chrono::Utc>,
) {
    let mut db = app.pool.get().expect("pool");
    db.execute(
        "INSERT INTO public.checkouts \
         (\"Id\",\"OrgId\",\"PublicToken\",\"PaymentLinkId\",\"SlotKey\",\"Amount\",\"Currency\",\
         \"Status\",\"Provider\",\"Interval\",\"CreatedAt\") \
         VALUES ($1,'t1',$2,$3,$4,$5,'MYR','open','test','one_off',$6)",
        &[
            &id,
            &format!("tok_{id}"),
            &link_id,
            &slot,
            &Decimal::from(10),
            &created_at,
        ],
    )
    .expect("seed child checkout");
}

#[test]
fn sweep_cannot_overwrite_a_committed_paid_checkout() {
    let app = TestApp::spawn();
    owner_one(&app);
    let checkout_id = uuid::Uuid::new_v4().to_string();
    seed_race_link(&app, "lk_002", "tok_lk_002", Some(1));
    seed_race_child(
        &app,
        &checkout_id,
        "lk_002",
        "slot-002-aaaa",
        chrono::Utc::now() - chrono::Duration::minutes(31),
    );

    let paid = support::test_webhook_paid(&app, "evt_002_pay", &checkout_id);
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());

    let mut db = app.pool.get().expect("pool");
    let mut tx = db.transaction().unwrap();
    let expired = occupancy::expire_stale(
        &mut tx,
        "lk_002",
        occupancy::reservation_ttl(Some(30)),
    )
    .unwrap();
    assert_eq!(expired.len(), 0, "a paid checkout must not be expired by the sweep");

    let marked = occupancy::mark_expired(&mut tx, vec![checkout_id.clone()], "ttl").unwrap();
    assert_eq!(
        marked.len(),
        0,
        "the CAS write must refuse a row that left 'open'"
    );
    tx.commit().unwrap();

    let status: String = db
        .query_one(
            "SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap()
        .get(0);
    assert_eq!(status, "paid");
    let charges: i64 = db
        .query_one(
            "SELECT count(*) FROM public.charges WHERE \"CheckoutId\" = $1",
            &[&checkout_id],
        )
        .unwrap()
        .get(0);
    assert_eq!(charges, 1);
    let expired_hooks: i64 = db
        .query_one(
            "SELECT count(*) FROM public.org_webhook_deliveries WHERE \"EventType\" = 'checkout.expired'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(
        expired_hooks, 0,
        "no expiry webhook may fire for a fulfilled checkout"
    );
}

#[test]
fn concurrent_late_captures_on_a_full_link_admit_one_and_refund_the_loser() {
    let app = TestApp::spawn();
    owner_one(&app);
    let a_id = uuid::Uuid::new_v4().to_string();
    let b_id = uuid::Uuid::new_v4().to_string();
    seed_race_link(&app, "lk_008", "tok_lk_008", Some(1));
    let stale = chrono::Utc::now() - chrono::Duration::minutes(31);
    seed_race_child(&app, &a_id, "lk_008", "slot-008-aaaa", stale);
    seed_race_child(&app, &b_id, "lk_008", "slot-008-bbbb", stale);

    let base = app.base_url.clone();
    let secret = app.config.test_webhook_secret.clone();
    let (status_a, status_b) = std::thread::scope(|scope| {
        let a = {
            let base = base.clone();
            let secret = secret.clone();
            let id = a_id.clone();
            scope.spawn(move || support::test_webhook_paid_at(&base, &secret, "evt_008_a", &id).status())
        };
        let b = {
            let base = base.clone();
            let secret = secret.clone();
            let id = b_id.clone();
            scope.spawn(move || support::test_webhook_paid_at(&base, &secret, "evt_008_b", &id).status())
        };
        (a.join().unwrap(), b.join().unwrap())
    });
    assert_eq!(status_a, 200, "webhook a");
    assert_eq!(status_b, 200, "webhook b");

    let mut db = app.pool.get().expect("pool");
    let rows = db
        .query(
            "SELECT \"Id\",\"Status\" FROM public.checkouts WHERE \"PaymentLinkId\" = 'lk_008'",
            &[],
        )
        .unwrap();
    let paid = rows
        .iter()
        .filter(|r| r.get::<_, String>("Status") == "paid")
        .count();
    let expired = rows
        .iter()
        .filter(|r| r.get::<_, String>("Status") == "expired")
        .count();
    assert_eq!(paid, 1, "only one payer may be admitted");
    assert_eq!(expired, 1, "the loser is expired, not silently dropped");
    let charges: i64 = db
        .query_one(
            "SELECT count(*) FROM public.charges WHERE \"CheckoutId\" = $1 OR \"CheckoutId\" = $2",
            &[&a_id, &b_id],
        )
        .unwrap()
        .get(0);
    assert_eq!(charges, 1, "exactly one charge for the admitted payer");
    let late_status: String = db
        .query_one(
            "SELECT \"Status\" FROM public.refunds WHERE \"Reason\" = 'late_pay'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(late_status, "succeeded", "the test rail settles the late refund");
}

#[test]
fn sweep_expires_stale_open_rows_and_notifies() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = auth_put(
        &app,
        "/v1/orgs/t1/webhooks",
        r#"{"url":"http://127.0.0.1:9/hook"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());

    let checkout_id = uuid::Uuid::new_v4().to_string();
    seed_race_link(&app, "lk_stale", "tok_stale", Some(1));
    seed_race_child(
        &app,
        &checkout_id,
        "lk_stale",
        "slot-stale-aaaa",
        chrono::Utc::now() - chrono::Duration::minutes(31),
    );

    let mut db = app.pool.get().expect("pool");
    let mut tx = db.transaction().unwrap();
    let expired = occupancy::expire_stale(
        &mut tx,
        "lk_stale",
        occupancy::reservation_ttl(Some(30)),
    )
    .unwrap();
    assert_eq!(expired.len(), 1);
    tx.commit().unwrap();

    let status: String = db
        .query_one(
            "SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap()
        .get(0);
    assert_eq!(status, "expired");
    let hooks: i64 = db
        .query_one(
            "SELECT count(*) FROM public.org_webhook_deliveries WHERE \"EventType\" = 'checkout.expired'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(hooks, 1);
}
