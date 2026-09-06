//! Display amounts as JSON numbers (09 lock 7). Never strings.

use domain::Money;
use rust_decimal::Decimal;
use serde::de::{self, Deserializer};
use serde::Deserialize;
use serde_json::{Number, Value};

pub fn money_number(m: Money) -> Value {
    let d = m
        .to_quoted_display()
        .unwrap_or_else(|_| Decimal::from(m.minor()));
    let s = d.normalize().to_string();
    Value::Number(s.parse::<Number>().unwrap_or_else(|_| Number::from(0)))
}

pub fn de_decimal<'de, D>(d: D) -> Result<Decimal, D::Error>
where
    D: Deserializer<'de>,
{
    let v = Value::deserialize(d)?;
    match v {
        Value::Number(n) => Decimal::from_str_exact(&n.to_string()).map_err(de::Error::custom),
        Value::String(s) => Decimal::from_str_exact(&s).map_err(de::Error::custom),
        _ => Err(de::Error::custom("amount must be a number")),
    }
}
