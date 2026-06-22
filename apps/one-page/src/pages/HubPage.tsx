import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Loader2, CreditCard, ExternalLink, Mail, Building2, Clock } from "lucide-react";
import { client, OPS_URL } from "../lib/api-client";

export default function HubPage() {
  const queryClient = useQueryClient();
  const [tokens, setTokens] = useState<Record<string, string>>({});
  const [activePortalId, setActivePortalId] = useState<string | null>(null);

  // Fetch Subscriptions
  const { data: subscriptions, isLoading: isLoadingSubs } = useQuery({
    queryKey: ["my-subscriptions"],
    queryFn: async () => {
      const { data, error } = await client.GET("/community/me/subscriptions");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  // Fetch Invitations
  const { data: invitations, isLoading: isLoadingInvites } = useQuery({
    queryKey: ["my-invitations"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/me/invitations");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  // Portal Link Generator
  const portalLinkMutation = useMutation({
    mutationFn: async (subscriptionId: string) => {
      const { data, error } = await client.POST("/community/me/subscriptions/{id}/portal-link", {
        params: { path: { id: subscriptionId } }
      });
      if (error) throw new Error(error.detail);
      return data.url;
    },
    onMutate: (id) => setActivePortalId(id),
    onSettled: () => setActivePortalId(null),
    onSuccess: (url) => {
      window.location.href = url;
    },
    onError: (err: any) => toast.error("Failed to generate portal link", { description: err.message })
  });

  // Accept Invite Mutation
  const acceptInviteMutation = useMutation({
    mutationFn: async (token: string) => {
      const { error } = await client.POST("/one/workspaces/invites/accept", {
        body: { token }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: async () => {
      toast.success("Invitation accepted!");
      await queryClient.invalidateQueries({ queryKey: ["my-invitations"] });
      
      // Check entitlements to see if we should route them to the Ops console
      const { data: entitlements } = await client.GET("/one/me/entitlements");
      if (entitlements && entitlements.some(e => e.role === "ADMIN" || e.role === "SUPER_ADMIN" || e.role === "STAFF")) {
        toast.success("Routing to workspace console...");
        window.location.href = OPS_URL;
      }
    },
    onError: (err: any) => toast.error("Failed to accept invite", { description: err.message })
  });

  return (
    <div className="space-y-8 animate-in fade-in duration-300">
      <div>
        <h1 className="text-2xl font-bold text-[#09090b] tracking-tight">Welcome to your Hub</h1>
        <p className="text-[13px] text-[#71717a] mt-1">Manage your active subscriptions and pending workspace invitations.</p>
      </div>

      {/* Pending Invitations Section */}
      {!isLoadingInvites && invitations && invitations.length > 0 && (
        <div className="space-y-4">
          <div className="flex items-center gap-2 border-b border-[#e5e5e5] pb-2">
            <Mail size={16} className="text-[#09090b]" />
            <h2 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Pending Invitations</h2>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {invitations.map((invite) => (
              <div key={invite.id} className="bg-white border border-[#e5e5e5] p-5 flex flex-col justify-between">
                <div className="mb-4">
                  <div className="flex items-center gap-2 mb-1">
                    <Building2 size={14} className="text-[#71717a]" />
                    <h3 className="text-[14px] font-bold text-[#09090b]">{invite.workspace_name}</h3>
                  </div>
                  <p className="text-[12px] text-[#71717a]">
                    Invited to join as <span className="font-bold text-[#09090b]">{invite.role}</span>
                  </p>
                  <p className="text-[10px] text-[#a1a1aa] mt-2 flex items-center gap-1">
                    <Clock size={10} /> Expires {new Date(invite.expires_at).toLocaleDateString()}
                  </p>
                </div>
                
                <div className="space-y-2 border-t border-[#f4f4f5] pt-4">
                  <input 
                    type="text" 
                    placeholder="Paste token from email..." 
                    value={tokens[invite.id] || ""}
                    onChange={(e) => setTokens({ ...tokens, [invite.id]: e.target.value })}
                    className="w-full h-9 px-3 text-[12px] border border-[#e5e5e5] bg-[#fafafa] focus:outline-none focus:border-[#09090b]"
                  />
                  <button 
                    onClick={() => {
                      const t = tokens[invite.id];
                      if (!t) { toast.error("Please enter the token from your email."); return; }
                      acceptInviteMutation.mutate(t);
                    }}
                    disabled={acceptInviteMutation.isPending}
                    className="w-full h-9 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
                  >
                    {acceptInviteMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : "Accept Invitation"}
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Active Subscriptions Section */}
      <div className="space-y-4">
        <div className="flex items-center gap-2 border-b border-[#e5e5e5] pb-2">
          <CreditCard size={16} className="text-[#09090b]" />
          <h2 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">My Subscriptions</h2>
        </div>

        {isLoadingSubs ? (
          <div className="flex justify-center py-12"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
        ) : subscriptions?.length === 0 ? (
          <div className="bg-white border border-[#e5e5e5] p-12 text-center">
            <p className="text-[13px] text-[#71717a]">You have no active community subscriptions.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {subscriptions?.map((sub) => (
              <div key={sub.subscription_id} className="bg-white border border-[#e5e5e5] flex flex-col">
                <div className="p-5 border-b border-[#f4f4f5]">
                  <div className="flex items-start justify-between mb-3">
                    <span className="text-[9px] font-bold uppercase tracking-widest bg-zinc-100 text-zinc-600 px-1.5 py-0.5 border border-zinc-200">
                      {sub.workspace_name}
                    </span>
                    <span className="text-[9px] font-bold uppercase tracking-widest text-[#71717a]">
                      {sub.status.replace("_", " ")}
                    </span>
                  </div>
                  <h3 className="text-[15px] font-bold text-[#09090b] leading-tight mb-1">{sub.plan_name}</h3>
                  <p className="text-[12px] font-mono text-[#71717a]">RM {sub.amount.toFixed(2)}</p>
                </div>
                
                <div className="p-4 bg-[#fafafa] flex-1 flex flex-col justify-between">
                  <div className="mb-4">
                    <span className="block text-[10px] uppercase tracking-widest text-[#a1a1aa] mb-0.5">Next Billing</span>
                    <span className="text-[12px] font-medium text-[#09090b]">
                      {sub.next_billing_date ? new Date(sub.next_billing_date).toLocaleDateString() : 'N/A'}
                    </span>
                  </div>
                  <button 
                    onClick={() => portalLinkMutation.mutate(sub.subscription_id)}
                    disabled={portalLinkMutation.isPending}
                    className="w-full h-9 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:border-[#09090b] hover:bg-[#fafafa] transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
                  >
                    {activePortalId === sub.subscription_id ? <Loader2 size={14} className="animate-spin" /> : <ExternalLink size={14} />}
                    Subscriber Portal
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
