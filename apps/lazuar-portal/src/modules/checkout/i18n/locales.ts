export const LOCALES = ["en", "ms"] as const;
export type Locale = (typeof LOCALES)[number];

export const DEFAULT_LOCALE: Locale = "en";
export const LOCALE_COOKIE = "lazuar_locale";
export const LOCALE_COOKIE_MAX_AGE = 60 * 60 * 24 * 365;

export function parseLocale(value: string | null | undefined): Locale | null {
  if (!value) return null;
  const primary = value.trim().split(/[-_]/)[0]?.toLowerCase();
  if (primary === "en") return "en";
  if (primary === "ms") return "ms";
  return null;
}

export function intlTag(locale: Locale): "en-MY" | "ms-MY" {
  return locale === "ms" ? "ms-MY" : "en-MY";
}

export function firstSearchValue(
  value: string | string[] | undefined | null,
): string | undefined {
  if (Array.isArray(value)) return value[0];
  return value ?? undefined;
}

export function parseAcceptLanguage(header: string | null | undefined): Locale | null {
  if (!header) return null;
  for (const part of header.split(",")) {
    const tag = part.split(";")[0]?.trim();
    if (parseLocale(tag) === "ms") return "ms";
  }
  return null;
}

export function resolveCheckoutLocale(input: {
  lang?: string | null;
  locale?: string | null;
  cookie?: string | null;
  acceptLanguage?: string | null;
}): Locale {
  return (
    parseLocale(input.lang) ??
    parseLocale(input.locale) ??
    parseLocale(input.cookie) ??
    parseAcceptLanguage(input.acceptLanguage) ??
    DEFAULT_LOCALE
  );
}

export function localeCookieString(locale: Locale): string {
  return `${LOCALE_COOKIE}=${locale}; Path=/; Max-Age=${LOCALE_COOKIE_MAX_AGE}; SameSite=Lax`;
}

export function queryFromRequestUrl(raw: string | null | undefined): {
  lang?: string;
  locale?: string;
} {
  if (!raw) return {};
  try {
    const url = raw.includes("://") ? new URL(raw) : new URL(raw, "http://local.invalid");
    return {
      lang: url.searchParams.get("lang") ?? undefined,
      locale: url.searchParams.get("locale") ?? undefined,
    };
  } catch {
    return {};
  }
}
