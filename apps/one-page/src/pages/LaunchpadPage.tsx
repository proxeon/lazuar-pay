import { useState, useEffect } from "react";
import { 
  Loader2, 
  ShieldAlert, 
  User, 
  Settings, 
  LayoutDashboard, 
  ExternalLink, 
  LogOut 
} from "lucide-react";

// Port configuration resolved from standard mappings
const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";
const AUTH_URL = import.meta.env.VITE_AUTH_URL || "http://localhost:3001";
const OPS_URL = import.meta.env.VITE_OPS_URL || "http://localhost:3003";
const COMMUNITY_URL = import.meta.env.VITE_COMMUNITY_URL || "http://localhost:3021";

interface AuthUser {
  email: string;
  name: string;
  role: string;
  is_email_verified: boolean;
}

interface EntitlementDto {
  workspace_id: string;
  workspace_name: string;
  workspace_slug: string;
  role: string;
}

export default function LaunchpadPage() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [entitlements, setEntitlements] = useState<EntitlementDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Authenticate user session and retrieve active tenant entitlements
  useEffect(() => {
    async function fetchSessionData() {
      try {
        const sessionRes = await fetch(`${API_URL}/one/auth/me`, { credentials: "include" });
        if (!sessionRes.ok) {
          window.location.href = `${AUTH_URL}/login`;
          return;
        }

        const userData: AuthUser = await sessionRes.json();
        setUser(userData);

        const entitlementsRes = await fetch(`${API_URL}/one/me/entitlements`, { credentials: "include" });
        if (entitlementsRes.ok) {
          const entitlementsData: EntitlementDto[] = await entitlementsRes.json();
          setEntitlements(entitlementsData);
        }
      } catch (err) {
        console.error("Authentication handshake failed:", err);
        window.location.href = `${AUTH_URL}/login`;
      } finally {
        setIsLoading(false);
      }
    }

    fetchSessionData();
  }, []);

  const handleLogout = async () => {
    try {
      await fetch(`${API_URL}/one/auth/logout`, { method: "POST", credentials: "include" });
    } catch (err) {
      console.error("Logout execution failed:", err);
    } finally {
      window.location.href = `${AUTH_URL}/login`;
    }
  };

  if (isLoading || !user) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-zinc-50">
        <Loader2 className="animate-spin h-8 w-8 text-zinc-400" />
      </div>
    );
  }

  // Filter workspaces based on structural role definitions
  const adminWorkspaces = entitlements.filter(
    (e) => e.role === "ADMIN" || e.role === "SUPER_ADMIN" || e.role === "STAFF"
  );
  
  const subscriberWorkspaces = entitlements.filter(
    (e) => e.role === "CLIENT"
  );

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 text-zinc-900 font-sans antialiased">
      {/* Top Navigation Anchor */}
      <header className="sticky top-0 z-40 w-full bg-white border-b border-zinc-200">
        <div className="max-w-5xl mx-auto px-6 h-14 flex items-center justify-between">
          <span className="text-[14px] font-bold uppercase tracking-widest">Lazuar One</span>
          <div className="flex items-center gap-4">
            <span className="text-xs text-zinc-500 font-medium">{user.email}</span>
            <button 
              onClick={handleLogout}
              className="text-xs text-rose-600 hover:text-rose-700 font-bold uppercase tracking-widest flex items-center gap-1.5 transition-colors"
            >
              <LogOut size={14} /> Logout
            </button>
          </div>
        </div>
      </header>

      {/* Main Switchboard Body */}
      <main className="flex-1 w-full max-w-5xl mx-auto px-6 py-12 md:py-16 space-y-12">
        <div className="border-b border-zinc-200 pb-6">
          <h1 className="text-2xl md:text-3xl font-bold tracking-tight text-zinc-900">
            Welcome back, {user.name}
          </h1>
          <p className="text-sm text-zinc-500 mt-1.5 leading-normal">
            Select a pathway below to manage your global identity profile, launch creator tools, or access your learning portals.
          </p>
        </div>

        {/* Multi-Tenant Switchboard Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 items-start">
          
          {/* Panel A: Master Profile Setup */}
          <div className="bg-white border border-zinc-200 p-6 flex flex-col h-full rounded-none">
            <div className="flex justify-between items-start mb-6">
              <div className="p-2 bg-zinc-50 border border-zinc-200 rounded-none text-zinc-600">
                <User size={16} />
              </div>
              <span className="text-[9px] font-bold uppercase tracking-widest text-zinc-400 bg-zinc-100 border border-zinc-200 px-1.5 py-0.5">
                Identity Profile
              </span>
            </div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-zinc-900 mb-2">My Profile</h3>
            <p className="text-[13px] text-zinc-500 mb-6 leading-relaxed flex-1">
              Edit your master profile credentials, verify your security keys, and manage global passwords.
            </p>
            <a 
              href={`${AUTH_URL}/profile`}
              className="h-10 border border-zinc-200 bg-zinc-50 hover:bg-zinc-100 text-zinc-800 text-[10px] font-bold uppercase tracking-widest flex items-center justify-center gap-1.5 transition-colors"
            >
              <Settings size={14} /> Manage Identity
            </a>
          </div>

          {/* Panel B: Administrative Creator Console */}
          {adminWorkspaces.length > 0 && (
            <div className="bg-white border border-zinc-200 p-6 flex flex-col h-full rounded-none">
              <div className="flex justify-between items-start mb-6">
                <div className="p-2 bg-blue-50 border border-blue-200 rounded-none text-blue-600">
                  <LayoutDashboard size={16} />
                </div>
                <span className="text-[9px] font-bold uppercase tracking-widest text-blue-700 bg-blue-50 border border-blue-200 px-1.5 py-0.5">
                  Console Access
                </span>
              </div>
              <h3 className="text-sm font-bold uppercase tracking-widest text-zinc-900 mb-2">Creator Workspaces</h3>
              <p className="text-[13px] text-zinc-500 mb-4 leading-relaxed">
                Launch administrative dashboards to configure templates, process payments, and audit ledgers.
              </p>
              <div className="space-y-2 flex-1 mb-6">
                {adminWorkspaces.map((workspace) => (
                  <a 
                    key={workspace.workspace_id}
                    href={`${OPS_URL}/community/dashboard?workspaceId=${workspace.workspace_id}`}
                    className="flex items-center justify-between p-2.5 bg-zinc-50 border border-zinc-200 hover:border-zinc-400 transition-colors text-xs font-semibold text-zinc-800 group"
                  >
                    <span>{workspace.workspace_name}</span>
                    <ExternalLink size={12} className="text-zinc-400 group-hover:text-zinc-900 transition-colors" />
                  </a>
                ))}
              </div>
            </div>
          )}

          {/* Panel C: Public Learning Portals */}
          {subscriberWorkspaces.length > 0 && (
            <div className="bg-white border border-zinc-200 p-6 flex flex-col h-full rounded-none">
              <div className="flex justify-between items-start mb-6">
                <div className="p-2 bg-emerald-50 border border-emerald-200 rounded-none text-emerald-600">
                  <ShieldAlert size={16} />
                </div>
                <span className="text-[9px] font-bold uppercase tracking-widest text-emerald-700 bg-emerald-50 border border-emerald-200 px-1.5 py-0.5">
                  Portals Access
                </span>
              </div>
              <h3 className="text-sm font-bold uppercase tracking-widest text-zinc-900 mb-2">Active Subscriptions</h3>
              <p className="text-[13px] text-zinc-500 mb-4 leading-relaxed">
                Access your active member accounts, view curriculums, and manage transaction receipts.
              </p>
              <div className="space-y-2 flex-1 mb-6">
                {subscriberWorkspaces.map((workspace) => (
                  <a 
                    key={workspace.workspace_id}
                    href={`${COMMUNITY_URL}/${workspace.workspace_slug}/portal`}
                    className="flex items-center justify-between p-2.5 bg-zinc-50 border border-zinc-200 hover:border-zinc-400 transition-colors text-xs font-semibold text-zinc-800 group"
                  >
                    <span>{workspace.workspace_name}</span>
                    <ExternalLink size={12} className="text-zinc-400 group-hover:text-zinc-900 transition-colors" />
                  </a>
                ))}
              </div>
            </div>
          )}

        </div>
      </main>
    </div>
  );
}
