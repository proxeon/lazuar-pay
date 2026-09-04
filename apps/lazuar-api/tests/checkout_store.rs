//! CheckoutStore port tests — idempotency, conflict, and the real-Postgres
//! race replay (G4: the C# suite's InMemory version of this test was weak;
//! this one races actual unique-violation paths on real Postgres).

mod support;

use lazuar_api::domain::checkout_store::{self, CreateError, NewCheckout};
use rust_decimal::Decimal;
use std::str::FromStr;
use support::TestApp;
use uuid::Uuid;

fn new_checkout(org: &str, amount: &str) -> NewCheckout {
    NewCheckout {
        id: Uuid::new_v4(),
        org_id: org.to_string(),
        provider: Some("test".into()),
        product_id: Some("prod_1".into()),
        payment_link_id: None,
        slot_key: None,
        amount: Decimal::from_str(amount).unwrap(),
        currency: "MYR".into(),
        status: "open".into(),
        interval: None,
        success_url: Some("https://app.test/success".into()),
        cancel_url: Some("https://app.test/cancel".into()),
        public_token: None,
    }
}

#[test]
fn same_key_same_body_replays_existing_checkout() {
    let app = TestApp::spawn();
    let mut db = app.db();

    let session = new_checkout("org_1", "9.90");
    let first = checkout_store::create(&mut db, &session, Some("key-1")).unwrap();
    let replay = checkout_store::create(&mut db, &session, Some("key-1")).unwrap();

    assert_eq!(first.id, replay.id);
    assert_eq!(replay.public_token, first.public_token);
    assert_eq!(replay.status, "open");
}

#[test]
fn same_key_different_body_is_conflict() {
    let app = TestApp::spawn();
    let mut db = app.db();

    let session = new_checkout("org_1", "9.90");
    checkout_store::create(&mut db, &session, Some("key-1")).unwrap();

    let mut different = session.clone();
    different.amount = Decimal::from_str("19.90").unwrap();
    match checkout_store::create(&mut db, &different, Some("key-1")) {
        Err(CreateError::Conflict) => {}
        other => panic!("expected Conflict, got {other:?}"),
    }
}

#[test]
fn different_orgs_can_reuse_the_same_key() {
    let app = TestApp::spawn();
    let mut db = app.db();

    let a = checkout_store::create(&mut db, &new_checkout("org_a", "9.90"), Some("key-1")).unwrap();
    let b = checkout_store::create(&mut db, &new_checkout("org_b", "9.90"), Some("key-1")).unwrap();
    assert_ne!(a.id, b.id);
}

#[test]
fn lost_insert_race_replays_the_winner_on_real_postgres() {
    let app = TestApp::spawn();
    let pool = app.pool.clone();
    let key = "race-key-1";

    // Two real concurrent creates with the same key: the unique PK on
    // (OrgId, Key) makes exactly one the insert winner; the loser must replay
    // the winner's checkout through the unique-violation path — no 500, no
    // duplicate row.
    let mut handles = Vec::new();
    for _ in 0..2 {
        let pool = pool.clone();
        handles.push(std::thread::spawn(move || {
            let mut conn = pool.get().unwrap();
            checkout_store::create(&mut conn, &new_checkout("org_race", "9.90"), Some(key))
                .expect("both racers must succeed (one by replay)")
        }));
    }
    let results: Vec<checkout_store::CheckoutSession> =
        handles.into_iter().map(|h| h.join().unwrap()).collect();

    assert_eq!(results[0].id, results[1].id, "both callers must see the winner's checkout");

    let mut db = app.db();
    let count: i64 = db
        .query_one("SELECT count(*) FROM public.checkouts WHERE \"OrgId\" = 'org_race'", &[])
        .unwrap()
        .get(0);
    assert_eq!(count, 1, "exactly one checkout row must exist");
}
