//! Three aggregates. Payment status is a persisted projection of `propose`.

use time::OffsetDateTime;

use crate::ids::{
    AttemptId, ChargeId, PaymentId, PaymentLinkId, PublicToken, RefundId, SettlementId, TenantId,
};
use crate::money::{Money, RateLock};
use crate::proof::{Proof, RefundReason, RefundStatus};
use crate::rail::{ConnectorRefs, HostedSession, MethodId, RailId};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum PaymentStatus {
    Open,
    Processing,
    Settled,
    Failed,
    Expired,
}

impl PaymentStatus {
    pub fn is_terminal(self) -> bool {
        matches!(self, Self::Settled | Self::Failed | Self::Expired)
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum ExceptionStatus {
    None,
    Partial,
    Over,
    Late,
    OverCapacity,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum ReturnKind {
    Late,
    OverCapacity,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Intake {
    Open,
    Taken {
        charge_id: ChargeId,
    },
    Returned {
        kind: ReturnKind,
        refund_id: RefundId,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum TerminalReason {
    None,
    PspFailed,
    WatchTimeout,
    ReservationTtl,
    OverCapacity,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct OccupancyRef {
    pub link_id: PaymentLinkId,
    pub slot_key: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Payment {
    pub id: PaymentId,
    pub tenant_id: TenantId,
    pub public_token: PublicToken,
    pub quoted: Money,
    pub rate: RateLock,
    pub status: PaymentStatus,
    pub exception: ExceptionStatus,
    pub intake: Intake,
    pub terminal_reason: TerminalReason,
    pub expires_at: OffsetDateTime,
    /// v1: unused by `propose`. Stored for a later "stop looking" policy.
    pub monitoring_until: OffsetDateTime,
    pub occupancy: Option<OccupancyRef>,
    pub version: u64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum AttemptStatus {
    Created,
    Pending,
    SessionLive,
    Failed,
    Expired,
    Settled,
}

impl AttemptStatus {
    pub fn is_live(self) -> bool {
        matches!(self, Self::Created | Self::Pending | Self::SessionLive)
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Attempt {
    pub id: AttemptId,
    pub payment_id: PaymentId,
    pub rail: RailId,
    pub method: MethodId,
    pub quoted_rail: Money,
    pub status: AttemptStatus,
    pub session: Option<HostedSession>,
    pub refs: ConnectorRefs,
    pub fail_reason: Option<TerminalReason>,
    pub version: u64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum SettlementState {
    Seen,
    Confirmed,
    Reorged,
    Ignored,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Settlement {
    pub id: SettlementId,
    pub attempt_id: AttemptId,
    pub received: Money,
    pub proof: Proof,
    pub state: SettlementState,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Charge {
    pub id: ChargeId,
    pub payment_id: PaymentId,
    pub amount: Money,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Refund {
    pub id: RefundId,
    pub payment_id: PaymentId,
    pub amount: Money,
    pub reason: RefundReason,
    pub status: RefundStatus,
}
