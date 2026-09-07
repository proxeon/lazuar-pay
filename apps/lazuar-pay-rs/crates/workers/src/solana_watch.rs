//! Poll reservations. Insert proofs with no attempt_id.

use chain::solana::LiveRpc;
use rails::solana::{
    mint, signatures_from_rpc, try_normalize, validate, FakeSolanaRpc, SolanaError,
};
use sqlx::PgPool;
use storage::ApplyError;

pub enum Rpc {
    Fake(FakeSolanaRpc),
    Live(LiveRpc),
}

impl Rpc {
    async fn get_transaction(&self, signature: &str) -> Result<String, SolanaError> {
        match self {
            Rpc::Fake(f) => f.get_transaction(signature),
            Rpc::Live(l) => l.get_transaction(signature).await,
        }
    }

    async fn get_signatures(&self, reference: &str) -> Result<String, SolanaError> {
        match self {
            Rpc::Fake(f) => f.get_signatures(reference),
            Rpc::Live(l) => l.get_signatures(reference).await,
        }
    }
}

pub async fn once(pool: &PgPool, rpc: &Rpc, cluster: &str) -> Result<usize, ApplyError> {
    let rows = storage::claim_watch_reservations(pool, 20).await?;
    let mut n = 0;
    let cluster_mint = mint(cluster);
    for row in rows {
        let Some(vault) = row.vault.as_deref().and_then(try_normalize) else {
            continue;
        };
        let sigs_json = match rpc.get_signatures(&row.locator).await {
            Ok(s) => s,
            Err(SolanaError::Throttled) => return Err(ApplyError::Conflict),
            Err(_) => continue,
        };
        for sig in signatures_from_rpc(&sigs_json) {
            let tx = match rpc.get_transaction(&sig).await {
                Ok(t) => t,
                Err(SolanaError::Throttled) => return Err(ApplyError::Conflict),
                Err(_) => continue,
            };
            if validate(
                &tx,
                &vault,
                row.amount_minor,
                cluster_mint,
                &row.locator,
                &row.payment_id.to_wire(),
                &sig,
            )
            .is_err()
            {
                continue;
            }
            if storage::insert_proof(pool, "solana", &sig, &tx)
                .await
                .unwrap_or(false)
            {
                n += 1;
            }
        }
    }
    Ok(n)
}
