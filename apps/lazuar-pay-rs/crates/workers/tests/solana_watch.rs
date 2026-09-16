mod support;

use domain::money::{Currency, Money};
use domain::proof::Proof;
use domain::rail::{ConnectorRefs, HostedSession, RailId};
use domain::{ProofId, PublicToken, TenantId};
use rails::solana::{encode, sample_address, tx_json, FakeSolanaRpc, DEVNET_MINT};
use storage::{apply, ApplyCmd, ApplyOutcome, MintSpec};
use support::pool;
use time::{Duration, OffsetDateTime};
use tokio::sync::Mutex;
use uuid::Uuid;
use workers::solana_bind;
use workers::solana_watch::{self, Rpc};

fn usdc10() -> Money {
    Money::from_quoted_str("10.00", Currency::USDC_SOLANA).unwrap()
}

static WATCH: Mutex<()> = Mutex::const_new(());

async fn mint_solana(
    pool: &sqlx::PgPool,
) -> (TenantId, domain::PaymentId, domain::AttemptId, String) {
    let now = OffsetDateTime::now_utc();
    let tenant = TenantId::new(format!("t-{}", Uuid::new_v4().simple()));
    let minted = apply(
        pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: tenant.clone(),
            public_token: PublicToken::new(format!("p-{}", Uuid::new_v4().simple())),
            quoted: usdc10(),
            expires_at: now + Duration::minutes(30),
            monitoring_until: now + Duration::minutes(30),
            payment_link_id: None,
            slot_key: None,
            success_url: None,
            cancel_url: None,
            rail: RailId::SOLANA,
        }),
    )
    .await
    .unwrap();
    let ApplyOutcome::Minted { payment_id } = minted else {
        panic!("mint");
    };
    let started = apply(
        pool,
        ApplyCmd::StartAttempt {
            payment_id,
            rail: RailId::SOLANA,
        },
    )
    .await
    .unwrap();
    let ApplyOutcome::Started { attempt_id, .. } = started else {
        panic!("start");
    };
    let reference = encode(&Uuid::new_v4().into_bytes());
    apply(
        pool,
        ApplyCmd::RecordSession {
            attempt_id,
            session: HostedSession {
                url: format!("solana:x?reference={reference}"),
                session_id: reference.clone(),
            },
        },
    )
    .await
    .unwrap();
    (tenant, payment_id, attempt_id, reference)
}

#[tokio::test]
async fn watch_paid_takes_once() {
    let _g = WATCH.lock().await;
    let pool = pool().await;
    let (tenant, payment_id, _a, reference) = mint_solana(&pool).await;
    let address = sample_address();
    storage::upsert_solana(&pool, tenant.as_str(), "addr", "devnet", &address)
        .await
        .unwrap();
    let signature = encode(&[11u8; 64]);
    let fake = FakeSolanaRpc::default();
    fake.set_sigs(&reference, &[&signature]);
    fake.set_tx(
        &signature,
        &tx_json(
            &signature,
            &address,
            DEVNET_MINT,
            "10000000",
            &reference,
            &payment_id.to_wire(),
        ),
    );
    solana_watch::once(&pool, &Rpc::Fake(fake), "devnet")
        .await
        .unwrap();
    solana_bind::once(&pool).await.unwrap();
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);
    solana_watch::once(&pool, &Rpc::Fake(FakeSolanaRpc::default()), "devnet")
        .await
        .ok();
    solana_bind::once(&pool).await.ok();
    let charges2: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges2, 1);
}

#[tokio::test]
async fn unfinalized_does_not_take() {
    let _g = WATCH.lock().await;
    let pool = pool().await;
    let (tenant, payment_id, attempt_id, _r) = mint_solana(&pool).await;
    let now = OffsetDateTime::now_utc();
    apply(
        &pool,
        ApplyCmd::InjectPaid {
            tenant_id: tenant,
            rail: RailId::SOLANA,
            proof_id: "sig-unfinal".into(),
            payment_id,
            attempt_id,
            received: usdc10(),
            proof: Proof::ChainTx {
                chain: domain::ChainId::SOLANA,
                txid: ProofId::new("sig-unfinal"),
                confirmations: 1,
                finalized: false,
            },
            now,
            refs: ConnectorRefs {
                session_id: None,
                capture_id: None,
                network_id: None,
            },
        },
    )
    .await
    .unwrap();
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 0);
}
