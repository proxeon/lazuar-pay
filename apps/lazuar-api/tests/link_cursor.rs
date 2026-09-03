//! Issues 013/015 — org-scoped cursor pagination for merchant link lists.

mod support;

use lazuar_api::links::list::list;
use rust_decimal::Decimal;
use std::str::FromStr;
use support::TestApp;

fn insert_link(db: &mut postgres::Client, org: &str, token: &str) -> String {
    let id = uuid::Uuid::new_v4().to_string();
    db.execute(
        "INSERT INTO public.payment_links \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"Amount\",\"Currency\",\"MaxPayers\",\"CreatedAt\") \
         VALUES ($1,$2,$3,'test',$4,$5,NULL,$6)",
        &[
            &id,
            &org,
            &token,
            &Decimal::from_str("9.90").unwrap(),
            &"MYR",
            &chrono::Utc::now(),
        ],
    )
    .unwrap();
    id
}

#[test]
fn foreign_org_cursor_id_is_treated_as_unknown() {
    let app = TestApp::spawn();
    let mut db = app.db();

    let mine_a = insert_link(&mut db, "org_mine", "tok_mine_a");
    let mine_b = insert_link(&mut db, "org_mine", "tok_mine_b");
    // Another org's link — its id must never act as a cursor for org_mine.
    let foreign = insert_link(&mut db, "org_foreign", "tok_foreign");

    // A foreign-org cursor id resolves to nothing → page restarts from the top
    // (no rows leaked, no error, no oracle).
    let page = list(&mut db, "org_mine", Some(50), Some(&foreign)).unwrap();
    let ids: Vec<&str> = page.items.iter().map(|l| l.id.as_str()).collect();
    assert_eq!(ids.len(), 2, "foreign cursor treated as unknown: full org page returned");
    assert!(ids.contains(&mine_a.as_str()));
    assert!(ids.contains(&mine_b.as_str()));
    assert!(page.next_cursor.is_none());
}

#[test]
fn cursor_pagination_follows_to_the_end_without_truncation_or_overlap() {
    let app = TestApp::spawn();
    let mut db = app.db();
    for i in 0..7 {
        let _ = insert_link(&mut db, "org_1", &format!("tok_{i}"));
        std::thread::sleep(std::time::Duration::from_millis(5)); // distinct CreatedAt
    }

    let mut seen: Vec<String> = Vec::new();
    let mut after: Option<String> = None;
    loop {
        let page = list(&mut db, "org_1", Some(3), after.as_deref()).unwrap();
        let page_ids: Vec<String> = page.items.iter().map(|l| l.id.clone()).collect();
        for id in &page_ids {
            assert!(!seen.contains(id), "no row may appear on two pages");
        }
        seen.extend(page_ids);
        match page.next_cursor {
            Some(next) => after = Some(next),
            None => break,
        }
    }
    assert_eq!(seen.len(), 7, "cursor must walk every row exactly once");
}

#[test]
fn list_never_returns_another_orgs_rows() {
    let app = TestApp::spawn();
    let mut db = app.db();
    for i in 0..3 {
        let _ = insert_link(&mut db, "org_mine", &format!("tok_mine_{i}"));
    }
    for i in 0..3 {
        let _ = insert_link(&mut db, "org_theirs", &format!("tok_theirs_{i}"));
    }

    let page = list(&mut db, "org_mine", Some(50), None).unwrap();
    assert_eq!(page.items.len(), 3, "org_mine sees only org_mine");
    assert!(page.items.iter().all(|l| l.org_id == "org_mine"));

    let theirs = list(&mut db, "org_theirs", Some(50), None).unwrap();
    assert_eq!(theirs.items.len(), 3);
    assert!(theirs.items.iter().all(|l| l.org_id == "org_theirs"));
}
