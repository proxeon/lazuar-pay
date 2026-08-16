import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { classifyCheckoutError, localizeCheckoutError } from "./errors.ts";
import { currencySymbol, formatMoney, interpolate } from "./format.ts";
import {
  parseAcceptLanguage,
  parseLocale,
  resolveCheckoutLocale,
} from "./locales.ts";
import { en, ms } from "./messages.ts";

describe("parseLocale", () => {
  it("maps en and regional English to en", () => {
    assert.equal(parseLocale("en"), "en");
    assert.equal(parseLocale("en-GB"), "en");
    assert.equal(parseLocale("en-US"), "en");
    assert.equal(parseLocale("en-MY"), "en");
  });

  it("maps ms and ms-MY to ms", () => {
    assert.equal(parseLocale("ms"), "ms");
    assert.equal(parseLocale("ms-MY"), "ms");
    assert.equal(parseLocale("ms-BN"), "ms");
    assert.equal(parseLocale("MS"), "ms");
  });

  it("does not treat Bahasa Indonesia as BM", () => {
    assert.equal(parseLocale("id"), null);
    assert.equal(parseLocale("id-ID"), null);
  });

  it("rejects unknown tags", () => {
    assert.equal(parseLocale("zh"), null);
    assert.equal(parseLocale(""), null);
    assert.equal(parseLocale(undefined), null);
  });
});

describe("resolveCheckoutLocale", () => {
  it("prefers ?lang= over cookie and Accept-Language", () => {
    assert.equal(
      resolveCheckoutLocale({
        lang: "ms",
        cookie: "en",
        acceptLanguage: "en-US",
      }),
      "ms",
    );
  });

  it("accepts ?locale= when lang is absent", () => {
    assert.equal(
      resolveCheckoutLocale({
        locale: "ms-MY",
        cookie: "en",
        acceptLanguage: "en-US",
      }),
      "ms",
    );
  });

  it("uses cookie when query is missing", () => {
    assert.equal(
      resolveCheckoutLocale({
        cookie: "ms",
        acceptLanguage: "en-US",
      }),
      "ms",
    );
  });

  it("uses Accept-Language ms even when it is not the first tag", () => {
    assert.equal(
      resolveCheckoutLocale({
        acceptLanguage: "en-US,en;q=0.9,ms-MY;q=0.8",
      }),
      "ms",
    );
  });

  it("defaults to en, including for id-ID", () => {
    assert.equal(resolveCheckoutLocale({ acceptLanguage: "id-ID" }), "en");
    assert.equal(resolveCheckoutLocale({}), "en");
    assert.equal(parseAcceptLanguage("id-ID,id;q=0.9"), null);
  });
});

describe("messages", () => {
  it("has matching en and ms keys", () => {
    const enKeys = Object.keys(en).sort();
    const msKeys = Object.keys(ms).sort();
    assert.deepEqual(msKeys, enKeys);
    for (const key of enKeys) {
      assert.equal(typeof ms[key], "string");
      assert.ok(ms[key].length > 0, `${key} is empty in ms`);
    }
  });
});

describe("interpolate and money", () => {
  it("replaces named placeholders", () => {
    assert.equal(interpolate("Hello {name}", { name: "Aina" }), "Hello Aina");
    assert.equal(interpolate("© {year} Lazuar", { year: 2026 }), "© 2026 Lazuar");
    assert.equal(interpolate("keep {missing}"), "keep {missing}");
  });

  it("formats MYR as RM for en-MY and ms-MY", () => {
    const enMoney = formatMoney("en", "MYR", 50);
    const msMoney = formatMoney("ms", "MYR", 50);
    assert.match(enMoney, /RM/);
    assert.match(enMoney, /50/);
    assert.match(msMoney, /RM/);
    assert.match(msMoney, /50/);
    assert.equal(currencySymbol("en", "MYR"), "RM");
    assert.equal(currencySymbol("ms", "MYR"), "RM");
  });
});

describe("classifyCheckoutError", () => {
  it("maps promo, gateway, and generic details", () => {
    assert.equal(classifyCheckoutError("Invalid promo code."), "error.invalidPromo");
    assert.equal(
      classifyCheckoutError("This code cannot be applied."),
      "error.promoNotApplicable",
    );
    assert.equal(
      classifyCheckoutError("Coupon is expired"),
      "error.promoNotApplicable",
    );
    assert.equal(
      classifyCheckoutError("Payment gateway 'Billplz' is not configured for this workspace."),
      "error.gatewayDown",
    );
    assert.equal(
      classifyCheckoutError("Workspace has not configured an active email provider."),
      "error.gatewayDown",
    );
    assert.equal(
      classifyCheckoutError("Buyer requires a phone number"),
      "passthrough",
    );
    assert.equal(classifyCheckoutError("Some unknown C# detail"), "error.generic");
  });

  it("localizes mapped errors and does not dump unknown C#", () => {
    const translate = (key) => en[key];
    assert.equal(
      localizeCheckoutError("Invalid promo code.", translate),
      "Invalid promo code.",
    );
    assert.equal(
      localizeCheckoutError("Payment gateway 'CHIP' is disabled.", translate),
      en["error.gatewayDown"],
    );
    assert.equal(
      localizeCheckoutError("Totally unknown exception from handler", translate),
      en["error.generic"],
    );
  });
});
