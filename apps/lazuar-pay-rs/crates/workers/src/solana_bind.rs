//! Claim unbound proofs and apply ChainTx. Never written by the watcher.

use domain::proof::Proof;
use domain::rail::{ConnectorRefs, RailId};
use domain::ProofId;
use rails::solana::locators_in_tx;
use sqlx::PgPool;
use storage::{apply, ApplyCmd, ApplyError, ApplyOutcome};
use time::OffsetDateTime;

pub async fn once(pool: &PgPool) -> Result<usize, ApplyError> {
    let claimed = storage::claim_unbound_proofs(pool, 20).await?;
    let n = claimed.len();
    for p in claimed {
        let raw = p.raw.to_string();
        let mut bound = None;
        for loc in locators_in_tx(&raw) {
            if let Ok(Some(hit)) = storage::reservation_by_locator(pool, "solana", &loc).await {
                bound = Some((hit, loc));
                break;
            }
        }
        let Some(((tenant_id, payment_id, attempt_id), locator)) = bound else {
            continue;
        };
        let view = match storage::read::payment_by_id(pool, payment_id).await {
            Ok(Some(v)) => v,
            _ => continue,
        };
        let cmd = ApplyCmd::InjectPaid {
            tenant_id,
            rail: RailId::SOLANA,
            proof_id: p.txid.clone(),
            payment_id,
            attempt_id,
            received: view.quoted,
            proof: Proof::ChainTx {
                chain: domain::ChainId::SOLANA,
                txid: ProofId::new(p.txid.clone()),
                confirmations: 1,
                finalized: true,
            },
            now: OffsetDateTime::now_utc(),
            refs: ConnectorRefs {
                session_id: Some(locator),
                capture_id: Some(p.txid.clone()),
                network_id: Some(p.txid.clone()),
            },
        };
        match apply(pool, cmd).await {
            Ok(ApplyOutcome::Duplicate) | Ok(_) | Err(ApplyError::Conflict) => {
                let _ = storage::bind_proof(pool, p.id, attempt_id).await;
            }
            Err(ApplyError::Integrity) | Err(ApplyError::Paused) => {}
            Err(e) => return Err(e),
        }
    }
    Ok(n)
}
