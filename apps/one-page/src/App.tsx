import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet } from "react-router-dom";
import { Toaster } from "sonner";
import Sidebar from "./components/Sidebar";
import LoginPage from "./components/LoginPage";
import Launchpad from "./components/Launchpad";
import Profile from "./components/Profile";
import Security from "./components/Security";

const SIDEBAR_STATE_KEY = "one_page_sidebar_state";

function PrivateLayout() {
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

  useEffect(() => {
    if (!isMobile) {
      localStorage.setItem(
        SIDEBAR_STATE_KEY,
        isSidebarOpen ? "expanded" : "collapsed"
      );
    }
  }, [isSidebarOpen, isMobile]);

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a] relative">
      <Sidebar isOpen={isSidebarOpen} setIsOpen={setIsSidebarOpen} isMobile={isMobile} />
      
      <main className="flex-1 flex flex-col overflow-y-auto w-full relative">
        <Outlet />
        
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
        {/* Public Boundary (No Sidebar) */}
        <Route path="/login" element={<LoginPage />} />

        {/* Private Boundary (Wrapped with Sidebar) */}
        <Route element={<PrivateLayout />}>
          <Route path="/" element={<Navigate to="/launchpad" replace />} />
          <Route path="/launchpad" element={<Launchpad />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/security" element={<Security />} />
        </Route>

        {/* Fallback for unknown routes */}
        <Route path="*" element={<Navigate to="/launchpad" replace />} />
      </Routes>
      
      <Toaster position="bottom-right" richColors theme="light" closeButton />
    </>
  );
}
