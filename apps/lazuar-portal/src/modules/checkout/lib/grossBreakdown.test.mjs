import assert from "node:assert/strict";
import { describe, it } from "node:test";
import {
  computeSstTax,
  grossBreakdown,
  productSignalsSst,
  roundMoney,
} from "./grossBreakdown.ts";

describe("computeSstTax", () => {
  it("type 06 never taxes even with a rate and SST id", () => {
    const { taxType, taxAmount } = computeSstTax("06", 8, 100, true);
    assert.equal(taxType, "06");
    assert.equal(taxAmount, 0);
  });

  it("type 02 with SST id computes exclusive tax", () => {
    const { taxType, taxAmount } = computeSstTax("02", 8, 100, true);
    assert.equal(taxType, "02");
    assert.equal(taxAmount, 8);
  });

  it("type 02 without SST id coerces to 06", () => {
    const { taxType, taxAmount } = computeSstTax("02", 8, 100, false);
    assert.equal(taxType, "06");
    assert.equal(taxAmount, 0);
  });

  it("rounds half away from zero like C# money", () => {
    assert.equal(roundMoney(1.225), 1.23);
    assert.equal(computeSstTax("02", 8, 12.5, true).taxAmount, 1);
  });
});

describe("grossBreakdown", () => {
  it("shows tax line + gross for hop-1 type 02 / 8% / qty 3", () => {
    const b = grossBreakdown(100, 3, "02", 8);
    assert.equal(b.unitNet, 100);
    assert.equal(b.unitTax, 8);
    assert.equal(b.unitGross, 108);
    assert.equal(b.lineTax, 24);
    assert.equal(b.gross, 324);
    assert.equal(b.taxType, "02");
  });

  it("omits tax when the product is type 06", () => {
    const b = grossBreakdown(100, 1, "06", 8);
    assert.equal(b.lineTax, 0);
    assert.equal(b.gross, 100);
    assert.equal(b.taxType, "06");
  });

  it("taxes the discounted unit, not the catalog unit", () => {
    const b = grossBreakdown(70, 1, "02", 8);
    assert.equal(b.unitTax, 5.6);
    assert.equal(b.gross, 75.6);
  });

  it("does not tax a trial-today zero net", () => {
    const today = grossBreakdown(0, 1, "02", 8);
    const then = grossBreakdown(100, 1, "02", 8);
    assert.equal(today.gross, 0);
    assert.equal(then.gross, 108);
  });
});

describe("productSignalsSst", () => {
  it("is the hop-1 stand-in for merchantHasSst", () => {
    assert.equal(productSignalsSst("02", 8), true);
    assert.equal(productSignalsSst("02", 0), false);
    assert.equal(productSignalsSst("06", 8), false);
  });
});
