import { useState } from "react";
import CreateWorkspaceModal from "../modules/workspace/components/CreateWorkspaceModal";

interface EmptyWorkspaceStateProps {
  onWorkspaceCreated: (id: string) => void;
  onLogout: () => void;
}

export default function EmptyWorkspaceState({ onWorkspaceCreated, onLogout }: EmptyWorkspaceStateProps) {
  const [isCreateOpen, setIsCreateOpen] = useState(false);

  return (
    <div className="flex h-screen w-full flex-col items-center justify-center bg-[#f5f5f5] gap-5 px-6">
      <div className="max-w-sm text-center space-y-2">
        <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Create your workspace</h1>
        <p className="text-[13px] text-[#71717a] leading-relaxed">
          You are signed in but have no workspace yet. Pick a name and slug — no Superadmin approval.
        </p>
      </div>
      <button
        type="button"
        onClick={() => setIsCreateOpen(true)}
        className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors"
      >
        Create workspace
      </button>
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
