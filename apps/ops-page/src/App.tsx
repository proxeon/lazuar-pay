import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet, useNavigate, useLocation } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import Sidebar from "./components/Sidebar";
import OpsChatWorkspace from "./components/OpsChatWorkspace";
import LoginPage from "./components/LoginPage";
import PaymentSettingsPage from "./components/PaymentSettingsPage";
import CommunityInsights from "./components/CommunityInsights";
import ConversationsDirectory from "./components/ConversationsDirectory";
import { client, type AuthUser, type EntitlementDto } from "./lib/api-client";

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

        if (data.role === "SUPER_ADMIN") {
          window.location.href = "http://localhost:3000/dashboard";
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
    navigate("/history");
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
        <p className="text-[12px] text-[#71717a] max-w-sm text-center">
          Your application is currently pending review by a system administrator. Check back later.
        </p>
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
      <Route path="/login" element={<LoginPage />} />
      <Route element={<OpsLayout />}>
        <Route path="/" element={<Navigate to="/chat" replace />} />
        <Route path="/chat" element={<OpsChatWorkspace />} />
        <Route path="/chat/:id" element={<OpsChatWorkspace />} />
        <Route path="/history" element={<ConversationsDirectory />} />
        <Route path="/insights" element={<CommunityInsights />} />
        <Route path="/settings/payment" element={<PaymentSettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/chat" replace />} />
    </Routes>
  );
}
