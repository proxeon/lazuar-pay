// apps/portal-page/src/app/[tenantSlug]/community/[resourceSlug]/success/page.tsx
import { notFound } from "next/navigation";
import { serverClient } from "../../../../../modules/core/lib/server-client";
import { CommunitySuccessView } from "../../../../../modules/community/components/CommunitySuccessView";
import { CommunityPlanDto } from "../../../../../modules/community/lib/api";

export default async function CommunitySuccessPage({
  params,
}: {
  params: Promise<{ tenantSlug: string; resourceSlug: string }>;
}) {
  const { tenantSlug, resourceSlug } = await params;

  const { data: plan, error } = await serverClient.GET("/public/community/{tenantSlug}/plans/{slug}", {
    params: { path: { tenantSlug, slug: resourceSlug } },
    next: { revalidate: 60 }
  });

  if (error || !plan) {
    notFound();
  }

  return <CommunitySuccessView tenantSlug={tenantSlug} plan={plan as CommunityPlanDto} />;
}
