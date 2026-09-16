//! Proof is the only thing that can settle. Admin cannot construct one.

use crate::ids::{PaymentId, ProofId};
use crate::money::{ChainId, Money};
use crate::rail::{ConnectorRefs, RailId};

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Proof {
    PspWebhook {
        rail: RailId,
        event_id: ProofId,
    },
    PspSync {
        rail: RailId,
        connector_txn_id: String,
    },
    ChainTx {
        chain: ChainId,
        txid: ProofId,
        confirmations: u32,
        finalized: bool,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ProofSufficiency {
    Insufficient,
    Seen,
    Confirmed,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SolanaConfirm {
    FinalizedOnly,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ConfirmPolicy {
    pub solana: SolanaConfirm,
}

impl ConfirmPolicy {
    pub const V1: Self = Self {
        solana: SolanaConfirm::FinalizedOnly,
    };
}

pub fn sufficiency(proof: &Proof, policy: &ConfirmPolicy) -> ProofSufficiency {
    match proof {
        Proof::PspWebhook { .. } | Proof::PspSync { .. } => ProofSufficiency::Confirmed,
        Proof::ChainTx { finalized, .. } => match policy.solana {
            SolanaConfirm::FinalizedOnly if *finalized => ProofSufficiency::Confirmed,
            SolanaConfirm::FinalizedOnly => ProofSufficiency::Insufficient,
        },
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum IgnoreReason {
    Preauthorized,
    UnknownEvent,
    UnrecognizedShape,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Binding {
    Payment { id: PaymentId },
    Session { session_id: String },
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum WebhookOutcome {
    Ignored {
        reason: IgnoreReason,
    },
    Failed {
        binding: Binding,
        reason: String,
    },
    Paid {
        binding: Binding,
        received: Money,
        refs: ConnectorRefs,
    },
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum SyncOutcome {
    Unknown,
    Failed {
        reason: String,
    },
    Paid {
        received: Money,
        refs: ConnectorRefs,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RefundOutcome {
    Settled,
    Rejected,
    Unknown,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RefundReason {
    Merchant,
    LatePay,
    OverCapacity,
    Surplus,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RefundStatus {
    Pending,
    Succeeded,
    Failed,
    Manual,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::ProofId;
    use crate::money::ChainId;
    use crate::rail::RailId;

    #[test]
    fn chain_unfinalized_is_insufficient_in_v1() {
        let p = Proof::ChainTx {
            chain: ChainId::SOLANA,
            txid: ProofId::new("sig"),
            confirmations: 1,
            finalized: false,
        };
        assert_eq!(
            sufficiency(&p, &ConfirmPolicy::V1),
            ProofSufficiency::Insufficient
        );
    }

    #[test]
    fn psp_webhook_is_confirmed() {
        let p = Proof::PspWebhook {
            rail: RailId::CHIP,
            event_id: ProofId::new("evt_1"),
        };
        assert_eq!(
            sufficiency(&p, &ConfirmPolicy::V1),
            ProofSufficiency::Confirmed
        );
    }
}
