//! Cash-basis journal facts. Apply posts these in the same TX as the fold.

use crate::error::JournalError;
use crate::fold::IntakeKind;
use crate::ids::{PaymentId, TenantId};
use crate::money::Money;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Account {
    Cash,
    PendingSettlement,
    Unearned,
    Revenue,
    RefundsPayable,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Dc {
    Debit,
    Credit,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct JournalLine {
    pub account: Account,
    pub dc: Dc,
    pub amount: Money,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct JournalEntry {
    pub tenant_id: TenantId,
    pub payment_id: PaymentId,
    lines: Vec<JournalLine>,
}

impl JournalEntry {
    pub fn try_new(
        tenant_id: TenantId,
        payment_id: PaymentId,
        lines: Vec<JournalLine>,
    ) -> Result<Self, JournalError> {
        if lines.is_empty() {
            return Err(JournalError::Empty);
        }
        let currency = lines[0].amount.currency();
        let mut debit: i128 = 0;
        let mut credit: i128 = 0;
        for line in &lines {
            if line.amount.currency() != currency {
                return Err(JournalError::MixedCurrency);
            }
            match line.dc {
                Dc::Debit => {
                    debit = debit
                        .checked_add(line.amount.minor())
                        .ok_or(JournalError::Unbalanced)?;
                }
                Dc::Credit => {
                    credit = credit
                        .checked_add(line.amount.minor())
                        .ok_or(JournalError::Unbalanced)?;
                }
            }
        }
        if debit != credit {
            return Err(JournalError::Unbalanced);
        }
        Ok(Self {
            tenant_id,
            payment_id,
            lines,
        })
    }

    pub fn lines(&self) -> &[JournalLine] {
        &self.lines
    }

    /// v1 posting from `IntakeKind`. `Keep` posts nothing.
    pub fn for_intake_kind(
        kind: IntakeKind,
        tenant_id: TenantId,
        payment_id: PaymentId,
        amount: Money,
    ) -> Result<Option<Self>, JournalError> {
        let pair = match kind {
            IntakeKind::Keep => return Ok(None),
            IntakeKind::Take => (Account::Cash, Account::Revenue),
            IntakeKind::ReturnLate | IntakeKind::ReturnOverCapacity => {
                (Account::Cash, Account::RefundsPayable)
            }
        };
        Self::two_line(tenant_id, payment_id, pair.0, pair.1, amount).map(Some)
    }

    pub fn merchant_refund_settled(
        tenant_id: TenantId,
        payment_id: PaymentId,
        amount: Money,
    ) -> Result<Self, JournalError> {
        Self::two_line(
            tenant_id,
            payment_id,
            Account::Revenue,
            Account::Cash,
            amount,
        )
    }

    pub fn late_refund_settled(
        tenant_id: TenantId,
        payment_id: PaymentId,
        amount: Money,
    ) -> Result<Self, JournalError> {
        Self::two_line(
            tenant_id,
            payment_id,
            Account::RefundsPayable,
            Account::Cash,
            amount,
        )
    }

    fn two_line(
        tenant_id: TenantId,
        payment_id: PaymentId,
        debit: Account,
        credit: Account,
        amount: Money,
    ) -> Result<Self, JournalError> {
        Self::try_new(
            tenant_id,
            payment_id,
            vec![
                JournalLine {
                    account: debit,
                    dc: Dc::Debit,
                    amount,
                },
                JournalLine {
                    account: credit,
                    dc: Dc::Credit,
                    amount,
                },
            ],
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::PaymentId;
    use crate::money::{Currency, Money};

    fn amt() -> Money {
        Money::from_quoted_str("10.00", Currency::MYR).unwrap()
    }

    #[test]
    fn take_balances() {
        let e = JournalEntry::for_intake_kind(
            IntakeKind::Take,
            TenantId::new("t1"),
            PaymentId::from_u128(1),
            amt(),
        )
        .unwrap()
        .unwrap();
        assert_eq!(e.lines().len(), 2);
        assert_eq!(e.lines()[0].account, Account::Cash);
        assert_eq!(e.lines()[1].account, Account::Revenue);
    }

    #[test]
    fn empty_is_err() {
        let err = JournalEntry::try_new(TenantId::new("t1"), PaymentId::from_u128(1), vec![])
            .unwrap_err();
        assert_eq!(err, JournalError::Empty);
    }

    #[test]
    fn unbalanced_is_err() {
        let a = amt();
        let err = JournalEntry::try_new(
            TenantId::new("t1"),
            PaymentId::from_u128(1),
            vec![JournalLine {
                account: Account::Cash,
                dc: Dc::Debit,
                amount: a,
            }],
        )
        .unwrap_err();
        assert_eq!(err, JournalError::Unbalanced);
    }
}
