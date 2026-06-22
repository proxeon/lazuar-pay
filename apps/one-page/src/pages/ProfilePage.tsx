import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Loader2, Save } from "lucide-react";
import { client } from "../lib/api-client";

export default function ProfilePage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");

  const { data: user, isLoading } = useQuery({
    queryKey: ["auth-me"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/auth/me");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  useEffect(() => {
    if (user) {
      setName(user.name);
    }
  }, [user]);

  const updateMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.PUT("/one/me/profile", {
        body: { name: name.trim() }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Profile updated successfully.");
      queryClient.invalidateQueries({ queryKey: ["auth-me"] });
    },
    onError: (err: any) => toast.error("Failed to update profile", { description: err.message })
  });

  if (isLoading) return <div className="flex justify-center p-8"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>;

  return (
    <div className="space-y-8 animate-in fade-in duration-300">
      <div>
        <h1 className="text-2xl font-bold text-[#09090b] tracking-tight">Global Profile</h1>
        <p className="text-[13px] text-[#71717a] mt-1">Manage your public display name across the ecosystem.</p>
      </div>

      <div className="bg-white border border-[#e5e5e5] max-w-xl">
        <div className="p-5 border-b border-[#f4f4f5] bg-[#fafafa]/50">
          <h2 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Personal Information</h2>
        </div>
        
        <form onSubmit={(e) => { e.preventDefault(); updateMutation.mutate(); }}>
          <div className="p-6 space-y-6">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address (Immutable)</label>
              <input 
                type="email" 
                value={user?.email || ""} 
                disabled 
                className="w-full h-10 px-3 text-[13px] border border-[#e5e5e5] bg-[#f4f4f5] text-[#71717a] cursor-not-allowed"
              />
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Full Name *</label>
              <input 
                type="text" 
                required
                value={name} 
                onChange={e => setName(e.target.value)}
                disabled={updateMutation.isPending}
                className="w-full h-10 px-3 text-[13px] border border-[#e5e5e5] bg-white focus:outline-none focus:border-[#09090b]"
              />
            </div>
          </div>

          <div className="p-5 border-t border-[#e5e5e5] bg-[#fafafa] flex justify-end">
            <button 
              type="submit" 
              disabled={updateMutation.isPending || !name.trim() || name === user?.name}
              className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center gap-2"
            >
              {updateMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />} 
              Save Changes
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
