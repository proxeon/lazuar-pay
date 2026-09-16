//! Prometheus scrape. OTLP is out of this slice (033/14 phase 4 skipped).

#![forbid(unsafe_code)]

use std::sync::OnceLock;

use prometheus::{Encoder, IntCounterVec, IntGauge, IntGaugeVec, Opts, Registry, TextEncoder};

pub const OK: &str = "ok";
pub const IGNORED: &str = "ignored";
pub const VERIFY_FAILED: &str = "verify_failed";
pub const DEDUPE: &str = "dedupe";
pub const CHECKOUT_MISSING: &str = "checkout_missing";
pub const AMOUNT_MISMATCH: &str = "amount_mismatch";
pub const CURRENCY_MISMATCH: &str = "currency_mismatch";
pub const SECRET_UNAVAILABLE: &str = "secret_unavailable";

const RAILS: &[&str] = &[
    "test", "stripe", "chip", "billplz", "xendit", "razorpay", "solana",
];

struct Metrics {
    registry: Registry,
    psp: IntCounterVec,
    refunds_pending: IntGaugeVec,
    refunds_oldest: IntGaugeVec,
    jobs_poison: IntGauge,
    jobs_leased: IntGauge,
}

fn metrics() -> &'static Metrics {
    static M: OnceLock<Metrics> = OnceLock::new();
    M.get_or_init(|| {
        let registry = Registry::new();
        let psp = IntCounterVec::new(
            Opts::new(
                "psp_parse_outcome",
                "PSP webhook parse/apply outcome (one per delivered event)",
            ),
            &["rail", "outcome"],
        )
        .expect("psp_parse_outcome");
        let refunds_pending = IntGaugeVec::new(
            Opts::new("refunds_pending", "Pending refund rows by rail"),
            &["rail"],
        )
        .expect("refunds_pending");
        let refunds_oldest = IntGaugeVec::new(
            Opts::new(
                "refunds_pending_oldest_seconds",
                "Age in seconds of the oldest pending refund on this rail",
            ),
            &["rail"],
        )
        .expect("refunds_pending_oldest_seconds");
        let jobs_poison = IntGauge::with_opts(Opts::new(
            "jobs_poison",
            "Outbound deliveries in poison status",
        ))
        .expect("jobs_poison");
        let jobs_leased = IntGauge::with_opts(Opts::new(
            "jobs_leased",
            "Outbound deliveries currently leased",
        ))
        .expect("jobs_leased");
        registry
            .register(Box::new(psp.clone()))
            .expect("register psp");
        registry
            .register(Box::new(refunds_pending.clone()))
            .expect("register refunds_pending");
        registry
            .register(Box::new(refunds_oldest.clone()))
            .expect("register refunds_oldest");
        registry
            .register(Box::new(jobs_poison.clone()))
            .expect("register jobs_poison");
        registry
            .register(Box::new(jobs_leased.clone()))
            .expect("register jobs_leased");
        for rail in RAILS {
            refunds_pending.with_label_values(&[*rail]).set(0);
            refunds_oldest.with_label_values(&[*rail]).set(0);
            let _ = psp.with_label_values(&[*rail, OK]);
        }
        Metrics {
            registry,
            psp,
            refunds_pending,
            refunds_oldest,
            jobs_poison,
            jobs_leased,
        }
    })
}

pub fn psp_outcome(rail: &str, outcome: &str) {
    metrics().psp.with_label_values(&[rail, outcome]).inc();
}

pub fn set_refunds_pending(rail: &str, count: i64, oldest_seconds: i64) {
    let m = metrics();
    m.refunds_pending.with_label_values(&[rail]).set(count);
    m.refunds_oldest
        .with_label_values(&[rail])
        .set(oldest_seconds);
}

pub fn reset_refunds_pending() {
    let m = metrics();
    for rail in RAILS {
        m.refunds_pending.with_label_values(&[*rail]).set(0);
        m.refunds_oldest.with_label_values(&[*rail]).set(0);
    }
}

pub fn set_jobs(poison: i64, leased: i64) {
    let m = metrics();
    m.jobs_poison.set(poison);
    m.jobs_leased.set(leased);
}

pub fn encode() -> String {
    let mut buf = Vec::new();
    let encoder = TextEncoder::new();
    let families = metrics().registry.gather();
    encoder.encode(&families, &mut buf).ok();
    String::from_utf8_lossy(&buf).into_owned()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn encode_includes_type_line() {
        let body = encode();
        assert!(body.contains("psp_parse_outcome"), "{body}");
        assert!(body.contains("refunds_pending"), "{body}");
    }
}
