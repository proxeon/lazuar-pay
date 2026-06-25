// apps/portal-page/src/app/[tenantSlug]/portal/page.tsx
import { redirect } from "next/navigation";
import { serverClient } from "../../../modules/core/lib/server-client";

export default async function RootPortalPage({
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
    if (token) {
      redirect(`/${tenantSlug}/community/portal?token=${encodeURIComponent(token)}`);
    } else {
      return (
        <div className="flex flex-col items-center justify-center min-h-[50vh] text-center p-4">
          <h1 className="text-2xl font-semibold mb-4 text-foreground">Welcome to your Dashboard</h1>
          <p className="text-muted-foreground text-sm max-w-md">
            Please log in using a secure magic link sent to your email to manage your subscriptions and downloads.
          </p>
        </div>
      );
    }
  }

  if (token) {
    redirect(`/${tenantSlug}/community/portal?token=${encodeURIComponent(token)}`);
  }

  return (
    <div className="flex flex-col items-center justify-center min-h-[50vh] text-center p-4">
      <h1 className="text-2xl font-semibold mb-4 text-foreground">Welcome back, {authData.name}</h1>
      <p className="text-muted-foreground text-sm max-w-md mb-8">
        Your active subscriptions and digital products are managed across the ecosystem. Use the specific portal links sent to your email to access your resources.
      </p>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 w-full max-w-md">
         <div className="bg-card border border-border p-6 rounded-sm text-left">
           <h3 className="text-sm font-bold uppercase tracking-widest text-foreground mb-2">Community</h3>
           <p className="text-xs text-muted-foreground">Manage your recurring subscriptions and Telegram group access.</p>
         </div>
         <div className="bg-card border border-border p-6 rounded-sm text-left opacity-60">
           <h3 className="text-sm font-bold uppercase tracking-widest text-foreground mb-2">Vault</h3>
           <p className="text-xs text-muted-foreground">Access your digital downloads and courses. (Coming Soon)</p>
         </div>
      </div>
    </div>
  );
}
