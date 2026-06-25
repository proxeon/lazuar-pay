// apps/portal-page/src/app/[tenantSlug]/community/[resourceSlug]/checkout/page.tsx
import { notFound } from "next/navigation";
import { serverClient } from "../../../../../modules/core/lib/server-client";
import { CommunityCheckoutView } from "../../../../../modules/community/components/CommunityCheckoutView";
import { CheckoutAuthContext } from "../../../../../modules/checkout/types";
import { CommunityPlanDto } from "../../../../../modules/community/lib/api";

export default async function CommunityCheckoutPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string; resourceSlug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug, resourceSlug } = await params;
  const isCancelled = (await searchParams).cancelled === "true";

  const { data: plan, error: planError } = await serverClient.GET("/public/community/{tenantSlug}/plans/{slug}", {
    params: { path: { tenantSlug, slug: resourceSlug } },
    next: { revalidate: 60 }
  });

  if (planError || !plan) {
    notFound();
  }

  if (plan.is_full) {
    return (
      <div className="min-h-[60vh] flex flex-col items-center justify-center p-4">
        <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-lg w-full text-center">
          <svg className="h-8 w-8 text-red-500 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
          <h1 className="text-xl font-semibold mb-2 text-foreground">Program is Full</h1>
          <p className="text-sm text-muted-foreground mb-8">This program is currently not accepting new enrollments.</p>
        </div>
      </div>
    );
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

  const slimPlan: CommunityPlanDto = {
    ...plan,
    admin_notes: ""
  };

  return (
    <CommunityCheckoutView 
      tenantSlug={tenantSlug} 
      plan={slimPlan} 
      initialAuthContext={authContext}
      isCancelled={isCancelled}
    />
  );
}
