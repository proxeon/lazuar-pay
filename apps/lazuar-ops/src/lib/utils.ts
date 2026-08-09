import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

const HIDDEN_INTERNAL_TARGETS = ["internal:community", "internal:vault"];

export function filterHiddenFulfillmentTargets(targets: string[] | undefined): string[] {
  return (targets ?? []).filter(t => !HIDDEN_INTERNAL_TARGETS.some(h => t.toLowerCase().startsWith(h)));
}
