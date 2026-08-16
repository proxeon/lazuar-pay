import { cookies, headers } from "next/headers";
import {
  firstSearchValue,
  LOCALE_COOKIE,
  queryFromRequestUrl,
  resolveCheckoutLocale,
  type Locale,
} from "./locales";

export async function getCheckoutLocale(search?: {
  lang?: string | string[];
  locale?: string | string[];
}): Promise<Locale> {
  const cookieStore = await cookies();
  const headerStore = await headers();
  const fromHeaders = queryFromRequestHeaders(headerStore);

  return resolveCheckoutLocale({
    lang: firstSearchValue(search?.lang) ?? fromHeaders.lang,
    locale: firstSearchValue(search?.locale) ?? fromHeaders.locale,
    cookie: cookieStore.get(LOCALE_COOKIE)?.value,
    acceptLanguage: headerStore.get("accept-language"),
  });
}

function queryFromRequestHeaders(headerStore: Headers): {
  lang?: string;
  locale?: string;
} {
  const invokeQuery = headerStore.get("x-invoke-query");
  const invokePath = headerStore.get("x-invoke-path");
  const candidates = [
    headerStore.get("x-url"),
    headerStore.get("x-next-url"),
    headerStore.get("next-url"),
    invokePath && invokeQuery
      ? `${invokePath}?${safeDecode(invokeQuery)}`
      : invokeQuery
        ? `?${safeDecode(invokeQuery)}`
        : null,
    headerStore.get("x-matched-path"),
  ];

  for (const raw of candidates) {
    const parsed = queryFromRequestUrl(raw);
    if (parsed.lang || parsed.locale) return parsed;
  }
  return {};
}

function safeDecode(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}
