import { notFound } from "next/navigation";
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

  if (!token) {
    notFound();
  }

  const { data: authData } = await serverClient.GET("/one/auth/me");

  const { data: portalData } = await serverClient.GET("/public/community/{tenantSlug}/portal", {
    params: { path: { tenantSlug }, query: { token } },
    next: { revalidate: 0 }
  });

  if (!portalData || !portalData.subscription) {
    notFound();
  }

  const fallbackUser = authData ?? {
    name: "",
    email: "",
    role: "GUEST",
    is_email_verified: false
  };

  return (
    <CommunityPortalView 
      tenantSlug={tenantSlug} 
      subscription={portalData.subscription} 
      user={fallbackUser} 
      token={token} 
    />
  );
}
