//! Live hosted-session HTTP. Fake path stays in-process. Rails stay reqwest-free.

use std::time::Duration;

use domain::rail::HostedSession;
use reqwest::Client;
use serde_json::Value;
use storage::CredentialRow;
use workers::secret_box::SecretBox;

pub fn live_client() -> Client {
    Client::builder()
        .timeout(Duration::from_secs(10))
        .redirect(reqwest::redirect::Policy::none())
        .build()
        .unwrap_or_else(|_| Client::new())
}

pub fn unprotect(wrap_key: [u8; 32], cred: &CredentialRow) -> Option<String> {
    SecretBox::new(wrap_key)
        .unprotect_str(&cred.ciphertext)
        .ok()
        .filter(|s| !s.is_empty())
}

async fn post_json(
    http: &Client,
    url: &str,
    auth: Auth<'_>,
    idem: &str,
    body: &Value,
) -> Result<(u16, String), ()> {
    let mut req = http.post(url).json(body);
    req = match auth {
        Auth::Bearer(s) => req.bearer_auth(s),
        Auth::Basic { user, pass } => req.basic_auth(user, pass),
    };
    if !idem.is_empty() {
        req = req.header("Idempotency-Key", idem);
    }
    let res = req.send().await.map_err(|_| ())?;
    let status = res.status().as_u16();
    let text = res.text().await.unwrap_or_default();
    Ok((status, text))
}

enum Auth<'a> {
    Bearer(&'a str),
    Basic {
        user: &'a str,
        pass: Option<&'a str>,
    },
}

fn ok_body(status: u16, body: String) -> Result<String, ()> {
    if (200..300).contains(&status) {
        Ok(body)
    } else {
        Err(())
    }
}

pub async fn stripe_session(
    http: &Client,
    secret: &str,
    form: &[(String, String)],
    idem: &str,
) -> Result<HostedSession, ()> {
    let url = format!("{}/v1/checkout/sessions", rails::stripe::API_BASE);
    let res = http
        .post(url)
        .bearer_auth(secret)
        .header("Idempotency-Key", idem)
        .header("Content-Type", "application/x-www-form-urlencoded")
        .form(form)
        .send()
        .await
        .map_err(|_| ())?;
    let status = res.status().as_u16();
    let body = res.text().await.unwrap_or_default();
    let body = ok_body(status, body)?;
    rails::stripe::parse_hosted(&body).map_err(|_| ())
}

pub async fn chip_purchase(
    http: &Client,
    secret: &str,
    payload: &Value,
    idem: &str,
) -> Result<HostedSession, ()> {
    let url = format!("{}/purchases/", rails::chip::API_BASE);
    let (status, body) = post_json(http, &url, Auth::Bearer(secret), idem, payload).await?;
    let body = ok_body(status, body)?;
    rails::chip::parse_hosted(&body).map_err(|_| ())
}

pub async fn billplz_bill(
    http: &Client,
    secret: &str,
    host: &str,
    payload: &Value,
    idem: &str,
) -> Result<HostedSession, ()> {
    let url = format!("{host}/bills");
    let (status, body) = post_json(
        http,
        &url,
        Auth::Basic {
            user: secret,
            pass: Some(""),
        },
        idem,
        payload,
    )
    .await?;
    let body = ok_body(status, body)?;
    rails::billplz::parse_hosted(&body).map_err(|_| ())
}

pub async fn xendit_invoice(
    http: &Client,
    secret: &str,
    payload: &Value,
    idem: &str,
) -> Result<HostedSession, ()> {
    let url = format!("{}/v2/invoices", rails::xendit::API_BASE);
    let (status, body) = post_json(
        http,
        &url,
        Auth::Basic {
            user: secret,
            pass: Some(""),
        },
        idem,
        payload,
    )
    .await?;
    let body = ok_body(status, body)?;
    rails::xendit::parse_hosted(&body).map_err(|_| ())
}

pub async fn razorpay_link(
    http: &Client,
    key_id: &str,
    key_secret: &str,
    payload: &Value,
    idem: &str,
) -> Result<HostedSession, ()> {
    let url = format!("{}/payment_links", rails::razorpay::API_BASE);
    let (status, body) = post_json(
        http,
        &url,
        Auth::Basic {
            user: key_id,
            pass: Some(key_secret),
        },
        idem,
        payload,
    )
    .await?;
    let body = ok_body(status, body)?;
    rails::razorpay::parse_hosted(&body).map_err(|_| ())
}
