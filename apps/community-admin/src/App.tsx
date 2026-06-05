import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import Dashboard from "./components/Dashboard";
import Plans from "./components/Plans";
import PlanForm from "./components/PlanForm";
import Subscribers from "./components/Subscribers";
import Settings from "./components/Settings";
import Communications from "./components/Communications";
import LoginPage from "./components/LoginPage";
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
  
  // Initialize state from localStorage on desktop viewports
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

  // Monitor screen resizing and adjust sidebar responsiveness
  useEffect(() => {
    const checkMobile = () => {
      const isMobileViewport = window.innerWidth < 768;
      setIsMobile(isMobileViewport);
      
      if (isMobileViewport) {
        setIsSidebarOpen(false);
      } else {
        const savedState = localStorage.getItem(SIDEBAR_STATE_KEY);
        setIsSidebarOpen(savedState === "collapsed" ? false : true);
      }
    };
    checkMobile();
    window.addEventListener('resize', checkMobile);
    return () => window.removeEventListener('resize', checkMobile);
  }, []);

  // Persist sidebar state changes on desktop views
  useEffect(() => {
    if (!isMobile) {
      localStorage.setItem(
        SIDEBAR_STATE_KEY,
        isSidebarOpen ? "expanded" : "collapsed"
      );
    }
  }, [isSidebarOpen, isMobile]);

  // Check Session via HttpOnly Cookie automatically
  useEffect(() => {
    async function verifySession() {
      try {
        const { data, error } = await client.GET("/platform/auth/me");
        if (data && !error) {
          setUser({
            email: data.email,
            name: data.name,
            role: data.role
          });
        }
      } catch (err) {
        // Silently fail, user remains null, prompting LoginPage
      } finally {
        setIsLoading(false);
      }
    }
    
    verifySession();
  }, []);

  function handleLogin(userData: User) {
    setUser(userData);
  }

  async function handleLogout() {
    try {
      await client.POST("/platform/auth/logout");
    } catch (e) {
      console.error("Logout request failed", e);
    } finally {
      setUser(null);
    }
  }

  if (isLoading) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-zinc-50 dark:bg-black text-foreground">
        <div className="text-sm font-medium uppercase tracking-widest text-muted-foreground">Loading...</div>
      </div>
    );
  }

  if (!user) {
    return <LoginPage onLogin={handleLogin} />;
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
          <div 
            className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm transition-opacity duration-150" 
            onClick={() => setIsSidebarOpen(false)} 
          />
        )}
      </main>

      <Toaster position="bottom-right" richColors theme="light" closeButton />
    </div>
  );
}
