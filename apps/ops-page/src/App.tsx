import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet, useNavigate, useLocation } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import Sidebar from "./components/Sidebar";
import { client, AUTH_URL, OPS_URL, type AuthUser, type EntitlementDto } from "./lib/api-client";

import DashboardPage from "./modules/community/pages/DashboardPage";
import PaymentSettingsPage from "./modules/community/pages/PaymentSettingsPage";
import TemplatesPage from "./modules/community/pages/TemplatesPage";
import SubscribersPage from "./modules/community/pages/SubscribersPage";
import PlansPage from "./modules/community/pages/PlansPage";
import CouponsPage from "./modules/community/pages/CouponsPage";
import AutomationsPage from "./modules/community/pages/AutomationsPage";
import WorkspaceSettingsPage from "./modules/workspace/pages/WorkspaceSettingsPage";

function OpsLayout() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [activeWorkspaceId, setActiveWorkspaceId] = useState<string | null>(() => localStorage.getItem("ops_active_workspace_id"));
  const [isAuthLoading, setIsAuthLoading] = useState(true);
  const [isMobile, setIsMobile] = useState(false);
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => localStorage.getItem("lazuar-ops-sidebar-collapsed") !== "true");
  
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    const checkMobile = () => {
      const mobileStatus = window.innerWidth < 768;
      setIsMobile(mobileStatus);
      if (mobileStatus) setIsSidebarOpen(false);
    };
    checkMobile();
    window.addEventListener("resize", checkMobile);
    return () => window.removeEventListener("resize", checkMobile);
  }, []);

  useEffect(() => {
    async function verifySession() {
      try {
        const { data, error } = await client.GET("/one/auth/me");
        if (error || !data) {
          window.location.href = `${AUTH_URL}/login?returnUrl=${encodeURIComponent(OPS_URL + location.pathname)}`;
          return;
        }
        setUser(data);
      } catch {
        window.location.href = `${AUTH_URL}/login?returnUrl=${encodeURIComponent(OPS_URL + location.pathname)}`;
      } finally {
        setIsAuthLoading(false);
      }
    }
    verifySession();
  }, [location.pathname]);

  const { data: entitlements, isLoading: isEntitlementsLoading } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/me/entitlements");
      if (error) throw new Error(error.detail);
      return data as EntitlementDto[];
    },
    enabled: !!user
  });

  useEffect(() => {
    if (entitlements) {
      if (entitlements.length > 0) {
        const isValid = entitlements.some(e => e.workspace_id === activeWorkspaceId);
        if (!isValid) {
          setActiveWorkspaceId(entitlements[0].workspace_id);
          localStorage.setItem("ops_active_workspace_id", entitlements[0].workspace_id);
        }
      } else {
        setActiveWorkspaceId(null);
        localStorage.removeItem("ops_active_workspace_id");
      }
    }
  }, [entitlements, activeWorkspaceId]);

  const handleToggleSidebar = () => {
    setIsSidebarOpen((prev) => {
      localStorage.setItem("lazuar-ops-sidebar-collapsed", String(prev));
      return !prev;
    });
  };

  const handleWorkspaceChange = (id: string) => {
    setActiveWorkspaceId(id);
    localStorage.setItem("ops_active_workspace_id", id);
    navigate("/community/dashboard");
  };

  const handleLogout = async () => {
    await client.POST("/one/auth/logout");
    localStorage.removeItem("ops_active_workspace_id");
    window.location.href = `${AUTH_URL}/login`;
  };

  if (isAuthLoading || (user && isEntitlementsLoading)) {
    return <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Loading Environment...</div>;
  }

  if (user && entitlements?.length === 0) {
    return (
      <div className="flex h-screen w-full flex-col items-center justify-center bg-[#f5f5f5] gap-4">
        <span className="text-[11px] font-bold uppercase tracking-widest text-rose-600">
          Access Denied: No active workspace entitlements found.
        </span>
        <button 
          onClick={handleLogout} 
          className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none hover:bg-[#27272a] transition-colors"
        >
          Log Out
        </button>
      </div>
    );
  }

  if (!user) return null;

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      <Sidebar
        isOpen={isSidebarOpen}
        setIsOpen={handleToggleSidebar}
        isMobile={isMobile}
        user={user}
        entitlements={entitlements || []}
        activeWorkspaceId={activeWorkspaceId}
        onWorkspaceSelect={handleWorkspaceChange}
        onLogout={handleLogout}
      />
      
      <main className="flex-1 flex flex-col overflow-hidden w-full relative bg-white">
        <Outlet context={{ activeWorkspaceId }} />
        {isMobile && isSidebarOpen && (
          <div className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm" onClick={handleToggleSidebar} />
        )}
      </main>
    </div>
  );
}

export default function App() {
  return (
    <Routes>
      <Route element={<OpsLayout />}>
        <Route path="/" element={<Navigate to="/community/dashboard" replace />} />
        
        <Route path="/community/dashboard" element={<DashboardPage />} />
        <Route path="/community/subscribers" element={<SubscribersPage />} />
        <Route path="/community/plans" element={<PlansPage />} />
        <Route path="/community/coupons" element={<CouponsPage />} />
        <Route path="/community/automations" element={<AutomationsPage />} />
        <Route path="/community/payment" element={<PaymentSettingsPage />} />
        <Route path="/community/templates" element={<TemplatesPage />} />

        <Route path="/workspace/settings" element={<WorkspaceSettingsPage />} />
      </Route>
      
      <Route path="*" element={<Navigate to="/community/dashboard" replace />} />
    </Routes>
  );
}
