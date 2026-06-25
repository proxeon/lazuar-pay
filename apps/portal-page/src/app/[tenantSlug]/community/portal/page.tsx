// apps/portal-page/src/app/[tenantSlug]/community/portal/page.tsx
import { notFound, redirect } from "next/navigation";
import { serverClient } from "../../../../modules/core/lib/server-client";
import { CommunityPortalView } from "../../../../modules/community/components/CommunityPortalView";

export default async function CommunityPortalPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug } = await params;
  const resolvedSearchParams = await searchParams;
  const token = resolvedSearchParams.token as string | undefined;

  const { data: authData } = await serverClient.GET("/one/auth/me");

  if (!authData) {
    const returnUrl = encodeURIComponent(`/${tenantSlug}/community/portal${token ? `?token=${token}` : ""}`);
    redirect(`http://localhost:3003/login?returnUrl=${returnUrl}`);
  }

  if (!token) {
    notFound();
  }

  const { data: portalData } = await serverClient.GET("/public/community/{tenantSlug}/portal", {
    params: { path: { tenantSlug }, query: { token } },
    next: { revalidate: 0 }
  });

  if (!portalData || !portalData.subscription) {
    notFound();
  }

  return (
    <CommunityPortalView 
      tenantSlug={tenantSlug} 
      subscription={portalData.subscription} 
      user={authData} 
      token={token} 
    />
  );
}
