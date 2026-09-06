mod support;

use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;

use serde_json::Value;
use sqlx::PgPool;
use support::pool;
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::TcpListener;
use uuid::Uuid;
use workers::hmac::sign_v1;
use workers::outbound::{process_batch, OutboundCfg};
use workers::secret_box::SecretBox;

async fn listen() -> (u16, Arc<AtomicUsize>, Arc<tokio::sync::Mutex<Vec<u8>>>) {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let port = listener.local_addr().unwrap().port();
    let hits = Arc::new(AtomicUsize::new(0));
    let body = Arc::new(tokio::sync::Mutex::new(Vec::new()));
    let hits2 = hits.clone();
    let body2 = body.clone();
    tokio::spawn(async move {
        loop {
            let Ok((mut s, _)) = listener.accept().await else {
                break;
            };
            let mut acc = Vec::new();
            let mut buf = [0u8; 4096];
            if let Ok(n) = s.read(&mut buf).await {
                if n > 0 {
                    acc.extend_from_slice(&buf[..n]);
                }
            }
            loop {
                match tokio::time::timeout(std::time::Duration::from_millis(50), s.read(&mut buf))
                    .await
                {
                    Ok(Ok(0)) | Err(_) | Ok(Err(_)) => break,
                    Ok(Ok(n)) => acc.extend_from_slice(&buf[..n]),
                }
            }
            hits2.fetch_add(1, Ordering::SeqCst);
            *body2.lock().await = acc;
            let _ = s
                .write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok")
                .await;
        }
    });
    (port, hits, body)
}

async fn insert_endpoint(pool: &PgPool, tenant: &str, url: &str, ct: &[u8]) {
    sqlx::query(
        "INSERT INTO pay_rs.org_webhook_endpoints (tenant_id, url, secret_ciphertext)
         VALUES ($1, $2, $3)
         ON CONFLICT (tenant_id) DO UPDATE SET url = EXCLUDED.url, secret_ciphertext = EXCLUDED.secret_ciphertext",
    )
    .bind(tenant)
    .bind(url)
    .bind(ct)
    .execute(pool)
    .await
    .unwrap();
}

async fn insert_delivery(pool: &PgPool, tenant: &str, event_id: &str, payload: &str) {
    sqlx::query(
        r#"
        INSERT INTO pay_rs.org_webhook_deliveries (
            tenant_id, event_id, event_type, payload_json, status, next_attempt_at
        ) VALUES ($1, $2, 'payment.completed', $3::jsonb, 'pending', now())
        "#,
    )
    .bind(tenant)
    .bind(event_id)
    .bind(payload)
    .execute(pool)
    .await
    .unwrap();
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn two_replicas_send_once_and_hmac_verifies() {
    let pool = pool().await;
    let box_ = SecretBox::new(SecretBox::testing_fallback_key());
    let secret = "whsec_test";
    let ct = box_.protect_str(secret).unwrap();
    let (port, hits, raw) = listen().await;
    let t1 = format!("t-{}", Uuid::new_v4().simple());
    insert_endpoint(&pool, &t1, &format!("http://127.0.0.1:{port}/hook"), &ct).await;
    let payload = r#"{"id":"e1","type":"payment.completed","org_id":"t1","api_version":"0.1.0","data":{"checkout_id":"aa","amount":10.00,"currency":"MYR","provider":"test"}}"#;
    insert_delivery(
        &pool,
        &t1,
        &format!("e-{}", Uuid::new_v4().simple()),
        payload,
    )
    .await;

    let cfg = || OutboundCfg {
        pool: &pool,
        box_,
        allow_loopback: true,
    };
    let (ra, rb) = tokio::join!(process_batch(cfg()), process_batch(cfg()));
    ra.expect("dispatch a");
    rb.expect("dispatch b");
    tokio::time::sleep(std::time::Duration::from_millis(200)).await;
    assert_eq!(hits.load(Ordering::SeqCst), 1);

    let captured = raw.lock().await.clone();
    let text = String::from_utf8_lossy(&captured);
    let lower = text.to_ascii_lowercase();
    assert!(
        lower.contains("x-lazuar-signature:"),
        "captured request: {text:?}"
    );
    let sig_line = text
        .lines()
        .find(|l| l.to_ascii_lowercase().starts_with("x-lazuar-signature:"))
        .unwrap();
    let ts_line = text
        .lines()
        .find(|l| l.to_ascii_lowercase().starts_with("x-lazuar-timestamp:"))
        .unwrap();
    let v1 = sig_line.split("v1=").nth(1).unwrap().trim();
    let unix: i64 = ts_line.split(':').nth(1).unwrap().trim().parse().unwrap();
    let body = text.split("\r\n\r\n").nth(1).unwrap_or("");
    let json_start = body.find('{').expect("json body");
    let json_body = body[json_start..].trim();
    assert_eq!(sign_v1(secret, json_body.as_bytes(), unix), v1);
    let v: Value = serde_json::from_str(json_body).unwrap();
    assert!(v["data"]["amount"].is_number());
    assert_ne!(v["type"], "settled");
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn poison_decrypt_does_not_starve_next_row() {
    let pool = pool().await;
    let box_ = SecretBox::new(SecretBox::testing_fallback_key());
    let ct_ok = box_.protect_str("whsec_ok").unwrap();
    let (port, hits, _) = listen().await;
    let poison_t = format!("tp-{}", Uuid::new_v4().simple());
    let ok_t = format!("to-{}", Uuid::new_v4().simple());
    insert_endpoint(
        &pool,
        &poison_t,
        &format!("http://127.0.0.1:{port}/p"),
        b"!!!not-a-wrapped-secret!!!",
    )
    .await;
    insert_endpoint(&pool, &ok_t, &format!("http://127.0.0.1:{port}/ok"), &ct_ok).await;
    insert_delivery(&pool, &poison_t, "evt_poison", "{}").await;
    insert_delivery(&pool, &ok_t, "evt_healthy", "{}").await;

    process_batch(OutboundCfg {
        pool: &pool,
        box_,
        allow_loopback: true,
    })
    .await
    .unwrap();
    tokio::time::sleep(std::time::Duration::from_millis(200)).await;
    assert!(hits.load(Ordering::SeqCst) >= 1);

    let st_p: String = sqlx::query_scalar(
        "SELECT status FROM pay_rs.org_webhook_deliveries WHERE tenant_id = $1 AND event_id = 'evt_poison'",
    )
    .bind(&poison_t)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(st_p, "pending");
    let err: Option<String> = sqlx::query_scalar(
        "SELECT last_error FROM pay_rs.org_webhook_deliveries WHERE tenant_id = $1 AND event_id = 'evt_poison'",
    )
    .bind(&poison_t)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert!(err.unwrap_or_default().starts_with("dispatch:"));

    let st_ok: String = sqlx::query_scalar(
        "SELECT status FROM pay_rs.org_webhook_deliveries WHERE tenant_id = $1 AND event_id = 'evt_healthy'",
    )
    .bind(&ok_t)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(st_ok, "succeeded");
}
