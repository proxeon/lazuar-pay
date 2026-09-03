//! Issue 002 (issues/001): CAS checkout status transitions on real Postgres.
//! The expiry sweep must not overwrite a committed `paid` (G4: this is one of
//! the race tests that must pass on real Postgres, not an InMemory stand-in).

mod support;

use lazuar_api::domain::transitions::{try_leave_open, try_transition};
use support::TestApp;
use uuid::Uuid;

#[test]
fn sweep_cannot_overwrite_paid() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let id = Uuid::new_v4();
    support::insert_checkout(&mut db, id, "open");

    // Fulfiller wins the race: open → paid commits.
    assert!(try_leave_open(&mut db, id, "paid").unwrap());

    // The expiry sweep arrives late: 0 affected rows — "another writer moved it".
    assert!(!try_leave_open(&mut db, id, "expired").unwrap());
    assert_eq!(support::checkout_status(&mut db, id), "paid");
}

#[test]
fn fulfiller_cannot_stamp_paid_over_committed_expired() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let id = Uuid::new_v4();
    support::insert_checkout(&mut db, id, "open");

    // Sweep wins this time: open → expired commits.
    assert!(try_leave_open(&mut db, id, "expired").unwrap());
    assert!(!try_leave_open(&mut db, id, "paid").unwrap());
    assert_eq!(support::checkout_status(&mut db, id), "expired");
}

#[test]
fn failed_to_open_reopens_for_retry_issue_004() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let id = Uuid::new_v4();
    support::insert_checkout(&mut db, id, "failed");

    // Issue 004: the failed→open direction re-opens a past_due checkout for retry.
    assert!(try_transition(&mut db, id, "failed", "open").unwrap());
    assert_eq!(support::checkout_status(&mut db, id), "open");
}
