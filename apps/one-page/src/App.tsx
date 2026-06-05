import { useState, useEffect } from "react";
import { Routes, Route, Navigate, Outlet } from "react-router-dom";
import { Toaster } from "sonner";
import Sidebar from "./components/Sidebar";
import LoginPage from "./components/LoginPage";
import Launchpad from "./components/Launchpad";
import Profile from "./components/Profile";
import Security from "./components/Security";
import Ledger from "./components/Ledger"; // <-- ADDED

// Layout wrapper for authenticated routes (includes Sidebar)
function PrivateLayout() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [isMobile, setIsMobile] = useState(false);

  useEffect(() => {
    const checkMobile = () => {
      const mobile = window.innerWidth < 768;
      setIsMobile(mobile);
      setIsSidebarOpen(!mobile);
    };
    
    checkMobile();
    window.addEventListener('resize', checkMobile);
    return () => window.removeEventListener('resize', checkMobile);
  }, []);

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      <Sidebar isOpen={isSidebarOpen} setIsOpen={setIsSidebarOpen} isMobile={isMobile} />
      
      <main className="flex-1 flex flex-col overflow-y-auto w-full relative">
        <Outlet />
        
        {isMobile && isSidebarOpen && (
          <div 
            className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm" 
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
          <Route path="/ledger" element={<Ledger />} />
        </Route>

        {/* Fallback for unknown routes */}
        <Route path="*" element={<Navigate to="/launchpad" replace />} />
      </Routes>
      
      <Toaster position="bottom-right" richColors theme="light" closeButton />
    </>
  );
}
