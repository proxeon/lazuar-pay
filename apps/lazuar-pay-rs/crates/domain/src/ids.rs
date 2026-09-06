//! Opaque identifiers. SQL uuids are JSON 32-hex (`N` format) on the TypeSpec wire.

use std::fmt;
use std::str::FromStr;

use uuid::Uuid;

use crate::error::ParseIdError;

macro_rules! uuid_id {
    ($name:ident) => {
        #[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
        pub struct $name(Uuid);

        impl $name {
            pub const fn from_uuid(id: Uuid) -> Self {
                Self(id)
            }

            pub const fn as_uuid(self) -> Uuid {
                self.0
            }

            /// TypeSpec / JSON: lowercase 32-hex, matching `Guid.ToString("N")`.
            pub fn to_wire(self) -> String {
                self.0.as_simple().to_string()
            }

            pub fn from_wire(s: &str) -> Result<Self, ParseIdError> {
                parse_uuid_n(s).map(Self)
            }

            pub fn from_u128(n: u128) -> Self {
                Self(Uuid::from_u128(n))
            }
        }

        impl fmt::Display for $name {
            fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
                f.write_str(&self.to_wire())
            }
        }
    };
}

uuid_id!(PaymentId);
uuid_id!(AttemptId);
uuid_id!(SettlementId);
uuid_id!(ChargeId);
uuid_id!(RefundId);
uuid_id!(PaymentLinkId);

/// One org. Today's `OrgId`. SQL `text` (08): tests use `"t1"`, production is a uuid string.
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
pub struct TenantId(String);

impl TenantId {
    pub fn new(raw: impl Into<String>) -> Self {
        Self(raw.into())
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl fmt::Display for TenantId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.0)
    }
}

/// Rail event id / chain txid. Not a uuid. Unique with `(tenant, rail, proof_id)`.
///
/// Razorpay: body-derived `captured:{payment_id}`, **not** `X-Razorpay-Event-Id` (issue 018).
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
pub struct ProofId(String);

impl ProofId {
    pub fn new(raw: impl Into<String>) -> Self {
        Self(raw.into())
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl fmt::Display for ProofId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.0)
    }
}

/// Unguessable buyer capability. Not a uuid-as-capability.
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
pub struct PublicToken(String);

impl PublicToken {
    pub fn new(raw: impl Into<String>) -> Self {
        Self(raw.into())
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

/// `"psp:stripe"` | `"one:{sub}"` | `"worker"`.
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
pub struct ActorId(String);

impl ActorId {
    pub fn new(raw: impl Into<String>) -> Self {
        Self(raw.into())
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

fn parse_uuid_n(s: &str) -> Result<Uuid, ParseIdError> {
    let t = s.trim();
    if t.len() == 32 && t.bytes().all(|b| b.is_ascii_hexdigit()) {
        let mut hyphenated = String::with_capacity(36);
        hyphenated.push_str(&t[0..8]);
        hyphenated.push('-');
        hyphenated.push_str(&t[8..12]);
        hyphenated.push('-');
        hyphenated.push_str(&t[12..16]);
        hyphenated.push('-');
        hyphenated.push_str(&t[16..20]);
        hyphenated.push('-');
        hyphenated.push_str(&t[20..32]);
        return Uuid::from_str(&hyphenated).map_err(|_| ParseIdError::Uuid);
    }
    Uuid::from_str(t).map_err(|_| ParseIdError::Uuid)
}
