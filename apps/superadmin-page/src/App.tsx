import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet, useNavigate, useLocation } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import LoginPage from "./components/LoginPage";
import { client, type AuthUser } from "./lib/api-client";
import PlatformPaymentSettingsPage from "./modules/platform/pages/PlatformPaymentSettingsPage";

function SuperadminLayout() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isAuthLoading, setIsAuthLoading] = useState(true);
  const [isMobile, setIsMobile] = useState(false);
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => localStorage.getItem("lazuar-admin-sidebar-collapsed") !== "true");
  
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

        if (data.role !== "SUPER_ADMIN") {
          await client.POST("/one/auth/logout");
          navigate("/login");
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

  const handleToggleSidebar = () => {
    setIsSidebarOpen((prev) => {
      localStorage.setItem("lazuar-admin-sidebar-collapsed", String(prev));
      return !prev;
    });
  };

  const handleLogout = async () => {
    await client.POST("/one/auth/logout");
    navigate("/login");
  };

  if (isAuthLoading) {
    return <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Loading Environment...</div>;
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
        <Outlet />
        
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
      <Route element={<SuperadminLayout />}>
        <Route path="/" element={<Navigate to="/platform/gateways" replace />} />
        <Route path="/platform/gateways" element={<PlatformPaymentSettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/platform/gateways" replace />} />
    </Routes>
  );
}
