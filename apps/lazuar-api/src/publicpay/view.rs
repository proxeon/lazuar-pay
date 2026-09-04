//! Port of `PublicPayEndpoints.Get` / `GetLink` / `CheckoutView`.

use postgres::Client;
use serde_json::{json, Value};

use crate::domain::checkout_store;
use crate::publicpay::buyer_email;
use crate::publicpay::occupancy;
use crate::rails::providers;

fn checkout_page_url(base: &str, token: &str) -> String {
    format!("{base}/c/{token}")
}

pub fn checkout_view(
    token: &str,
    session: &checkout_store::CheckoutSession,
    checkout_base: &str,
    slot_key: Option<&str>,
    solana_cluster: &str,
) -> Value {
    let psp = session.pay_url.clone().unwrap_or_default();
    let on_page = providers::is_on_page_url(Some(&psp));
    let mine = match (slot_key, session.slot_key.as_deref()) {
        (Some(a), Some(b)) => a == b,
        _ => slot_key.is_none(),
    };
    json!({
        "token": token,
        "amount": crate::hosting::decimal_json(session.amount),
        "currency": session.currency,
        "status": session.status,
        "email_required": session.provider.as_deref().is_some_and(providers::requires_email)
            && session.payer_email.as_deref().is_none_or(|e| !buyer_email::is_usable(Some(e))),
        "started": session.pay_url.as_deref().is_some_and(|u| !u.is_empty()),
        "mine": mine,
        "provider": session.provider,
        "redirect_url": if on_page { Value::Null } else { json!(session.pay_url) },
        "solana_pay_url": if on_page { json!(psp) } else { Value::Null },
        "solana_cluster": if session.provider.as_deref().is_some_and(providers::is_solana) {
            json!(solana_cluster)
        } else {
            Value::Null
        },
        "payer_name": session.payer_name,
        "payer_email": session.payer_email,
        "pay_url": checkout_page_url(checkout_base, token),
    })
}

pub fn get(
    conn: &mut Client,
    checkout_base: &str,
    solana_cluster: &str,
    ttl_minutes: i64,
    token: &str,
    slot_key: Option<&str>,
) -> Result<Result<Value, crate::hosting::PayError>, postgres::Error> {
    if let Some(link) = conn.query_opt(
        "SELECT \"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"Amount\",\"Currency\",\"MaxPayers\",\"ProductId\" \
         FROM public.payment_links WHERE \"PublicToken\" = $1",
        &[&token],
    )? {
        return Ok(Ok(get_link(conn, checkout_base, solana_cluster, ttl_minutes, &link, slot_key)?));
    }

    let Some(session) = checkout_store::get_by_public_token(conn, token)? else {
        return Ok(Err(crate::hosting::PayError::not_found("Checkout not found")));
    };
    if let Some(parent_id) = session.payment_link_id.as_deref().filter(|s| !s.is_empty()) {
        if let Some(link) = conn.query_opt(
            "SELECT \"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"Amount\",\"Currency\",\"MaxPayers\",\"ProductId\" \
             FROM public.payment_links WHERE \"Id\" = $1",
            &[&parent_id],
        )? {
            let slot = session.slot_key.as_deref().or(slot_key);
            return Ok(Ok(get_link(conn, checkout_base, solana_cluster, ttl_minutes, &link, slot)?));
        }
    }
    Ok(Ok(checkout_view(token, &session, checkout_base, slot_key, solana_cluster)))
}

fn get_link(
    conn: &mut Client,
    checkout_base: &str,
    _solana_cluster: &str,
    ttl_minutes: i64,
    link: &postgres::Row,
    slot_key: Option<&str>,
) -> Result<Value, postgres::Error> {
    let link_id: String = link.get("Id");
    let org_id: String = link.get("OrgId");
    let token: String = link.get("PublicToken");
    let provider: String = link.get("Provider");
    let amount: rust_decimal::Decimal = link.get("Amount");
    let currency: String = link.get("Currency");
    let max_payers: Option<i32> = link.get("MaxPayers");

    let paused = conn
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .map(|row| row.get::<_, bool>(0))
        .unwrap_or(false);

    {
        let mut tx = conn.transaction()?;
        occupancy::lock_parent(&mut tx, &link_id)?;
        let reason = if paused { "charges_paused" } else { "ttl" };
        if paused {
            let open: Vec<String> = tx
                .query(
                    "SELECT \"Id\" FROM public.checkouts WHERE \"PaymentLinkId\" = $1 AND \"Status\" = 'open'",
                    &[&link_id],
                )?
                .iter()
                .map(|r| r.get(0))
                .collect();
            let _ = occupancy::mark_expired(&mut tx, open, reason)?;
        } else {
            let _ = occupancy::expire_stale(&mut tx, &link_id, occupancy::reservation_ttl(Some(ttl_minutes)))?;
        }
        tx.commit()?;
    }

    let children = conn.query(
        "SELECT \"Status\",\"SlotKey\",\"PspRedirectUrl\",\"PayerName\",\"PayerEmail\" FROM public.checkouts WHERE \"PaymentLinkId\" = $1",
        &[&link_id],
    )?;
    let mut taken = 0i64;
    let mut paid = 0i64;
    let mut mine = false;
    let mut started = false;
    let mut redirect: Option<String> = None;
    let mut payer_name: Option<String> = None;
    let mut payer_email: Option<String> = None;
    for row in &children {
        let status: String = row.get("Status");
        if occupancy::counts_toward_capacity(&status) {
            taken += 1;
        }
        if status == "paid" {
            paid += 1;
        }
        let slot: Option<String> = row.get("SlotKey");
        if slot_key.is_some() && slot.as_deref() == slot_key {
            mine = true;
            started = true;
            redirect = row.get("PspRedirectUrl");
            payer_name = row.get("PayerName");
            payer_email = row.get("PayerEmail");
        }
    }

    let mut status = occupancy::merchant_status(max_payers, taken).to_string();
    if max_payers == Some(1) && paid >= 1 {
        status = "already_paid".into();
    }

    let on_page = providers::is_on_page_url(redirect.as_deref());
    Ok(json!({
        "token": token,
        "amount": crate::hosting::decimal_json(amount),
        "currency": currency,
        "status": status,
        "email_required": providers::requires_email(&provider)
            && payer_email.as_deref().is_none_or(|e| !buyer_email::is_usable(Some(e))),
        "started": started,
        "mine": mine,
        "provider": provider,
        "redirect_url": if on_page { Value::Null } else { json!(redirect) },
        "solana_pay_url": if on_page { json!(redirect) } else { Value::Null },
        "solana_cluster": Value::Null,
        "payer_name": payer_name,
        "payer_email": payer_email,
        "pay_url": checkout_page_url(checkout_base, &token),
        "max_payers": max_payers,
        "taken": taken,
        "paid": paid,
        "remaining": occupancy::remaining_unclamped(max_payers, taken),
        "unlimited": max_payers.is_none(),
    }))
}
