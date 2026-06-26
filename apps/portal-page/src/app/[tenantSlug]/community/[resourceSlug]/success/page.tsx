import { Suspense } from "react";
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

  return (
    <Suspense fallback={
      <div className="min-h-screen flex items-center justify-center p-4">
        <svg className="animate-spin h-8 w-8 text-muted-foreground" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M21 12a9 9 0 1 1-6.219-8.56" />
        </svg>
      </div>
    }>
      <CommunitySuccessView tenantSlug={tenantSlug} plan={plan as CommunityPlanDto} />
    </Suspense>
  );
}
