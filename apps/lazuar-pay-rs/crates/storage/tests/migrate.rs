//! 033/01: migrate `pay_rs` and prove the unique indexes that are the money locks.
//! No `apply`, no `propose`.

mod support;

use support::{
    insert_attempt, insert_charge, insert_link, insert_payment, insert_refund, is_check_violation,
    is_unique_violation, pool, token,
};

#[tokio::test]
async fn migrate_up_creates_pay_rs() {
    let pool = pool().await;
    let nsp: (String,) =
        sqlx::query_as("SELECT nspname FROM pg_namespace WHERE nspname = 'pay_rs'")
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(nsp.0, "pay_rs");
}

#[tokio::test]
async fn no_public_checkouts_written() {
    let pool = pool().await;
    let row: (Option<String>,) = sqlx::query_as("SELECT to_regclass('public.payments')::text")
        .fetch_one(&pool)
        .await
        .unwrap();
    assert!(row.0.is_none());
    let checkouts: (Option<String>,) =
        sqlx::query_as("SELECT to_regclass('public.checkouts')::text")
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(checkouts.0.is_none());
}

#[tokio::test]
async fn tenant_id_accepts_t1() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    insert_payment(&mut tx, &token("tok-t1")).await.unwrap();
    tx.commit().await.unwrap();
}

#[tokio::test]
async fn lock_index_names_exist() {
    let pool = pool().await;
    for name in [
        "payments_public_token",
        "payments_link_slot",
        "attempts_one_live",
        "attempts_session_id",
        "settlements_proof",
        "reservations_locator",
        "proofs_txid",
        "charges_payment",
        "refunds_late_one",
        "refunds_idem",
        "inbound_events_pk",
        "deliveries_event",
        "documents_number",
        "idempotency_pk",
        "one_delivery",
        "credentials_pk",
    ] {
        let found: (i64,) = sqlx::query_as(
            r#"
            SELECT count(*) FROM pg_indexes
             WHERE schemaname = 'pay_rs' AND indexname = $1
            "#,
        )
        .bind(name)
        .fetch_one(&pool)
        .await
        .unwrap();
        assert_eq!(found.0, 1, "missing index {name}");
    }
}

#[tokio::test]
async fn charges_payment_rejects_second_fulfill() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let payment = insert_payment(&mut tx, &token("tok-charge")).await.unwrap();
    insert_charge(&mut tx, payment).await.unwrap();
    let err = insert_charge(&mut tx, payment).await.unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn refunds_late_one_rejects_second_return() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let payment = insert_payment(&mut tx, &token("tok-late")).await.unwrap();
    insert_refund(&mut tx, payment, "late_pay", None, None)
        .await
        .unwrap();
    let err = insert_refund(&mut tx, payment, "over_capacity", None, None)
        .await
        .unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn attempts_one_live_rejects_second_live() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let payment = insert_payment(&mut tx, &token("tok-live")).await.unwrap();
    insert_attempt(&mut tx, payment, "session_live", None)
        .await
        .unwrap();
    let err = insert_attempt(&mut tx, payment, "pending", None)
        .await
        .unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn attempts_one_live_allows_after_terminal() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let payment = insert_payment(&mut tx, &token("tok-term")).await.unwrap();
    insert_attempt(&mut tx, payment, "failed", None)
        .await
        .unwrap();
    insert_attempt(&mut tx, payment, "pending", None)
        .await
        .unwrap();
    tx.commit().await.unwrap();
}

#[tokio::test]
async fn inbound_events_rejects_duplicate_proof() {
    let pool = pool().await;
    let proof = token("evt");
    sqlx::query(
        r#"
        INSERT INTO pay_rs.inbound_events (tenant_id, rail, proof_id)
        VALUES ('t1', 'stripe', $1)
        "#,
    )
    .bind(&proof)
    .execute(&pool)
    .await
    .unwrap();
    let err = sqlx::query(
        r#"
        INSERT INTO pay_rs.inbound_events (tenant_id, rail, proof_id)
        VALUES ('t1', 'stripe', $1)
        "#,
    )
    .bind(&proof)
    .execute(&pool)
    .await
    .unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn proofs_insert_without_attempt_id() {
    let pool = pool().await;
    let txid = token("sig");
    sqlx::query(
        r#"
        INSERT INTO pay_rs.proofs (chain, txid, raw)
        VALUES ('solana', $1, '{}'::jsonb)
        "#,
    )
    .bind(&txid)
    .execute(&pool)
    .await
    .unwrap();
    let bound: (Option<uuid::Uuid>,) =
        sqlx::query_as("SELECT bound_attempt_id FROM pay_rs.proofs WHERE txid = $1")
            .bind(&txid)
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(bound.0.is_none());
}

#[tokio::test]
async fn proofs_txid_rejects_duplicate_chain_tx() {
    let pool = pool().await;
    let txid = token("sig-dup");
    sqlx::query(
        r#"
        INSERT INTO pay_rs.proofs (chain, txid)
        VALUES ('solana', $1)
        "#,
    )
    .bind(&txid)
    .execute(&pool)
    .await
    .unwrap();
    let err = sqlx::query(
        r#"
        INSERT INTO pay_rs.proofs (chain, txid)
        VALUES ('solana', $1)
        "#,
    )
    .bind(&txid)
    .execute(&pool)
    .await
    .unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn payments_public_token_unique() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let tok = token("same-token");
    insert_payment(&mut tx, &tok).await.unwrap();
    let err = insert_payment(&mut tx, &tok).await.unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn payments_link_slot_unique() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let link = insert_link(&mut tx, &token("link-a")).await.unwrap();
    sqlx::query(
        r#"
        INSERT INTO pay_rs.payments (
            tenant_id, public_token, amount_minor, currency, exponent,
            status, expires_at, monitoring_until, payment_link_id, slot_key
        ) VALUES (
            't1', $2, 1000, 'MYR', 2,
            'open', now() + interval '30 minutes', now() + interval '30 minutes',
            $1, 'slot-1'
        )
        "#,
    )
    .bind(link)
    .bind(token("p1"))
    .execute(&mut *tx)
    .await
    .unwrap();
    let err = sqlx::query(
        r#"
        INSERT INTO pay_rs.payments (
            tenant_id, public_token, amount_minor, currency, exponent,
            status, expires_at, monitoring_until, payment_link_id, slot_key
        ) VALUES (
            't1', $2, 1000, 'MYR', 2,
            'open', now() + interval '30 minutes', now() + interval '30 minutes',
            $1, 'slot-1'
        )
        "#,
    )
    .bind(link)
    .bind(token("p2"))
    .execute(&mut *tx)
    .await
    .unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn payments_link_slot_allows_null_slots() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let link = insert_link(&mut tx, &token("link-null")).await.unwrap();
    sqlx::query(
        r#"
        INSERT INTO pay_rs.payments (
            tenant_id, public_token, amount_minor, currency, exponent,
            status, expires_at, monitoring_until, payment_link_id
        ) VALUES (
            't1', $2, 1000, 'MYR', 2,
            'open', now() + interval '30 minutes', now() + interval '30 minutes',
            $1
        )
        "#,
    )
    .bind(link)
    .bind(token("p3"))
    .execute(&mut *tx)
    .await
    .unwrap();
    sqlx::query(
        r#"
        INSERT INTO pay_rs.payments (
            tenant_id, public_token, amount_minor, currency, exponent,
            status, expires_at, monitoring_until, payment_link_id
        ) VALUES (
            't1', $2, 1000, 'MYR', 2,
            'open', now() + interval '30 minutes', now() + interval '30 minutes',
            $1
        )
        "#,
    )
    .bind(link)
    .bind(token("p4"))
    .execute(&mut *tx)
    .await
    .unwrap();
    tx.commit().await.unwrap();
}

#[tokio::test]
async fn refunds_idem_rejects_duplicate_key() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let payment = insert_payment(&mut tx, &token("tok-idem")).await.unwrap();
    let charge = insert_charge(&mut tx, payment).await.unwrap();
    let idem = token("idem");
    insert_refund(
        &mut tx,
        payment,
        "merchant",
        Some(charge),
        Some(idem.as_str()),
    )
    .await
    .unwrap();
    let err = insert_refund(
        &mut tx,
        payment,
        "merchant",
        Some(charge),
        Some(idem.as_str()),
    )
    .await
    .unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn attempts_session_id_unique() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let a = insert_payment(&mut tx, &token("tok-sess-a")).await.unwrap();
    let b = insert_payment(&mut tx, &token("tok-sess-b")).await.unwrap();
    let sid = token("cs");
    insert_attempt(&mut tx, a, "session_live", Some(sid.as_str()))
        .await
        .unwrap();
    let err = insert_attempt(&mut tx, b, "session_live", Some(sid.as_str()))
        .await
        .unwrap_err();
    assert!(is_unique_violation(&err), "{err}");
}

#[tokio::test]
async fn intake_taken_requires_charge_id() {
    let pool = pool().await;
    let err = sqlx::query(
        r#"
        INSERT INTO pay_rs.payments (
            tenant_id, public_token, amount_minor, currency, exponent,
            status, intake, expires_at, monitoring_until
        ) VALUES (
            't1', $1, 1000, 'MYR', 2,
            'settled', 'taken', now() + interval '30 minutes', now() + interval '30 minutes'
        )
        "#,
    )
    .bind(token("tok-taken"))
    .execute(&pool)
    .await
    .unwrap_err();
    assert!(is_check_violation(&err), "{err}");
}

#[tokio::test]
async fn merchant_refund_requires_charge_id() {
    let pool = pool().await;
    let mut tx = pool.begin().await.unwrap();
    let payment = insert_payment(&mut tx, &token("tok-merch")).await.unwrap();
    let err = insert_refund(&mut tx, payment, "merchant", None, None)
        .await
        .unwrap_err();
    assert!(is_check_violation(&err), "{err}");
}
