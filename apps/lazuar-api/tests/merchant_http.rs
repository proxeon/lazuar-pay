mod support;

use support::TestApp;

fn owner_one(app: &TestApp) {
    app.one.respond_with(|req| {
        if req.url.contains("/me") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"user_id":"u1","email":"ada@acme.test","name":"Ada","tenants":[{"id":"t1","role":"owner","status":"active"}]}"#.into(),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":true}"#.into(),
            }
        }
    });
}

#[test]
fn checkout_mint_and_get_round_trip() {
    let app = TestApp::spawn();
    owner_one(&app);
    let url = format!("{}/v1/checkouts", app.base_url);
    let resp = ureq::post(&url)
        .set("Authorization", "Bearer jwt")
        .send_string(r#"{"org_id":"t1","provider":"test","amount":9.90,"currency":"MYR"}"#);
    let resp = match resp {
        Ok(r) => r,
        Err(ureq::Error::Status(code, r)) => panic!("status {code} {}", r.into_string().unwrap_or_default()),
        Err(e) => panic!("{e}"),
    };
    assert_eq!(resp.status(), 201);
    let body: serde_json::Value = resp.into_json().unwrap();
    assert_eq!(body["org_id"], "t1");
    assert!(body["amount"].is_number(), "decimal JSON must be a number: {body}");
    let id = body["id"].as_str().unwrap();
    let get = ureq::get(&format!("{}/v1/checkouts/{id}", app.base_url))
        .set("Authorization", "Bearer jwt")
        .call()
        .unwrap();
    assert_eq!(get.status(), 200);
}

#[test]
fn catalog_rejects_non_myr_with_exact_string() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = ureq::post(&format!("{}/v1/orgs/t1/products", app.base_url))
        .set("Authorization", "Bearer jwt")
        .send_string(r#"{"name":"Cut","amount":10,"currency":"USD"}"#);
    match resp {
        Err(ureq::Error::Status(400, r)) => {
            let body: serde_json::Value = r.into_json().unwrap();
            assert_eq!(body["detail"], "Bar B currency is MYR");
        }
        other => panic!("expected 400, got {other:?}"),
    }
}
