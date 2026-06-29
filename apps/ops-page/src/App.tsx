import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet, useNavigate, useLocation } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import Sidebar from "./components/Sidebar";
import LoginPage from "./components/LoginPage";
import { client, type AuthUser, type EntitlementDto } from "./lib/api-client";

import DashboardPage from "./modules/commerce/pages/DashboardPage";
import ProductsPage from "./modules/commerce/pages/ProductsPage";
import SubscribersPage from "./modules/commerce/pages/SubscribersPage";
import TransactionsPage from "./modules/commerce/pages/TransactionsPage";
import CouponsPage from "./modules/commerce/pages/CouponsPage";
import PaymentSettingsPage from "./modules/commerce/pages/PaymentSettingsPage";
import DunningCampaignsPage from "./modules/commerce/pages/DunningCampaignsPage";

import SpacesPage from "./modules/community/pages/SpacesPage";
import BroadcastsPage from "./modules/community/pages/BroadcastsPage";
import TemplatesPage from "./modules/community/pages/TemplatesPage";

import DigitalProductsPage from "./modules/vault/pages/DigitalProductsPage";

import GeneralSettingsPage from "./modules/workspace/pages/GeneralSettingsPage";
import DeveloperSettingsPage from "./modules/workspace/pages/DeveloperSettingsPage";
import BillingSettingsPage from "./modules/workspace/pages/BillingSettingsPage";

export interface OpsOutletContext {
  activeWorkspaceId: string | null;
  entitlements: EntitlementDto[];
  onWorkspaceSelect: (id: string) => void;
}

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
          navigate(`/login?returnUrl=${encodeURIComponent(location.pathname)}`);
          return;
        }
        setUser(data);
      } catch {
        navigate("/login");
      } finally {
        setIsAuthLoading(false);
      }
    }
    verifySession();
  }, [navigate, location.pathname]);

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
    if (entitlements && entitlements.length > 0) {
      const isValid = entitlements.some(e => e.workspace_id === activeWorkspaceId);
      if (!isValid) {
        localStorage.setItem("ops_active_workspace_id", entitlements[0].workspace_id);
        setActiveWorkspaceId(entitlements[0].workspace_id);
      }
    } else if (entitlements?.length === 0) {
      localStorage.removeItem("ops_active_workspace_id");
      setActiveWorkspaceId(null);
    }
  }, [entitlements, activeWorkspaceId]);

  const handleToggleSidebar = () => {
    setIsSidebarOpen((prev) => {
      localStorage.setItem("lazuar-ops-sidebar-collapsed", String(prev));
      return !prev;
    });
  };

  const handleWorkspaceChange = (id: string) => {
    localStorage.setItem("ops_active_workspace_id", id);
    setActiveWorkspaceId(id);
    navigate("/commerce/dashboard");
  };

  const handleLogout = async () => {
    await client.POST("/one/auth/logout");
    localStorage.removeItem("ops_active_workspace_id");
    navigate("/login");
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

  if (user && entitlements && entitlements.length > 0 && !activeWorkspaceId) {
    return <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Initializing Workspace Context...</div>;
  }

  if (!user) return null;

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      <Sidebar
        isOpen={isSidebarOpen}
        setIsOpen={handleToggleSidebar}
        isMobile={isMobile}
        user={user}
        onLogout={handleLogout}
      />
      
      <main className="flex-1 flex flex-col overflow-hidden w-full relative bg-white">
        <Outlet context={{ 
          activeWorkspaceId,
          entitlements: entitlements || [],
          onWorkspaceSelect: handleWorkspaceChange
        }} />
        
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
      <Route path="/login" element={<LoginPage />} />
      <Route element={<OpsLayout />}>
        <Route path="/" element={<Navigate to="/commerce/dashboard" replace />} />
        
        <Route path="/commerce/dashboard" element={<DashboardPage />} />
        <Route path="/commerce/products" element={<ProductsPage />} />
        <Route path="/commerce/subscribers" element={<SubscribersPage />} />
        <Route path="/commerce/transactions" element={<TransactionsPage />} />
        <Route path="/commerce/coupons" element={<CouponsPage />} />
        <Route path="/commerce/dunning-campaigns" element={<DunningCampaignsPage />} />
        <Route path="/commerce/payment" element={<PaymentSettingsPage />} />

        <Route path="/community/spaces" element={<SpacesPage />} />
        <Route path="/community/broadcasts" element={<BroadcastsPage />} />
        <Route path="/community/templates" element={<TemplatesPage />} />

        <Route path="/vault/products" element={<DigitalProductsPage />} />

        <Route path="/developer/webhooks" element={<DeveloperSettingsPage />} />
        
        <Route path="/workspace/general" element={<GeneralSettingsPage />} />
        <Route path="/workspace/billing" element={<BillingSettingsPage />} />
      </Route>
      
      <Route path="*" element={<Navigate to="/commerce/dashboard" replace />} />
    </Routes>
  );
}
