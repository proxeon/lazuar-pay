import type { components } from "../../../lib/api-client";

export type TransactionLogDto = components["schemas"]["Commerce.TransactionLogDto"];

export const REFUNDABLE_STATUSES = new Set(["CONFIRMED", "PARTIALLY_REFUNDED", "REFUND_FAILED"]);

export function remainingAmount(tx: TransactionLogDto): number {
  if (typeof tx.remaining_amount === "number") return Math.max(0, tx.remaining_amount);
  return Math.max(0, tx.amount - (tx.refunded_amount ?? 0));
}

const OFFLINE_RECORDED_BY = new Set(["BANK_TRANSFER", "CASH", "COMPED", "MANUAL", "MANUAL_OFFLINE"]);

export function canRefund(tx: TransactionLogDto): boolean {
  const recordedBy = (tx.recorded_by_name || "").toUpperCase();
  if (OFFLINE_RECORDED_BY.has(recordedBy)) return false;
  if (tx.supports_api_refund === false) return false;
  return remainingAmount(tx) > 0 && REFUNDABLE_STATUSES.has(tx.status);
}

export function statusBadgeClass(status: string): string {
  switch (status) {
    case "CONFIRMED":
      return "bg-emerald-50 text-emerald-700 border-emerald-200";
    case "REFUND_PENDING":
      return "bg-blue-50 text-blue-700 border-blue-200";
    case "PARTIALLY_REFUNDED":
      return "bg-amber-50 text-amber-700 border-amber-200";
    case "REFUNDED":
      return "bg-amber-50 text-amber-700 border-amber-200";
    case "REFUND_FAILED":
      return "bg-rose-50 text-rose-700 border-rose-200";
    default:
      return "bg-zinc-50 text-zinc-700 border-zinc-200";
  }
}

export function statusLabel(status: string, refunded?: number): string {
  switch (status) {
    case "REFUND_PENDING":
      return "Pending refund";
    case "PARTIALLY_REFUNDED":
      return typeof refunded === "number" ? `Partial · RM ${refunded.toFixed(2)} back` : "Partially refunded";
    case "REFUND_FAILED":
      return "Refund failed";
    default:
      return status;
  }
}
