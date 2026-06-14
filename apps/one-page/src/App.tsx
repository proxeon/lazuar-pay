import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet, useNavigate, useLocation } from "react-router-dom";
import { Toaster } from "sonner";
import Sidebar from "./components/Sidebar";
import LoginPage from "./components/LoginPage";
import Launchpad from "./components/Launchpad";
import Profile from "./components/Profile";
import Security from "./components/Security";
import { client, type AuthUser } from "./lib/api-client";

const SIDEBAR_STATE_KEY = "one_page_sidebar_state";

function PrivateLayout() {
  const [isMobile, setIsMobile] = useState(false);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  
  const navigate = useNavigate();
  const location = useLocation();

  const [isSidebarOpen, setIsSidebarOpen] = useState(() => {
    if (typeof window !== "undefined") {
      const isMobileViewport = window.innerWidth < 768;
      if (isMobileViewport) return false;
      
      const savedState = localStorage.getItem(SIDEBAR_STATE_KEY);
      return savedState === "collapsed" ? false : true;
    }
    return true;
  });

  useEffect(() => {
    const checkMobile = () => {
      const isMobileViewport = window.innerWidth < 768;
      setIsMobile(isMobileViewport);
      if (isMobileViewport) setIsSidebarOpen(false);
      else setIsSidebarOpen(localStorage.getItem(SIDEBAR_STATE_KEY) !== "collapsed");
    };
    checkMobile();
    window.addEventListener('resize', checkMobile);
    return () => window.removeEventListener('resize', checkMobile);
  }, []);

  useEffect(() => {
    if (!isMobile) localStorage.setItem(SIDEBAR_STATE_KEY, isSidebarOpen ? "expanded" : "collapsed");
  }, [isSidebarOpen, isMobile]);

  useEffect(() => {
    async function verifySession() {
      try {
        const { data, error } = await client.GET("/one/auth/me");
        
        if (error || !data) {
          const returnUrl = encodeURIComponent(location.pathname + location.search);
          navigate(`/login?returnUrl=${returnUrl}`);
          return;
        }

        // Prevent Superadmins from accessing the client-facing portal
        if (data.role === "SUPER_ADMIN") {
          window.location.href = "http://localhost:3000/dashboard";
          return;
        }

        setUser(data);
      } catch (err) {
        navigate("/login");
      } finally {
        setIsLoading(false);
      }
    }
    verifySession();
  }, [navigate, location.pathname, location.search]);

  const handleLogout = async () => {
    await client.POST("/one/auth/logout");
    window.location.href = "/login";
  };

  if (isLoading) {
    return <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Verifying Identity...</div>;
  }

  if (!user) return null;

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a] relative">
      <Sidebar isOpen={isSidebarOpen} setIsOpen={setIsSidebarOpen} isMobile={isMobile} user={user} onLogout={handleLogout} />
      
      <main className="flex-1 flex flex-col overflow-y-auto w-full relative">
        <Outlet context={{ user }} />
        
        {isMobile && isSidebarOpen && (
          <div 
            className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm transition-opacity duration-150" 
            onClick={() => setIsSidebarOpen(false)}
          />
        )}
      </main>
    </div>
  );
}

export default function App() {
  return (
    <>
      <Routes>
        <Route path="/login" element={<LoginPage />} />

        <Route element={<PrivateLayout />}>
          <Route path="/" element={<Navigate to="/launchpad" replace />} />
          <Route path="/launchpad" element={<Launchpad />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/security" element={<Security />} />
        </Route>

        <Route path="*" element={<Navigate to="/launchpad" replace />} />
      </Routes>
      <Toaster position="bottom-right" richColors theme="light" closeButton />
    </>
  );
}
