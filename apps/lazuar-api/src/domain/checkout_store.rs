//! Port of `Checkouts/CheckoutStore.cs` + `Checkouts/CheckoutSession.cs`.
//!
//! Idempotency: `(OrgId, Key)` is the PRIMARY KEY of `idempotency_keys`
//! (D003: no ORM — the unique-violation replay path inspects SQLSTATE 23505).
//! Same key + same fingerprint (amount, currency, provider, interval, all
//! case-insensitive) replays the existing checkout; same key + different
//! fingerprint is a conflict. This is what makes checkout mint idempotent
//! under retry, and what the Start-race path (issue 007) leans on.

use chrono::{DateTime, Utc};
use rust_decimal::Decimal;
use uuid::Uuid;

#[derive(Debug, Clone)]
pub struct CheckoutSession {
    pub id: String,
    pub org_id: String,
    pub provider: Option<String>,
    pub product_id: Option<String>,
    pub payment_link_id: Option<String>,
    pub slot_key: Option<String>,
    pub amount: Decimal,
    pub currency: String,
    pub status: String,
    pub public_token: String,
    pub pay_url: Option<String>,
    pub interval: Option<String>,
    pub success_url: Option<String>,
    pub cancel_url: Option<String>,
    pub payer_name: Option<String>,
    pub payer_email: Option<String>,
    pub created_at: DateTime<Utc>,
}

/// The input to `create` — everything a checkout starts with before persistence
/// fills in `public_token` and defaults.
#[derive(Debug, Clone)]
pub struct NewCheckout {
    pub id: Uuid,
    pub org_id: String,
    pub provider: Option<String>,
    pub product_id: Option<String>,
    pub payment_link_id: Option<String>,
    pub slot_key: Option<String>,
    pub amount: Decimal,
    pub currency: String,
    pub status: String,
    pub interval: Option<String>,
    pub success_url: Option<String>,
    pub cancel_url: Option<String>,
    /// When set (checkout HTTP mint), 64 uppercase hex. Store fallback is 32 hex.
    pub public_token: Option<String>,
}

#[derive(Debug, thiserror::Error)]
pub enum CreateError {
    /// C# `IdempotencyConflictException`: key reused with a different body.
    #[error("idempotency key reused with a different body")]
    Conflict,
    #[error("database: {0}")]
    Db(#[from] postgres::Error),
}

const SELECT_COLUMNS: &str = "\
    SELECT \"Id\",\"OrgId\",\"Provider\",\"ProductId\",\"PaymentLinkId\",\"SlotKey\",\
    \"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\"SuccessUrl\",\
    \"CancelUrl\",\"PayerName\",\"PayerEmail\",\"CreatedAt\" FROM public.checkouts";

fn map_row(row: &postgres::Row) -> CheckoutSession {
    CheckoutSession {
        id: row.get("Id"),
        org_id: row.get("OrgId"),
        provider: row.get("Provider"),
        product_id: row.get("ProductId"),
        payment_link_id: row.get("PaymentLinkId"),
        slot_key: row.get("SlotKey"),
        public_token: row.get("PublicToken"),
        amount: row.get("Amount"),
        currency: row.get("Currency"),
        status: row.get("Status"),
        interval: row.get("Interval"),
        success_url: row.get("SuccessUrl"),
        cancel_url: row.get("CancelUrl"),
        payer_name: row.get("PayerName"),
        payer_email: row.get("PayerEmail"),
        pay_url: None,
        created_at: row.get("CreatedAt"),
    }
}

fn fingerprint(existing: &CheckoutSession, session: &NewCheckout) -> bool {
    let default_interval = |i: &Option<String>| {
        i.clone()
            .unwrap_or_else(|| "one_off".to_string())
            .to_lowercase()
    };
    existing.amount == session.amount
        && existing.currency.eq_ignore_ascii_case(&session.currency)
        && existing
            .provider
            .as_deref()
            .map(|p| p.eq_ignore_ascii_case(session.provider.as_deref().unwrap_or("")))
            .unwrap_or(session.provider.is_none())
        && default_interval(&existing.interval) == default_interval(&session.interval)
}

/// C# `Convert.ToHexString(Guid.NewGuid().ToByteArray())` — 32 uppercase hex chars.
fn new_public_token() -> String {
    hex::encode_upper(*uuid::Uuid::new_v4().as_bytes())
}

/// Idempotent create. Same key + same body replays the original checkout;
/// same key + different body is [`CreateError::Conflict`]; a lost insert race
/// replays the winner's row (issues/001 — checkout mint is idempotent on key).
pub fn create(
    conn: &mut postgres::Client,
    session: &NewCheckout,
    idempotency_key: Option<&str>,
) -> Result<CheckoutSession, CreateError> {
    if let Some(key) = idempotency_key.map(str::trim).filter(|k| !k.is_empty()) {
        if let Some(existing) = find_by_key(conn, &session.org_id, key)? {
            if !fingerprint(&existing, session) {
                return Err(CreateError::Conflict);
            }
            return Ok(existing);
        }
    }

    let row = InsertRow::from_new(session);
    let key_opt = idempotency_key.map(str::trim).filter(|k| !k.is_empty());
    let insert_result = (|| -> Result<(), postgres::Error> {
        let mut tx = conn.transaction()?;
        tx.execute(
            "INSERT INTO public.checkouts \
             (\"Id\",\"OrgId\",\"Provider\",\"ProductId\",\"PaymentLinkId\",\"SlotKey\",\
             \"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\
             \"SuccessUrl\",\"CancelUrl\",\"CreatedAt\") \
             VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)",
            &[
                &row.id, &row.org_id, &row.provider, &row.product_id, &row.payment_link_id,
                &row.slot_key, &row.public_token, &row.amount, &row.currency, &row.status,
                &row.interval, &row.success_url, &row.cancel_url, &row.created_at,
            ],
        )?;
        if let Some(key) = key_opt {
            tx.execute(
                "INSERT INTO public.idempotency_keys (\"OrgId\",\"Key\",\"CheckoutId\") \
                 VALUES ($1,$2,$3)",
                &[&row.org_id, &key, &row.id],
            )?;
        }
        tx.commit()?;
        Ok(())
    })();

    if let Err(err) = insert_result {
        // The C# path catches DbUpdateException and replays. Same rule here, but
        // only when an idempotency key is in play and the violation is uniqueness —
        // everything else is a real failure and re-raised.
        let is_unique_violation = err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION);
        let key = idempotency_key.map(str::trim).filter(|k| !k.is_empty());
        if !is_unique_violation || key.is_none() {
            return Err(CreateError::Db(err));
        }

        let key = key.unwrap();
        let raced = find_by_key(conn, &session.org_id, key)?;
        match raced {
            Some(existing) if fingerprint(&existing, session) => Ok(existing),
            Some(_) => Err(CreateError::Conflict),
            None => Err(CreateError::Db(err)),
        }
    } else {
        let inserted = find_by_id(conn, &session.id.to_string())?
            .expect("inserted checkout must be readable");
        Ok(inserted)
    }
}

struct InsertRow {
    id: String,
    org_id: String,
    provider: Option<String>,
    product_id: Option<String>,
    payment_link_id: Option<String>,
    slot_key: Option<String>,
    public_token: String,
    amount: Decimal,
    currency: String,
    status: String,
    interval: Option<String>,
    success_url: Option<String>,
    cancel_url: Option<String>,
    created_at: DateTime<Utc>,
}

impl InsertRow {
    fn from_new(session: &NewCheckout) -> Self {
        Self {
            id: session.id.to_string(),
            org_id: session.org_id.clone(),
            provider: session.provider.clone(),
            product_id: session.product_id.clone(),
            payment_link_id: session.payment_link_id.clone(),
            slot_key: session.slot_key.clone(),
            public_token: session
                .public_token
                .clone()
                .filter(|t| !t.trim().is_empty())
                .unwrap_or_else(new_public_token),
            amount: session.amount,
            currency: session.currency.clone(),
            status: session.status.clone(),
            interval: Some(session.interval.clone().unwrap_or_else(|| "one_off".into())),
            success_url: session.success_url.clone(),
            cancel_url: session.cancel_url.clone(),
            created_at: Utc::now(),
        }
    }
}

fn find_by_key(
    conn: &mut postgres::Client,
    org_id: &str,
    key: &str,
) -> Result<Option<CheckoutSession>, postgres::Error> {
    let checkout_id: Option<String> = conn
        .query_opt(
            "SELECT \"CheckoutId\" FROM public.idempotency_keys \
             WHERE \"OrgId\" = $1 AND \"Key\" = $2",
            &[&org_id, &key],
        )?
        .map(|row| row.get(0));
    match checkout_id {
        Some(id) => find_by_id(conn, &id),
        None => Ok(None),
    }
}

fn find_by_id(conn: &mut postgres::Client, id: &str) -> Result<Option<CheckoutSession>, postgres::Error> {
    conn.query_opt(&format!("{SELECT_COLUMNS} WHERE \"Id\" = $1"), &[&id])
        .map(|opt| opt.map(|row| map_row(&row)))
}

pub fn get(conn: &mut postgres::Client, id: &str) -> Result<Option<CheckoutSession>, postgres::Error> {
    find_by_id(conn, id)
}

pub fn get_by_public_token(
    conn: &mut postgres::Client,
    token: &str,
) -> Result<Option<CheckoutSession>, postgres::Error> {
    conn.query_opt(&format!("{SELECT_COLUMNS} WHERE \"PublicToken\" = $1"), &[&token])
        .map(|opt| opt.map(|row| map_row(&row)))
}
