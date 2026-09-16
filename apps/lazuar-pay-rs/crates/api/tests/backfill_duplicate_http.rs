mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Value};
use support::pool;
use tokio::sync::Mutex;
use tower::ServiceExt;
use uuid::Uuid;

const SECRET: &str = "test-secret";
const WHSEC: &str = "whsec_test";
static BACKFILL_HTTP: Mutex<()> = Mutex::const_new(());

async fn call(app: axum::Router, req: Request<Body>) -> (StatusCode, Value) {
    let res = app.oneshot(req).await.unwrap();
    let status = res.status();
    let bytes = res.into_body().collect().await.unwrap().to_bytes();
    let json: Value = if bytes.is_empty() {
        json!(null)
    } else {
        serde_json::from_slice(&bytes).unwrap_or(json!(null))
    };
    (status, json)
}

fn fixture(name: &str) -> String {
    std::fs::read_to_string(format!(
        "{}/../rails/tests/fixtures/stripe/{name}",
        env!("CARGO_MANIFEST_DIR")
    ))
    .unwrap()
}

#[tokio::test]
async fn stripe_on_backfilled_paid_does_not_second_charge() {
    let _g = BACKFILL_HTTP.lock().await;
    let pool = pool().await;
    storage::backfill::install_public_ddl(&pool)
        .await
        .expect("ddl");
    sqlx::query(
        r#"
        TRUNCATE public.checkouts, public.charges, public.refunds,
                 public.journal_entries, public.journal_lines, public.documents,
                 public.org_settings, public.gateway_credentials, public.products,
                 public.prices, public.payment_links, public.document_sequences,
                 public.org_webhook_endpoints
        "#,
    )
    .execute(&pool)
    .await
    .unwrap();

    let id = Uuid::new_v4().simple().to_string();
    let charge_id = Uuid::new_v4().simple().to_string();
    let token = format!("tok-{}", Uuid::new_v4().simple());
    sqlx::query(
        r#"
        INSERT INTO public.checkouts (
            "Id", "OrgId", "PublicToken", "Amount", "Currency", "Status",
            "Provider", "ProviderSessionId", "CreatedAt"
        ) VALUES ($1, 't1', $2, 10.00, 'MYR', 'paid', 'stripe', 'cs_test_1', now() - interval '1 day')
        "#,
    )
    .bind(&id)
    .bind(&token)
    .execute(&pool)
    .await
    .unwrap();
    sqlx::query(
        r#"
        INSERT INTO public.charges (
            "Id", "OrgId", "CheckoutId", "Provider", "ProviderRef",
            "Amount", "Currency", "Status"
        ) VALUES ($1, 't1', $2, 'stripe', 'cs_test_1', 10.00, 'MYR', 'paid')
        "#,
    )
    .bind(&charge_id)
    .bind(&id)
    .execute(&pool)
    .await
    .unwrap();

    storage::backfill::run(&pool, storage::BackfillOpts::apply())
        .await
        .unwrap();

    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        Request::builder()
            .method("PUT")
            .uri("/v1/orgs/t1/gateway")
            .header("Authorization", "Bearer test-writer")
            .header("Content-Type", "application/json")
            .body(Body::from(
                json!({
                    "provider": "stripe",
                    "secret": "sk_test_dummy",
                    "webhook_secret": WHSEC
                })
                .to_string(),
            ))
            .unwrap(),
    )
    .await;
    assert!(st.is_success(), "{st}");

    let body = fixture("webhook_paid.json").replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", &id);
    let now = time::OffsetDateTime::now_utc().unix_timestamp();
    let sig = rails::stripe::sign(WHSEC, body.as_bytes(), now);
    let (st, hook) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/stripe/t1")
            .header("Stripe-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(body))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{hook}");
    assert!(hook.get("ignored").is_none(), "{hook}");
    let n: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(Uuid::try_parse(&id).unwrap())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(n, 1, "S1 Keep must not insert a second charge");
}
