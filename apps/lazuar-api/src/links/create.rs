//! Port of `PaymentLinks/PaymentLinkEndpoints.Create` — validations + insert.
//! Writer-gate stays at the HTTP layer (needs One); everything DB-side lives here.

use chrono::Utc;
use postgres::Client;
use rust_decimal::Decimal;
use uuid::Uuid;

use crate::domain::currency;
use crate::rails::solana::money as solana_money;
use crate::rails::providers;

#[derive(Debug, Clone)]
pub struct CreateLinkInput<'a> {
    pub org_id: &'a str,
    pub provider: Option<&'a str>,
    pub amount: Option<Decimal>,
    pub currency: Option<&'a str>,
    pub product_id: Option<&'a str>,
    pub max_payers: Option<i32>,
    pub unlimited: bool,
}

#[derive(Debug)]
pub enum CreateLinkOutcome {
    Created {
        id: String,
        public_token: String,
        provider: String,
        amount: Decimal,
        currency: String,
        max_payers: Option<i32>,
    },
    Paused,
    AmountRequired,
    UnknownProvider,
    TestNotEnabled,
    RailNotConfigured,
    MaxPayersTooLow,
    /// Solana payload rules (subscriptions, catalog, currency, precision).
    SolanaMintError(String),
    /// Issues 003/014 — rail cannot settle the quoted currency.
    CurrencyUnsupported { currency: String, provider: String, supported: String },
    ProductNotFound,
    CatalogPriceMismatch,
}

pub fn create_link(
    conn: &mut Client,
    environment: &str,
    input: &CreateLinkInput,
) -> Result<CreateLinkOutcome, postgres::Error> {
    let org_id = input.org_id.trim();

    // Org settings row is created lazily on first touch (C# parity).
    let paused = conn
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .map(|row| row.get::<_, bool>(0));
    match paused {
        Some(true) => return Ok(CreateLinkOutcome::Paused),
        Some(false) => {}
        None => {
            conn.execute(
                "INSERT INTO public.org_settings (\"OrgId\",\"Currency\",\"ChargesPaused\") \
                 VALUES ($1,$2,$3) ON CONFLICT DO NOTHING",
                &[&org_id, &"MYR", &false],
            )?;
        }
    }
    if paused == Some(true) {
        return Ok(CreateLinkOutcome::Paused);
    }

    let Some(amount) = input.amount.filter(|a| *a > Decimal::ZERO) else {
        return Ok(CreateLinkOutcome::AmountRequired);
    };

    let Some(provider) = providers::try_normalize(input.provider) else {
        return Ok(CreateLinkOutcome::UnknownProvider);
    };

    if providers::is_test(provider) {
        if !providers::allows_test(environment) {
            return Ok(CreateLinkOutcome::TestNotEnabled);
        }
    } else {
        let cred = conn.query_opt(
            "SELECT 1 FROM public.gateway_credentials \
             WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
            &[&org_id, &provider],
        )?;
        if cred.is_none() {
            return Ok(CreateLinkOutcome::RailNotConfigured);
        }
    }

    let max_payers = if input.unlimited {
        None
    } else {
        match input.max_payers.unwrap_or(1) {
            n if n < 1 => return Ok(CreateLinkOutcome::MaxPayersTooLow),
            n => Some(n),
        }
    };

    let product_id = input.product_id.map(str::trim).filter(|s| !s.is_empty());
    if let Some(err) = solana_money::mint_error(provider, input.currency, None, product_id, Some(amount)) {
        return Ok(CreateLinkOutcome::SolanaMintError(err));
    }

    let currency = input
        .currency
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(|c| c.to_uppercase())
        .unwrap_or_else(|| "MYR".to_string());
    // Issues 003/014: reject currencies the rail cannot settle before minting.
    if !currency::is_supported(provider, &currency) {
        return Ok(CreateLinkOutcome::CurrencyUnsupported {
            currency: currency.clone(),
            provider: provider.to_string(),
            supported: currency::describe(provider),
        });
    }

    if let Some(product_id) = product_id {
        let product = conn.query_opt(
            "SELECT 1 FROM public.products WHERE \"Id\" = $1 AND \"OrgId\" = $2",
            &[&product_id, &org_id],
        )?;
        if product.is_none() {
            return Ok(CreateLinkOutcome::ProductNotFound);
        }
        // Catalog price check (C# PaymentLinkEndpoints.cs:117-124).
        let price = conn.query_opt(
            "SELECT \"Amount\",\"Currency\" FROM public.prices WHERE \"ProductId\" = $1",
            &[&product_id],
        )?;
        if let Some(price) = price {
            let price_amount: Decimal = price.get("Amount");
            let price_currency: String = price.get("Currency");
            if price_amount != amount || !price_currency.eq_ignore_ascii_case(&currency) {
                return Ok(CreateLinkOutcome::CatalogPriceMismatch);
            }
        }
    }

    let id = Uuid::new_v4().simple().to_string();
    // C# PaymentLink mint: 64 uppercase hex (two GUIDs).
    let public_token = format!(
        "{}{}",
        hex::encode_upper(*Uuid::new_v4().as_bytes()),
        hex::encode_upper(*Uuid::new_v4().as_bytes())
    );
    conn.execute(
        "INSERT INTO public.payment_links \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"ProductId\",\"Amount\",\"Currency\",\"MaxPayers\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)",
        &[
            &id,
            &org_id,
            &public_token,
            &provider,
            &product_id,
            &amount,
            &currency,
            &max_payers,
            &Utc::now(),
        ],
    )?;

    Ok(CreateLinkOutcome::Created {
        id,
        public_token,
        provider: provider.to_string(),
        amount,
        currency,
        max_payers,
    })
}
