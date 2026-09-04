mod support;

use lazuar_api::money::reconciliation;
use rust_decimal::Decimal;
use support::TestApp;

#[test]
fn stale_pending_refund_is_counted() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let old = chrono::Utc::now() - chrono::Duration::minutes(45);
    db.execute(
        "INSERT INTO public.refunds \
         (\"Id\",\"OrgId\",\"CheckoutId\",\"Amount\",\"Currency\",\"Status\",\"Provider\",\"Reason\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5,'pending',$6,$7,$8)",
        &[
            &"rf_stale",
            &"org_test",
            &"co_stale",
            &Decimal::new(990, 2),
            &"MYR",
            &"test",
            &"merchant",
            &old,
        ],
    )
    .expect("insert stale refund");
    let n = reconciliation::run_once(&mut db).unwrap();
    assert!(n >= 1);
}
