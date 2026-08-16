import { ReactNode } from "react";
import type { Metadata } from "next";
import { CheckoutHeader, CheckoutI18nProvider } from "../../../../modules/checkout/i18n/CheckoutI18n";
import { getCheckoutLocale } from "../../../../modules/checkout/i18n/getCheckoutLocale";
import { t } from "../../../../modules/checkout/i18n/translate";
import { serverClient } from "../../../../modules/core/lib/server-client";
import { fetchWorkspaceBranding } from "../../../../modules/core/lib/branding";

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

  const branding = await fetchWorkspaceBranding(tenantSlug);
  return {
    title: branding?.name
      ? `${product.name} · ${branding.name}`
      : t(locale, "meta.checkoutTitle", { product: product.name }),
  };
}

export default async function BlindCheckoutLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ tenantSlug: string; productSlug: string }>;
}) {
  const { tenantSlug } = await params;
  const locale = await getCheckoutLocale();
  const branding = await fetchWorkspaceBranding(tenantSlug);

  return (
    <CheckoutI18nProvider locale={locale}>
      <div className="flex flex-1 flex-col min-h-0 bg-zinc-50 dark:bg-black">
        <CheckoutHeader workspaceName={branding?.name} logoUrl={branding?.logo_url} />
        <main className="flex-1 flex flex-col w-full min-h-0">
          {children}
        </main>
      </div>
    </CheckoutI18nProvider>
  );
}
