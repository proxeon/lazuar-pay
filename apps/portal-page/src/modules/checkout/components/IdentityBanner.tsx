// apps/portal-page/src/modules/checkout/components/IdentityBanner.tsx

interface IdentityBannerProps {
  userName?: string;
  isAdminOfTenant: boolean;
  isGuestMode: boolean;
  onEnableGuestMode: () => void;
  onDisableGuestMode: () => void;
}

export function IdentityBanner({
  userName,
  isAdminOfTenant,
  isGuestMode,
  onEnableGuestMode,
  onDisableGuestMode
}: IdentityBannerProps) {
  if (!userName) return null;

  if (isGuestMode) {
    return (
      <div className="flex items-center justify-between p-3 bg-zinc-100 border border-zinc-200 dark:bg-zinc-900 dark:border-zinc-800 mb-4">
        <p className="text-[11px] font-bold uppercase tracking-widest text-zinc-600 dark:text-zinc-400">
          Checking out as Guest
        </p>
        <button 
          type="button" 
          onClick={onDisableGuestMode} 
          className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:underline dark:text-zinc-300"
        >
          Use my Lazuar account
        </button>
      </div>
    );
  }

  if (isAdminOfTenant) {
    return (
      <div className="flex items-center justify-between p-3 bg-blue-50 border border-blue-200 dark:bg-blue-950/30 dark:border-blue-900 mb-4">
        <p className="text-[11px] font-bold uppercase tracking-widest text-blue-700 dark:text-blue-400">
          Viewing as Workspace Admin
        </p>
        <button 
          type="button" 
          onClick={onEnableGuestMode} 
          className="text-[11px] font-bold uppercase tracking-widest text-blue-700 dark:text-blue-400 hover:underline"
        >
          Checkout as Guest
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-center justify-between p-3 bg-emerald-50/50 border border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900 mb-4">
      <p className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500">
        ✓ Logged in as {userName}
      </p>
      <button 
        type="button" 
        onClick={onEnableGuestMode} 
        className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500 hover:underline"
      >
        Checkout as Guest
      </button>
    </div>
  );
}
