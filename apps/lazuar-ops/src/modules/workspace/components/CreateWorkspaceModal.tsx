import { useState } from "react";
import { Loader2, X } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import { slugify, validateSlug } from "../../../lib/workspace-slug";

interface CreateWorkspaceModalProps {
  onClose: () => void;
  onSuccess: (newWorkspaceId: string) => void;
}

export default function CreateWorkspaceModal({ onClose, onSuccess }: CreateWorkspaceModalProps) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");

  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setName(val);
    setSlug(slugify(val));
  };

  const createMutation = useMutation({
    mutationFn: async () => {
      const normalized = slugify(slug);
      setSlug(normalized);
      const slugError = validateSlug(normalized);
      if (slugError) throw new Error(slugError);
      if (!name.trim()) throw new Error("Workspace name is required.");

      const { data, error } = await client.POST("/one/workspaces", {
        body: {
          name: name.trim(),
          slug: normalized,
          provision_apps: ["OPS", "BILLING", "PAYMENTS", "CRM", "LHDN"]
        }
      });
      if (error) throw new Error(error.detail);
      return data.id;
    },
    onSuccess: async (newId) => {
      toast.success("Workspace created successfully");
      await queryClient.invalidateQueries({ queryKey: ["entitlements"] });
      onSuccess(newId);
    },
    onError: (err: Error) => toast.error(err.message || "Failed to create workspace")
  });

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={onClose} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200">
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create New Workspace</h3>
          <button onClick={onClose} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
        </div>
        <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
          <div className="p-5 space-y-4">
            {createMutation.isError && (
              <div className="p-3 bg-rose-50 border border-rose-200">
                <p className="text-[10px] font-bold uppercase tracking-widest text-rose-600">
                  {createMutation.error?.message || "Failed to create workspace."}
                </p>
              </div>
            )}
            
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Workspace Name *</label>
              <input required type="text" value={name} onChange={handleNameChange} disabled={createMutation.isPending} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" placeholder="e.g. Acme Corp" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Workspace Slug *</label>
              <input required type="text" minLength={3} maxLength={63} value={slug} onChange={(e) => setSlug(slugify(e.target.value))} disabled={createMutation.isPending} className="w-full h-9 border border-[#e5e5e5] bg-[#fafafa] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" placeholder="acme-corp" />
              <p className="text-[11px] text-[#a1a1aa]">
                3–63 chars: a–z, 0–9, hyphens. Not reserved (login, admin, portal, …).
              </p>
            </div>
          </div>
          <div className="p-4 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex justify-end gap-2">
            <button type="button" onClick={onClose} disabled={createMutation.isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
            <button type="submit" disabled={createMutation.isPending} className="px-5 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
              {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create Workspace
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
