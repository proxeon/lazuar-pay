import { notFound, redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { serverClient } from "../../../modules/core/lib/server-client";
import { RequestMagicLinkForm } from "../../../modules/portal/components/RequestMagicLinkForm";
import { PortalPlanChange } from "../../../modules/portal/components/PortalPlanChange";
import { ShieldCheck, FileText } from "lucide-react";

function formatPaidThrough(value?: string | null) {
  if (!value) return null;
  return new Date(value).toLocaleDateString();
}

async function cancelPortalSubscription(
  tenantSlug: string,
  token: string,
  subscriptionId: string,
  atPeriodEnd: boolean,
) {
  "use server";
  const { error } = await serverClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
    params: { path: { tenantSlug }, query: { token } },
    body: { subscription_id: subscriptionId, at_period_end: atPeriodEnd },
  });
  if (error) {
    redirect(`/${tenantSlug}/portal?token=${encodeURIComponent(token)}&err=action`);
  }
  revalidatePath(`/${tenantSlug}/portal`);
}

async function keepPortalSubscription(tenantSlug: string, token: string, subscriptionId: string) {
  "use server";
  const { error } = await serverClient.POST("/public/commerce/{tenantSlug}/portal/keep", {
    params: { path: { tenantSlug }, query: { token } },
    body: { subscription_id: subscriptionId },
  });
  if (error) {
    redirect(`/${tenantSlug}/portal?token=${encodeURIComponent(token)}&err=action`);
  }
  revalidatePath(`/${tenantSlug}/portal`);
}

export default async function AggregatedPortalPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug } = await params;
  const resolvedSearchParams = await searchParams;
  const token = resolvedSearchParams.token as string | undefined;
  const actionError = resolvedSearchParams.err === "action";

  if (!token) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh] text-center p-4">
        <h1 className="text-2xl font-semibold mb-4 text-foreground">Welcome to your Dashboard</h1>
        <p className="text-sm text-muted-foreground max-w-md mx-auto">
          Enter the email on your subscription and we will send a secure link that expires in 24 hours.
        </p>
        <RequestMagicLinkForm tenantSlug={tenantSlug} />
      </div>
    );
  }

  const accessToken = token;

  const { data: commerceData, error: commerceError } = await serverClient.GET("/public/commerce/{tenantSlug}/portal", {
    params: { path: { tenantSlug }, query: { token: token ?? "" } },
    next: { revalidate: 0 }
  });

  if (commerceError || !commerceData) {
    notFound();
  }

  const { data: documentsData } = await serverClient.GET("/public/commerce/{tenantSlug}/portal/documents", {
    params: { path: { tenantSlug }, query: { token: token ?? "" } },
    next: { revalidate: 0 }
  });
  const documents = documentsData?.items ?? [];

  return (
    <div className="w-full max-w-4xl mx-auto space-y-8">
      {actionError && (
        <p className="p-4 bg-rose-50 border border-rose-200 text-rose-700 text-sm font-medium">
          That change could not be saved. Try again.
        </p>
      )}

      <div className="flex flex-col sm:flex-row sm:items-center justify-between p-4 bg-emerald-50/50 border border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900 gap-2">
        <p className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500 flex items-center gap-1.5">
          <ShieldCheck size={14} /> Identity Verified
        </p>
        <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-500/80">
          Accessing resources for this workspace.
        </p>
      </div>

      <div className="space-y-8">
        <h2 className="text-xl font-bold tracking-tight text-foreground border-b border-border/60 pb-2">Active Subscriptions</h2>
        {commerceData.subscriptions.length === 0 ? (
          <p className="text-sm text-muted-foreground">No active subscriptions found.</p>
        ) : (
          <div className="grid grid-cols-1 gap-4">
            {commerceData.subscriptions.map(sub => {
              const paidThrough = formatPaidThrough(sub.current_period_end);
              const isActiveOrTrialing = sub.status === "ACTIVE" || sub.status === "TRIALING";
              const isHealthyActive = sub.status === "ACTIVE" && !sub.cancel_at_period_end;
              const isHealthyForCancel = isActiveOrTrialing && !sub.cancel_at_period_end;
              const isFlagged = isActiveOrTrialing && sub.cancel_at_period_end;
              const isPastDue = sub.status === "PAST_DUE";

              return (
                <div key={sub.id} className="bg-card border border-border/60 shadow-sm p-6 rounded-none flex flex-col md:flex-row gap-6 justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-3 mb-2">
                      <h3 className="text-lg font-semibold text-foreground">{sub.product_name}</h3>
                      <span className="text-[10px] font-bold uppercase tracking-widest bg-secondary text-foreground px-2 py-0.5 border border-border">
                        {sub.status.replace("_", " ")}
                      </span>
                      {sub.status === "TRIALING" && (
                        <span className="text-[10px] font-bold uppercase tracking-widest bg-sky-50 text-sky-800 px-2 py-0.5 border border-sky-200">
                          Trial{sub.trial_ends_at ? ` until ${formatPaidThrough(sub.trial_ends_at)}` : ""}
                        </span>
                      )}
                      {isFlagged && (
                        <span className="text-[10px] font-bold uppercase tracking-widest bg-amber-50 text-amber-800 px-2 py-0.5 border border-amber-200">
                          Cancels {paidThrough ? `on ${paidThrough}` : "at period end"}
                        </span>
                      )}
                    </div>
                    {paidThrough && (
                      <p className="text-xs text-muted-foreground font-mono mb-4">
                        {isFlagged ? "Access until" : "Renews/Expires"}: {paidThrough}
                      </p>
                    )}
                    {isHealthyForCancel && paidThrough && (
                      <p className="text-xs text-muted-foreground">
                        Cancel at period end keeps access until {paidThrough}. No further charges after that.
                      </p>
                    )}
                    {isHealthyActive && token && (
                      <PortalPlanChange
                        tenantSlug={tenantSlug}
                        token={token}
                        subscriptionId={sub.id}
                        paidThrough={sub.current_period_end}
                        pendingProductName={sub.pending_product_name}
                      />
                    )}
                    {isPastDue && (
                      <p className="text-xs text-muted-foreground">
                        This plan is past due. Canceling now ends access immediately.
                      </p>
                    )}
                  </div>
                  
                  <div className="shrink-0 flex flex-col gap-2 items-end justify-center">
                    {sub.document_url && (
                      <a href={sub.document_url} target="_blank" rel="noopener noreferrer" className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors mb-2">
                        <FileText size={12} /> {sub.document_label || "Download receipt"}
                      </a>
                    )}
                    {isHealthyForCancel && (
                      <>
                        <form action={cancelPortalSubscription.bind(null, tenantSlug, accessToken, sub.id, true)}}>
                          <button className="h-9 px-4 border border-red-200 bg-background text-red-600 text-[11px] font-bold uppercase tracking-widest hover:bg-red-50 transition-colors rounded-none">
                            Cancel Plan
                          </button>
                        </form>
                        <form action={cancelPortalSubscription.bind(null, tenantSlug, accessToken, sub.id, false)}}>
                          <button className="h-8 px-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground hover:text-red-600 transition-colors">
                            Cancel immediately
                          </button>
                        </form>
                      </>
                    )}
                    {isFlagged && (
                      <form action={keepPortalSubscription.bind(null, tenantSlug, accessToken, sub.id)}}>
                        <button className="h-9 px-4 border border-emerald-200 bg-background text-emerald-700 text-[11px] font-bold uppercase tracking-widest hover:bg-emerald-50 transition-colors rounded-none">
                          Keep plan
                        </button>
                      </form>
                    )}
                    {(isHealthyActive || isPastDue || isFlagged) && !(sub as { is_reminder_only?: boolean }).is_reminder_only && (
                      <a
                        href={`/${tenantSlug}/update-payment/${sub.id}?token=${encodeURIComponent(token)}`}
                        className="h-9 px-4 border border-border bg-background text-[11px] font-bold uppercase tracking-widest hover:bg-secondary transition-colors rounded-none inline-flex items-center"
                      >
                        Update payment method
                      </a>
                    )}
                    {isPastDue && (
                      <form action={cancelPortalSubscription.bind(null, tenantSlug, accessToken, sub.id, false)}}>
                        <button className="h-9 px-4 border border-red-200 bg-background text-red-600 text-[11px] font-bold uppercase tracking-widest hover:bg-red-50 transition-colors rounded-none">
                          Cancel immediately
                        </button>
                      </form>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <div className="space-y-4">
        <h2 className="text-xl font-bold tracking-tight text-foreground border-b border-border/60 pb-2">Documents</h2>
        {documents.length === 0 ? (
          <p className="text-sm text-muted-foreground">No receipts or invoices yet.</p>
        ) : (
          <div className="border border-border/60 overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="bg-secondary/30 border-b border-border/60">
                <tr>
                  <th className="px-4 py-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Date</th>
                  <th className="px-4 py-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Number</th>
                  <th className="px-4 py-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Type</th>
                  <th className="px-4 py-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground text-right">Amount</th>
                  <th className="px-4 py-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground">MyInvois</th>
                  <th className="px-4 py-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/40">
                {documents.map((doc) => (
                  <tr key={doc.id}>
                    <td className="px-4 py-3 text-xs font-mono">{new Date(doc.issued_at).toLocaleDateString("en-GB")}</td>
                    <td className="px-4 py-3 text-xs font-mono font-semibold">{doc.document_number || "—"}</td>
                    <td className="px-4 py-3 text-xs">{doc.type}</td>
                    <td className="px-4 py-3 text-xs font-mono text-right">{doc.currency} {doc.amount.toFixed(2)}</td>
                    <td className="px-4 py-3 text-[10px] font-bold uppercase tracking-widest text-muted-foreground">
                      {doc.type === "Official Receipt" || doc.type === "Proforma Invoice" || doc.type === "Draft Quotation"
                        ? "—"
                        : (doc.lhdn_status && doc.lhdn_status !== "B2C_RECEIPT" ? doc.lhdn_status : "—")}
                    </td>
                    <td className="px-4 py-3 text-right">
                      {doc.download_url ? (
                        <a href={doc.download_url} target="_blank" rel="noopener noreferrer" className="text-[10px] font-bold uppercase tracking-widest text-foreground hover:underline">
                          Download
                        </a>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
