export function interpolate(
  template: string,
  vars?: Record<string, string | number>,
): string {
  if (!vars) return template;
  return template.replace(/\{(\w+)\}/g, (whole, key: string) =>
    vars[key] !== undefined ? String(vars[key]) : whole,
  );
}

export function formatMoney(
  locale: "en" | "ms",
  currency: string,
  amount: number,
): string {
  return new Intl.NumberFormat(locale === "ms" ? "ms-MY" : "en-MY", {
    style: "currency",
    currency: currency || "MYR",
  }).format(amount);
}

export function currencySymbol(locale: "en" | "ms", currency: string): string {
  const parts = new Intl.NumberFormat(locale === "ms" ? "ms-MY" : "en-MY", {
    style: "currency",
    currency: currency || "MYR",
  }).formatToParts(0);
  return parts.find((part) => part.type === "currency")?.value ?? "RM";
}
