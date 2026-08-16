import type { MessageKey } from "./messages";

const PROMO_NOT_APPLICABLE = [
  "cannot be applied",
  "not valid for the selected product",
  "archived",
  "expired",
  "maximum usage",
  "minimum original price",
] as const;

export type CheckoutErrorClass = MessageKey | "passthrough";

export function classifyCheckoutError(
  detail: string | null | undefined,
): CheckoutErrorClass {
  const lower = (detail ?? "").toLowerCase();
  if (!lower) return "error.generic";
  if (lower.includes("invalid promo code")) return "error.invalidPromo";
  if (PROMO_NOT_APPLICABLE.some((needle) => lower.includes(needle))) {
    return "error.promoNotApplicable";
  }
  if (
    lower.includes("payment gateway") ||
    lower.includes("not configured an active email provider")
  ) {
    return "error.gatewayDown";
  }
  if (lower.includes("confirmation link was missing")) return "error.missingConfirmUrl";
  if (lower.includes("checkout submission failed")) return "error.submitFailed";
  if (lower.includes("status check failed")) return "error.statusFailed";
  if (
    lower.includes("requires a phone number") ||
    lower.includes("tax id") ||
    lower.includes("billing address")
  ) {
    return "passthrough";
  }
  return "error.generic";
}

export function localizeCheckoutError(
  detail: string | null | undefined,
  translate: (key: MessageKey) => string,
): string {
  const classified = classifyCheckoutError(detail);
  if (classified === "passthrough") {
    return detail?.trim() ? detail : translate("error.generic");
  }
  return translate(classified);
}
