import { useState } from "react";
import { useNavigate } from "react-router-dom";
import CreateWorkspaceModal from "../modules/workspace/components/CreateWorkspaceModal";

interface EmptyWorkspaceStateProps {
  onWorkspaceCreated: (id: string) => void;
  onLogout: () => void;
}

export default function EmptyWorkspaceState({ onWorkspaceCreated, onLogout }: EmptyWorkspaceStateProps) {
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [inviteToken, setInviteToken] = useState("");
  const navigate = useNavigate();

  return (
    <div className="flex h-screen w-full flex-col items-center justify-center bg-[#f5f5f5] gap-5 px-6">
      <div className="max-w-sm text-center space-y-2">
        <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Create your workspace</h1>
        <p className="text-[13px] text-[#71717a] leading-relaxed">
          You are signed in but have no workspace yet. Pick a name and slug — no Superadmin approval. Invited? Paste the invite token instead.
        </p>
      </div>
      <button
        type="button"
        onClick={() => setIsCreateOpen(true)}
        className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors"
      >
        Create workspace
      </button>
      <form
        className="w-full max-w-sm flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          const token = inviteToken.trim();
          if (!token) return;
          navigate(`/accept-invite?token=${encodeURIComponent(token)}`);
        }}
      >
        <input
          type="text"
          value={inviteToken}
          onChange={(e) => setInviteToken(e.target.value)}
          placeholder="Invite token"
          className="flex-1 h-9 px-3 border border-[#e5e5e5] bg-white text-sm"
        />
        <button
          type="submit"
          className="h-9 px-4 border border-[#e5e5e5] text-[11px] font-bold uppercase tracking-widest hover:border-[#09090b]"
        >
          Accept invite
        </button>
      </form>
      <button
        type="button"
        onClick={onLogout}
        className="h-9 px-6 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors"
      >
        Log out
      </button>
      {isCreateOpen && (
        <CreateWorkspaceModal
          onClose={() => setIsCreateOpen(false)}
          onSuccess={(id) => {
            setIsCreateOpen(false);
            onWorkspaceCreated(id);
          }}
        />
      )}
    </div>
  );
}
