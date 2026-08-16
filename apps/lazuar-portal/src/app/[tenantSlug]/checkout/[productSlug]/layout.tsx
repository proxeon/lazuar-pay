import { ReactNode } from "react";
import type { Metadata } from "next";
import { CheckoutHeader, CheckoutI18nProvider } from "../../../../modules/checkout/i18n/CheckoutI18n";
import { getCheckoutLocale } from "../../../../modules/checkout/i18n/getCheckoutLocale";
import { t } from "../../../../modules/checkout/i18n/translate";
import { serverClient } from "../../../../modules/core/lib/server-client";

export async function generateMetadata({
  params,
}: {
  params: Promise<{ tenantSlug: string; productSlug: string }>;
}): Promise<Metadata> {
  const { tenantSlug, productSlug } = await params;
  const locale = await getCheckoutLocale();
  const { data: product } = await serverClient.GET("/public/commerce/{tenantSlug}/products/{slug}", {
    params: { path: { tenantSlug, slug: productSlug } },
    next: { revalidate: 60 },
  });

  if (!product) {
    return { title: t(locale, "meta.title") };
  }

  return { title: t(locale, "meta.checkoutTitle", { product: product.name }) };
}

export default async function BlindCheckoutLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ tenantSlug: string; productSlug: string }>;
}) {
  await params;
  const locale = await getCheckoutLocale();

  return (
    <CheckoutI18nProvider locale={locale}>
      <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
        <CheckoutHeader />
        <main className="flex-1 w-full">
          {children}
        </main>
      </div>
    </CheckoutI18nProvider>
  );
}
