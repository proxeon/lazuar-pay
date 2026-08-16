import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { serverClient } from "../../../../modules/core/lib/server-client";
import { CheckoutView } from "../../../../modules/checkout/components/CheckoutView";
import { getCheckoutLocale } from "../../../../modules/checkout/i18n/getCheckoutLocale";
import { t } from "../../../../modules/checkout/i18n/translate";
import { CheckoutAuthContext } from "../../../../modules/checkout/types";

export async function generateMetadata({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string; productSlug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}): Promise<Metadata> {
  const { tenantSlug, productSlug } = await params;
  const search = await searchParams;
  const locale = await getCheckoutLocale(search);
  const { data: product } = await serverClient.GET("/public/commerce/{tenantSlug}/products/{slug}", {
    params: { path: { tenantSlug, slug: productSlug } },
    next: { revalidate: 60 },
  });

  if (!product) {
    return { title: t(locale, "meta.title") };
  }

  return { title: t(locale, "meta.checkoutTitle", { product: product.name }) };
}

export default async function UniversalCheckoutPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string; productSlug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug, productSlug } = await params;
  const isCancelled = (await searchParams).cancelled === "true";

  const { data: product, error: productError } = await serverClient.GET("/public/commerce/{tenantSlug}/products/{slug}", {
    params: { path: { tenantSlug, slug: productSlug } },
    next: { revalidate: 60 }
  });

  if (productError || !product) {
    notFound();
  }

  let authContext: CheckoutAuthContext = {
    isAdminOfTenant: false,
    isGuestMode: false,
  };

  const { data: authData } = await serverClient.GET("/one/auth/me");
  
  if (authData) {
    authContext.userName = authData.name;
    authContext.userEmail = authData.email;

    const { data: entitlements } = await serverClient.GET("/one/me/entitlements");
    if (entitlements) {
      authContext.isAdminOfTenant = entitlements.some(
        (e) => e.workspace_slug === tenantSlug && (e.role === "ADMIN" || e.role === "SUPER_ADMIN")
      );
    }
  }

  return (
    <CheckoutView 
      tenantSlug={tenantSlug} 
      product={product} 
      initialAuthContext={authContext}
      isCancelled={isCancelled}
    />
  );
}
