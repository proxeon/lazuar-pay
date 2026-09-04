//! Postgres connect with TLS when the DSN asks for it (Npgsql-style).
//! `sslmode=disable` stays cleartext; Prefer/Require use native-tls.

use postgres::config::SslMode;
use postgres::{Client, Config, NoTls};
use postgres_native_tls::MakeTlsConnector;
use r2d2_postgres::PostgresConnectionManager;

pub type PgPool = r2d2::Pool<PostgresConnectionManager<MakeTlsConnector>>;

pub fn tls_connector() -> MakeTlsConnector {
    let connector = native_tls::TlsConnector::builder()
        .build()
        .expect("native-tls connector");
    MakeTlsConnector::new(connector)
}

/// One-shot client (workers). Honors `sslmode` on the connection string.
pub fn connect(conn_string: &str) -> Result<Client, postgres::Error> {
    let config: Config = conn_string.parse()?;
    match config.get_ssl_mode() {
        SslMode::Disable => config.connect(NoTls),
        _ => config.connect(tls_connector()),
    }
}

pub fn pool(conn_string: &str) -> Result<PgPool, String> {
    let config: Config = conn_string
        .parse()
        .map_err(|e: postgres::Error| e.to_string())?;
    let manager = PostgresConnectionManager::new(config, tls_connector());
    r2d2::Pool::builder()
        .build(manager)
        .map_err(|e| e.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sslmode_disable_and_require_parse() {
        let disable: Config = "host=localhost sslmode=disable user=u".parse().unwrap();
        assert_eq!(disable.get_ssl_mode(), SslMode::Disable);
        let require: Config = "host=db.example sslmode=require user=u".parse().unwrap();
        assert_eq!(require.get_ssl_mode(), SslMode::Require);
    }
}
