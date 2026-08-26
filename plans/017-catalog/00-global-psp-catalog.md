# Global PSP catalog (awareness)

This is an awareness catalog, not a build list. Pay today only wraps hosted links (Stripe, CHIP, Billplz, Xendit, Razorpay, plus local Test). Most names below are a different job: they acquire, they are a wallet, they hold funds, or they are the card network itself.

There is no closed global list of “every PSP.” There are thousands of licensed acquirers and bank processors. What follows is the set a payments product should actually know, grouped by job and region, so you can pick later.

---

## How to read this

| Kind | What they are | Fit for Pay later |
|---|---|---|
| Hosted-link PSP | Merchant has keys. Buyer pays on their page. Webhook says paid. | Current wrap-rail shape |
| Full-stack acquirer | One API for cards + local methods + often POS | Same shape if they have hosted checkout |
| Local method aggregator | FPX, DuitNow, GCash, Pix, UPI… | Wrap if they expose a hosted URL |
| Escrow | Hold money until inspect/accept. No instant “paid” | Different state machine |
| Marketplace / split | Hold + split to sellers | Connect/Mangopay, not a pay link |
| Merchant of Record | They are the seller of record, tax, refunds | Competes with Pay, not a rail |
| Orchestrator | Routes across many PSPs | Above Pay, not a rail |
| Network / wallet | Visa, Alipay, GrabPay | Arrive through a PSP, do not wrap them |

Pay’s current contract is still: one rail per link, BYOK, hosted URL, webhook fulfills, Official Receipt. Escrow.com does not fit that contract as-is. It is still worth knowing, and worth a later product slice.

---

## Already in Pay

| Name | Home | Role |
|---|---|---|
| Stripe | US / global | Hosted Checkout. Cards. Weak SEA wallets. |
| CHIP | Malaysia | Hosted page, FPX/wallets on their brand. |
| Billplz | Malaysia | Hosted bill / reminder. No auto-debit. |
| Xendit | SEA (ID/PH/MY…) | Hosted invoice / payment link. Regional API. |
| Razorpay | India | Payment link, not e-mandate. |
| Test | Local only | No secrets. Dev dogfood. |

Not in Pay, but already named in your own SEA research as displacement: HitPay, Midtrans, PayMongo, 2C2P, Fiuu, iPay88, Airwallex.

---

## Escrow.com — yes, it is an advantage, as a different product

Escrow.com is not “another Stripe.” It is a licensed US escrow agent (Fidelity National Financial lineage, California-regulated, KYC/AML on both parties). Public claims: ~US$5B+ protected, fees from ~0.7–0.89% at volume, no chargebacks, inspection period before release.

Escrow Pay is a hosted wizard (one API call + `return_url`). Cards and PayPal are funded on their site, not via API. Wires can be referenced via API. Webhooks track: created → funded → shipped → received → accepted / rejected.

That maps to a wrap-rail only for the “go pay” hop. The rest of the life cycle is new:

`open → funded → in_transit → inspect → released | returned | disputed`

A normal pay link is `open → paid`. Mixing those on one checkout row would lie.

### Where it wins for Lazuar

- High-ticket B2B, domains, vehicles, machinery, IPv4, classifieds, cross-border “I don’t trust this seller.”
- Marketplace / broker (they support a broker party and revenue share at high volume).
- Sales story no FPX link has: buyer inspects, seller cannot be charged-back.
- Recommended by GoDaddy / Uniregistry / Shopify Exchange for domain-class goods — that is a real category, not a toy.

### Where it does not replace CHIP/Billplz

- Not a MYR FPX/DuitNow rail. USD-centric, US KYC, inspection days.
- RM 10 salon deposit is the wrong ticket.
- Cards/PayPal cannot be API-funded; buyer must hit their hosted pay URL.
- You would mint an Official Receipt on release, not on “funded.” Funded is not paid-to-seller.

### Escrow siblings (same job family)

| Name | Notes |
|---|---|
| Escrow.com | High-value goods/domains/vehicles. Hosted Escrow Pay. Strongest brand. |
| Tazapay | Singapore. B2B cross-border + digital escrow. MAS MPI. Closer to ASEAN trade than Escrow.com. Series B 2026. |
| Mangopay | EU marketplace wallets + escrow-like hold. Licensed. |
| Trustap | C2C / used-goods escrow-style, Shopify/Woo plugins. |
| Shieldpay | Enterprise / professional escrow (UK). |
| Stripe Connect | Not licensed escrow. Hold + transfer. Chargebacks still exist. |
| Payoneer / PayPal Goods & Services | Hold-ish, not a real escrow agent. |

If you later pick one escrow: Escrow.com for USD high-ticket goods, Tazapay for ASEAN B2B trade. Do not treat Stripe Connect as “we have escrow.”

---

## Global catalog (awareness)

Status key: in Pay · SEA-relevant · hosted-link candidate · different job

### Global full-stack / enterprise acquirers

| Name | HQ / bias | Notes |
|---|---|---|
| Stripe | US | In Pay. Developer default. |
| Adyen | NL | Enterprise unified commerce. Interchange++. Uber/McD scale. |
| Checkout.com | UK | Digital-native, auth-rate, 150+ currencies. |
| PayPal + Braintree | US | Wallet brand + gateway. Buyer trust, ugly disputes. |
| Square / Block | US | SMB POS + online. Afterpay in the family. |
| Worldpay (now Global Payments) | US/UK | 2026 GPN deal. Retail/omnichannel giant. |
| Fiserv (Clover, etc.) | US | Bank processor / POS. |
| FIS | US | Issuer + merchant infrastructure. |
| Worldline (+ Ingenico) | EU | EU acquiring + terminals. |
| Nexi | EU | South/Central Europe acquiring. |
| Nuvei | CA | Global + gaming-heavy. |
| Rapyd | IL/global | Local methods as a platform. Collect + disburse. |
| dLocal | UY | Emerging-market collector (LATAM, Africa, Asia). |
| EBANX | BR | LATAM specialist. |
| PayU | NL/Prosus | Multi-region (IN, LATAM, CEE, Africa). |
| Airwallex | AU/HK | Cross-border accounts + collecting. SEA-relevant. |
| Nium | SG | Payouts + cards, less “pay link.” |
| Wise | UK | Payouts / accounts, not merchant acquiring. |
| Flywire | US | Education, travel, high-value receivables. |

### Malaysia (home market)

| Name | Notes |
|---|---|
| CHIP | In Pay. |
| Billplz | In Pay. |
| Xendit (via Payex) | In Pay. BNM path is not “Xendit the bank.” |
| HitPay | SEA-relevant. Link + invoice + POS. Displacement, not a wrap unless they offer BYOK (they usually don’t — they are the acquirer). |
| Fiuu (ex Razer/MOLPay) | MY/SEA aggregator. Common in plugins. |
| iPay88 | Legacy MY/SEA aggregator, banks/e-wallets. |
| SenangPay | MY SME. |
| GHL / eGHL | Terminals + e-commerce. |
| Revenue Monster | QR / loyalty / wallets. |
| Payex | BNM merchant acquirer; Xendit MY pipe. |
| Kiple / others | Smaller wallets/QR. |
| DuitNow / FPX | Rails, not PSPs. Arrive through the names above. |

### Rest of ASEAN

| Country | Names to know |
|---|---|
| Singapore | HitPay, 2C2P (Antom), Stripe, Adyen SG, Airwallex, Aspire, ipaymy, Qashier, Xendit. PayNow is the rail. |
| Indonesia | Midtrans (GoTo), Xendit, DOKU, Faspay, DANA, OVO, GoPay, ShopeePay, QRIS (rail). |
| Philippines | PayMongo, Xendit, Dragonpay, Paynamics, GCash, Maya, QR Ph. |
| Thailand | Omise, 2C2P, GBPrimePay, PromptPay, TrueMoney, Rabbit LINE Pay. |
| Vietnam | VNPay, MoMo, ZaloPay, Payoo, 2C2P. |
| Cambodia/MM/LA | Wing, Pi Pay, ACLEDA, 2C2P coverage — thin. |

2C2P / Antom is the pan-Asia enterprise collector (airlines, OTC 600k+ points). Ant International / Alipay+ is how WeChat/Alipay land in SEA.

### India

Razorpay (in Pay), Cashfree, PayU, CCAvenue, Paytm, PhonePe, Instamojo, Juspay (orchestrator), BillDesk, Pine Labs. UPI is the rail.

### Greater China / North Asia

| Market | Names |
|---|---|
| China | Alipay, WeChat Pay, UnionPay, PingPong, WorldFirst (collect). You do not wrap Alipay; a PSP brings Alipay+. |
| Japan | KOMOJU, SB Payment Service, GMO, PayPay, Stripe JP, Paidy. |
| Korea | Toss Payments, NHN KCP, Nicepay, KakaoPay, Naver Pay. |
| Taiwan | NewebPay, TapPay, ECPay, Line Pay. |
| Hong Kong | Stripe, Airwallex, PayMe, AlipayHK. |

### Australia / NZ

Stripe, Square, Afterpay, Windcave, eWAY, Pin Payments, Fat Zebra, Tyro (POS), POLi / PayTo (bank).

### Europe / UK

Adyen, Stripe, Checkout.com, Mollie (NL/EU SMB), GoCardless (ACH/Direct Debit), Klarna (BNPL), SumUp, Payoneer, Trustly (open banking), Worldline, Nexi, Stripe Tax companions. UK extras: Worldpay, Opayo (Sage Pay), Take Payments, Checkout.com.

Open banking / A2A: GoCardless, TrueLayer, Token, Yapily — not hosted card pages.

### United States / Canada

Stripe, PayPal/Braintree, Square, Adyen, Authorize.net, Worldpay/GPN, Fiserv/Clover, Helcim, Stax, PaySimple, Affirm (BNPL), Apple Pay / Google Pay (wallets on top of an acquirer). Canada: Moneris, Global Payments Canada, Stripe, Square.

### Latin America

dLocal, EBANX, Mercado Pago, PagSeguro / PagBank, PayU Latam, Clip (MX), Kushki, Openpay, Getnet, Rede, Stone. Pix (Brazil) is the rail.

### Africa / Middle East

| Region | Names |
|---|---|
| Africa | Paystack (Stripe-owned), Flutterwave, DPO Group, Pesapal, Cellulant, M-Pesa (Safaricom rail), Chipper. |
| Middle East | Checkout.com, PayTabs, HyperPay, Tap Payments, PayFort (Amazon), Network International, Telr. |

### Cross-border collectors / “one API, many countries”

dLocal, EBANX, Rapyd, PayU, 2C2P, Xendit, Airwallex, Nium, Worldpay, Adyen, Checkout.com, Tazapay.

---

## Adjacent (not PSPs, still catalog)

Orchestrators (sit above rails): Primer, Spreedly, Gr4vy, ProcessOut, Juspay, Braintree + whatever. Pay should not become one of these.

Merchant of Record (they are the merchant): Paddle, FastSpring, Lemon Squeezy, Dodo Payments, Polar. Competitor to “we host checkout,” not a BYOK rail.

BNPL: Klarna, Afterpay/Clearpay, Affirm, Atome, Grab PayLater, SPayLater. Usually via a PSP, not a first-party rail.

Crypto: BitPay, Coinbase Commerce, NOWPayments, Triple-A. Separate risk/compliance box.

Payouts / accounts: Wise, Payoneer, Nium, Thunes, Airwallex, Stripe Treasury. Opposite direction of a pay link.

Card networks: Visa, Mastercard, UnionPay, Amex. Not wrap targets.

---

## Later: how to choose, without implementing now

Keep three buckets. Do not flatten them into `PayProviders.All`.

1. **More hosted-link rails** (same code shape as today)  
   HitPay (if they ever give BYOK; unlikely), Midtrans, PayMongo, Fiuu, iPay88, Omise, Mollie, PayPal, Adyen, Checkout.com, 2C2P.  
   Pick by merchant demand in MY, not by global fame.

2. **Escrow / trade** (new capability)  
   Escrow.com (USD goods/domains) and Tazapay (ASEAN B2B). This is the interesting expansion: high-ticket trust, not another FPX clone. Needs funded/inspect/release, receipt on release.

3. **Refuse as rails**  
   Alipay/WeChat as direct, Visa as direct, MoR platforms, orchestrators, M-Pesa as direct, DuitNow as direct.

Escrow.com is worth a later design, not a sixth wrap-rail this week. The advantage is real for classifieds, domains, machinery, and cross-border “I will only pay if I can inspect.” It is a weak fit for the current RM pay-link. If you want one escrow name that is closer to MY/SG trade than California domain escrow, put Tazapay next to it on the same shortlist.

When you want to choose what to implement, we can score bucket 1 vs bucket 2 against actual merchant demand (MY SMBs vs high-ticket B2B) and keep the hosted-link switch from growing into a factory.
