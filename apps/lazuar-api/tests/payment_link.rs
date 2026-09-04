//! Port of C# `PaymentLinks/PaymentLinkTests.cs` HTTP occupancy.

mod support;

use rust_decimal::Decimal;
use support::{
    allow_org, auth_get, auth_post, call, member_one, owner_one, put_chip, put_gateway,
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
