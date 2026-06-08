import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import Dashboard from "./components/Dashboard";
import Users from "./components/Users";
import type { MockUser } from "./components/Users";
import UserDetailsPage from "./components/UserDetailsPage";
import Onboard from "./components/Onboard";
import type { OnboardingRequest } from "./components/Onboard";
import OnboardDetailsPage from "./components/OnboardDetailsPage";
import LoginPage from "./components/LoginPage";
import Tenants from "./components/Tenants";
import { Toaster } from "sonner";
import { client, type AuthUser } from "./lib/api-client";

const SIDEBAR_STATE_KEY = "one_admin_sidebar_state";

export default function App() {
  const [isMobile, setIsMobile] = useState(false);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isAuthLoading, setIsAuthLoading] = useState(true);

  const [users, setUsers] = useState<MockUser[]>([]);
  const [pendingRequests, setPendingRequests] = useState<OnboardingRequest[]>([]);

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
        if (data && !error && data.is_system_admin) {
          if (window.location.pathname === "/login") {
            const searchParams = new URLSearchParams(window.location.search);
            const returnUrl = searchParams.get("returnUrl");
            if (returnUrl) {
              window.location.href = returnUrl;
              return;
            }
          }
          setUser(data);
        }
      } catch (err) {
        // Silent fail
      } finally {
        setIsAuthLoading(false);
      }
    }
    verifySession();
  }, []);

  async function handleLogout() {
    await client.POST("/one/auth/logout");
    setUser(null);
  }

  if (isAuthLoading) return <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Loading Environment...</div>;

  if (!user) return <LoginPage onLogin={setUser} />;

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      <Sidebar isOpen={isSidebarOpen} setIsOpen={setIsSidebarOpen} isMobile={isMobile} user={user} onLogout={handleLogout} />
      
      <main className="flex-1 flex flex-col overflow-y-auto w-full relative">
        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<Dashboard isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/workspaces" element={<Tenants isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/users" element={<Users users={users} setUsers={setUsers} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/users/:id" element={<UserDetailsPage users={users} setUsers={setUsers} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/onboard" element={<Onboard pendingRequests={pendingRequests} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/onboard/:id" element={<OnboardDetailsPage pendingRequests={pendingRequests} setPendingRequests={setPendingRequests} setUsers={setUsers} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/login" element={<Navigate to="/dashboard" replace />} />
        </Routes>
        
        {isMobile && isSidebarOpen && <div className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm" onClick={() => setIsSidebarOpen(false)} />}
      </main>
      
      <Toaster position="bottom-right" richColors theme="light" closeButton />
    </div>
  );
}
