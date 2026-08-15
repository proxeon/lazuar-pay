import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

const HIDDEN_INTERNAL_TARGETS = ["internal:community", "internal:vault"];

export function filterHiddenFulfillmentTargets(targets: string[] | undefined): string[] {
  return (targets ?? []).filter(t => !HIDDEN_INTERNAL_TARGETS.some(h => t.toLowerCase().startsWith(h)));
}

/** Stripe/CHIP can vault; Billplz and unknown rails are reminder-only. Prefer API flag when present. */
export function gatewaySupportsOffSession(
  gatewayName?: string | null,
  supportsOffSession?: boolean | null
): boolean {
  if (typeof supportsOffSession === "boolean") return supportsOffSession;
  const g = (gatewayName ?? "").trim().toUpperCase();
  return g === "STRIPE" || g === "CHIP";
}
