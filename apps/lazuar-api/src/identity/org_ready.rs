//! Port of `Identity/OrgReadyEndpoints.cs`.

use postgres::Client;
use serde_json::{json, Value};

use crate::rails::providers;

pub fn is_ready(charges_paused: bool, has_vault: bool, allows_test: bool) -> bool {
    !charges_paused && (has_vault || allows_test)
}

pub fn handle(conn: &mut Client, org_id: &str, environment: &str) -> Result<Value, postgres::Error> {
    let paused = conn
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .map(|row| row.get::<_, bool>(0))
        .unwrap_or(false);
    let has_vault = conn
        .query_opt(
            "SELECT 1 FROM public.gateway_credentials WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .is_some();
    let ready = is_ready(paused, has_vault, providers::allows_test(environment));
    Ok(json!({ "org_id": org_id, "ready": ready }))
}
