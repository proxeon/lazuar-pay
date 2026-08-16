import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet, useNavigate, useLocation } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import Sidebar from "./components/Sidebar";
import LoginPage from "./components/LoginPage";
import PricingPage from "./components/PricingPage";
import EmptyWorkspaceState from "./components/EmptyWorkspaceState";
import { client, type AuthUser, type EntitlementDto } from "./lib/api-client";

import DashboardPage from "./modules/commerce/pages/DashboardPage";
import ProductsPage from "./modules/commerce/pages/ProductsPage";
import SubscribersPage from "./modules/commerce/pages/SubscribersPage";
import TransactionsPage from "./modules/commerce/pages/TransactionsPage";
import CouponsPage from "./modules/commerce/pages/CouponsPage";
import DunningCampaignsPage from "./modules/commerce/pages/DunningCampaignsPage";
import CampaignBuilderPage from "./modules/commerce/pages/CampaignBuilderPage";
import TemplatesPage from "./modules/commerce/pages/TemplatesPage";

import GeneralSettingsPage from "./modules/workspace/pages/GeneralSettingsPage";
import DeveloperSettingsPage from "./modules/workspace/pages/DeveloperSettingsPage";
import DeliveryLogsPage from "./modules/workspace/pages/DeliveryLogsPage";
import ApiKeysPage from "./modules/workspace/pages/ApiKeysPage";
import BillingSettingsPage from "./modules/workspace/pages/BillingSettingsPage";
import BillingProfilePage from "./modules/workspace/pages/BillingProfilePage";
import UtilityLedgerPage from "./modules/workspace/pages/UtilityLedgerPage";
import PaymentSettingsPage from "./modules/workspace/pages/PaymentSettingsPage";
import EmailSettingsPage from "./modules/workspace/pages/EmailSettingsPage";
import TeamPage from "./modules/workspace/pages/TeamPage";
import AuditLogPage from "./modules/workspace/pages/AuditLogPage";
import DisputesPage from "./modules/commerce/pages/DisputesPage";
import QuotesPage from "./modules/invoicing/pages/QuotesPage";
import TaxInvoicesPage from "./modules/invoicing/pages/TaxInvoicesPage";
import CreditNotesPage from "./modules/invoicing/pages/CreditNotesPage";

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
      <EmptyWorkspaceState
        onWorkspaceCreated={handleWorkspaceChange}
        onLogout={handleLogout}
      />
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

/**
 * Ops routes (Pure CaaS MVP — ADR 023).
 *
 * Intentionally unrouted "floating islands":
 * - components/OpsChatWorkspace + ConversationsDirectory (ops AI chat)
 * Legal & Billing profile is remounted (LP-122). Invoicing pages remounted (Wave 2).
 *
 * Re-mount by adding Route entries + Sidebar links; do not delete backends.
 * See docs/contracts/openapi-vs-minimal-api.md and ADR 023.
 */
function HomeRedirect() {
  const [dest, setDest] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data, error } = await client.GET("/one/auth/me");
        if (cancelled) return;
        setDest(!error && data ? "/commerce/dashboard" : "/pricing");
      } catch {
        if (!cancelled) setDest("/pricing");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (!dest) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] text-[11px] font-bold uppercase tracking-widest text-[#71717a]">
        Loading…
      </div>
    );
  }

  return <Navigate to={dest} replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomeRedirect />} />
      <Route path="/pricing" element={<PricingPage />} />
      <Route path="/signup" element={<LoginPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route element={<OpsLayout />}>
        <Route path="/commerce/dashboard" element={<DashboardPage />} />
        <Route path="/commerce/products" element={<ProductsPage />} />
        <Route path="/commerce/subscribers" element={<SubscribersPage />} />
        <Route path="/commerce/transactions" element={<TransactionsPage />} />
        <Route path="/commerce/disputes" element={<DisputesPage />} />
        <Route path="/commerce/coupons" element={<CouponsPage />} />
        <Route path="/commerce/dunning-campaigns" element={<DunningCampaignsPage />} />
        <Route path="/commerce/dunning-campaigns/new" element={<CampaignBuilderPage />} />
        <Route path="/commerce/dunning-campaigns/:id" element={<CampaignBuilderPage />} />
        <Route path="/commerce/templates" element={<TemplatesPage />} />

        <Route path="/developer/api-keys" element={<ApiKeysPage />} />
        <Route path="/developer/webhooks" element={<DeveloperSettingsPage />} />
        <Route path="/developer/logs" element={<DeliveryLogsPage />} />
        
        <Route path="/workspace/general" element={<GeneralSettingsPage />} />
        <Route path="/workspace/team" element={<TeamPage />} />
        <Route path="/workspace/audit" element={<AuditLogPage />} />
        <Route path="/workspace/billing-profile" element={<BillingProfilePage />} />
        <Route path="/workspace/payment-gateways" element={<PaymentSettingsPage />} />
        <Route path="/workspace/email" element={<EmailSettingsPage />} />
        <Route path="/workspace/billing" element={<BillingSettingsPage />} />
        <Route path="/workspace/ledger" element={<UtilityLedgerPage />} />

        <Route path="/invoicing/quotes" element={<QuotesPage />} />
        <Route path="/invoicing/tax-invoices" element={<TaxInvoicesPage />} />
        <Route path="/invoicing/credit-notes" element={<CreditNotesPage />} />

        {/* [MVP-HIDE] ADR 023 — ops chat remains disconnected
        <Route path="/ops/chat" element={<OpsChatWorkspace />} />
        */}
      </Route>
      
      <Route path="*" element={<Navigate to="/commerce/dashboard" replace />} />
    </Routes>
  );
}
