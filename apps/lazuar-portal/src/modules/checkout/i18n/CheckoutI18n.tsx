"use client";

import {
  createContext,
  Fragment,
  useCallback,
  useContext,
  useLayoutEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { usePathname, useRouter } from "next/navigation";
import { interpolate } from "./format";
import {
  localeCookieString,
  parseLocale,
  type Locale,
} from "./locales";
import { messages, type MessageKey } from "./messages";

type Vars = Record<string, string | number>;

type CheckoutI18nValue = {
  locale: Locale;
  t: (key: MessageKey, vars?: Vars) => string;
  setLocale: (next: Locale) => void;
};

const CheckoutI18nContext = createContext<CheckoutI18nValue | null>(null);

export function interpolateNodes(
  template: string,
  vars: Record<string, ReactNode>,
): ReactNode[] {
  return template.split(/(\{\w+\})/g).map((part, index) => {
    const match = /^\{(\w+)\}$/.exec(part);
    if (match && match[1] in vars) {
      return <Fragment key={index}>{vars[match[1]]}</Fragment>;
    }
    return <Fragment key={index}>{part}</Fragment>;
  });
}

export function CheckoutI18nProvider({
  locale: initialLocale,
  children,
}: {
  locale: Locale;
  children: ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const [override, setOverride] = useState<Locale | null>(null);
  const locale = override ?? initialLocale;

  useLayoutEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);

  useLayoutEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const fromQuery = parseLocale(params.get("lang") ?? params.get("locale"));
    if (!fromQuery || fromQuery === initialLocale) return;
    document.cookie = localeCookieString(fromQuery);
    document.documentElement.lang = fromQuery;
    router.refresh();
  }, [initialLocale, router]);

  const setLocale = useCallback(
    (next: Locale) => {
      document.cookie = localeCookieString(next);
      document.documentElement.lang = next;
      setOverride(next);
      const params = new URLSearchParams(window.location.search);
      params.set("lang", next);
      params.delete("locale");
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
      router.refresh();
    },
    [pathname, router],
  );

  const value = useMemo<CheckoutI18nValue>(
    () => ({
      locale,
      t: (key, vars) => interpolate(messages[locale][key], vars),
      setLocale,
    }),
    [locale, setLocale],
  );

  return (
    <CheckoutI18nContext.Provider value={value}>{children}</CheckoutI18nContext.Provider>
  );
}

export function useCheckoutT(): CheckoutI18nValue {
  const ctx = useContext(CheckoutI18nContext);
  if (!ctx) {
    throw new Error("useCheckoutT must be used within CheckoutI18nProvider");
  }
  return ctx;
}

export function CheckoutHeader() {
  const { t, locale, setLocale } = useCheckoutT();

  return (
    <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
      <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-end gap-4">
        <div
          className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-widest"
          role="group"
          aria-label={t("chrome.langSwitch")}
        >
          <button
            type="button"
            aria-pressed={locale === "en"}
            onClick={() => setLocale("en")}
            className={
              locale === "en"
                ? "text-foreground"
                : "text-muted-foreground hover:text-foreground"
            }
          >
            {t("chrome.langEn")}
          </button>
          <span className="text-muted-foreground/50" aria-hidden="true">
            |
          </span>
          <button
            type="button"
            aria-pressed={locale === "ms"}
            onClick={() => setLocale("ms")}
            className={
              locale === "ms"
                ? "text-foreground"
                : "text-muted-foreground hover:text-foreground"
            }
          >
            {t("chrome.langBm")}
          </button>
        </div>
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <svg
            className="h-3.5 w-3.5"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
          <span className="text-xs font-semibold uppercase tracking-widest">
            {t("chrome.poweredBy")}
          </span>
        </div>
      </div>
    </header>
  );
}
