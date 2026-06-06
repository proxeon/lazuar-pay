import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import Dashboard from "./components/Dashboard";
import Plans from "./components/Plans";
import PlanForm from "./components/PlanForm";
import Subscribers from "./components/Subscribers";
import Settings from "./components/Settings";
import Communications from "./components/Communications";
import { Toaster } from "sonner";
import { client } from "./lib/api-client";

interface User {
  email: string;
  name?: string;
  role: string;
}

const SIDEBAR_STATE_KEY = "community_admin_sidebar_state";

export default function App() {
  const [isMobile, setIsMobile] = useState(false);
  
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => {
    if (typeof window !== "undefined") {
      const isMobileViewport = window.innerWidth < 768;
      if (isMobileViewport) return false;
      const savedState = localStorage.getItem(SIDEBAR_STATE_KEY);
      return savedState === "collapsed" ? false : true;
    }
    return true;
  });

  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

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
    if (!isMobile) {
      localStorage.setItem(SIDEBAR_STATE_KEY, isSidebarOpen ? "expanded" : "collapsed");
    }
  }, [isSidebarOpen, isMobile]);

  // Centralized Admin SSO Check
  useEffect(() => {
    async function verifySession() {
      try {
        const { data, error } = await client.GET("/one/auth/me");
        if (error || !data) {
          throw new Error("Unauthorized");
        }
        setUser({
          email: data.email,
          name: data.name,
          role: data.role
        });
        setIsLoading(false);
      } catch (err) {
        // Unauthenticated -> Hard redirect to Lazuar One Admin
        const returnUrl = encodeURIComponent(window.location.href);
        window.location.href = `http://localhost:3000/login?returnUrl=${returnUrl}`;
      }
    }
    
    verifySession();
  }, []);

  async function handleLogout() {
    await client.POST("/one/auth/logout");
    window.location.href = "http://localhost:3000/login";
  }

  if (isLoading || !user) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-zinc-50 dark:bg-black text-foreground">
        <div className="text-sm font-medium uppercase tracking-widest text-muted-foreground">Authenticating Session...</div>
      </div>
    );
  }

  return (
    <div className="flex h-screen w-full overflow-hidden bg-zinc-50 dark:bg-black font-sans text-foreground relative">
      <Sidebar
        isOpen={isSidebarOpen}
        setIsOpen={setIsSidebarOpen}
        isMobile={isMobile}
        user={user}
        onLogout={handleLogout}
      />

      <main className="flex-1 flex flex-col overflow-y-auto w-full relative">
        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<Dashboard isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/plans" element={<Plans isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/plans/new" element={<PlanForm />} />
          <Route path="/plans/:id/edit" element={<PlanForm />} />
          <Route path="/subscribers" element={<Subscribers isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/communications" element={<Communications isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/settings" element={<Settings isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
        </Routes>

        {isMobile && isSidebarOpen && (
          <div className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm transition-opacity duration-150" onClick={() => setIsSidebarOpen(false)} />
        )}
      </main>
      <Toaster position="bottom-right" richColors theme="light" closeButton />
    </div>
  );
}
