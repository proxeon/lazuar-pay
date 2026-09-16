use sqlx::postgres::PgPoolOptions;
use sqlx::PgPool;
use std::sync::OnceLock;
use testcontainers::runners::SyncRunner;
use testcontainers::{Container, ImageExt};
use testcontainers_modules::postgres::Postgres as PgImage;

struct Pg {
    _container: Container<PgImage>,
    url: String,
}

static PG: OnceLock<Pg> = OnceLock::new();

fn db_url() -> &'static str {
    &PG.get_or_init(|| {
        std::thread::spawn(|| {
            let container = PgImage::default()
                .with_tag("16-alpine")
                .start()
                .expect("Pay api tests require Docker/Testcontainers Postgres 16");
            let port = container.get_host_port_ipv4(5432).expect("postgres port");
            Pg {
                _container: container,
                url: format!(
                    "postgres://postgres:postgres@127.0.0.1:{port}/postgres?sslmode=disable"
                ),
            }
        })
        .join()
        .expect("postgres container thread")
    })
    .url
}

pub async fn pool() -> PgPool {
    let pool = PgPoolOptions::new()
        .max_connections(8)
        .connect(db_url())
        .await
        .expect("connect postgres");
    storage::migrate(&pool).await.expect("migrate pay_rs");
    pool
}

/// C# `{series}-{MalaysiaTime.Year}-{n:00000}` — not `RCPT-TEST-{uuid}`.
#[allow(dead_code)]
pub fn assert_issued_number(series: &str, number: &str) {
    let prefix = format!("{series}-");
    assert!(
        number.starts_with(&prefix),
        "expected {prefix}year-nnnnn, got {number}"
    );
    let rest = number.strip_prefix(&prefix).expect(number);
    let (year, n) = rest.split_once('-').unwrap_or_else(|| panic!("{number}"));
    assert_eq!(year.len(), 4, "{number}");
    assert!(year.chars().all(|c| c.is_ascii_digit()), "{number}");
    assert_eq!(n.len(), 5, "{number}");
    assert!(n.chars().all(|c| c.is_ascii_digit()), "{number}");
    assert_ne!(n, "00000", "{number}");
}

#[allow(dead_code)]
pub fn sign(secret: &str, body: &str) -> String {
    use hmac::{Hmac, Mac};
    use sha2::Sha256;
    let mut mac = Hmac::<Sha256>::new_from_slice(secret.as_bytes()).unwrap();
    mac.update(body.as_bytes());
    hex::encode(mac.finalize().into_bytes())
}
