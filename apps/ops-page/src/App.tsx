import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";

export default function App() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [isMobile, setIsMobile] = useState(false);

  useEffect(() => {
    const checkMobile = () => {
      setIsMobile(window.innerWidth < 768);
      if (window.innerWidth < 768) {
        setIsSidebarOpen(false);
      } else {
        setIsSidebarOpen(true);
      }
    };
    
    checkMobile();
    window.addEventListener("resize", checkMobile);
    return () => window.removeEventListener("resize", checkMobile);
  }, []);

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      {/* Sidebar (We preserve the existing mechanics) */}
      <Sidebar isOpen={isSidebarOpen} setIsOpen={setIsSidebarOpen} isMobile={isMobile} />
      
      {/* Main Content Viewport */}
      <main className="flex-1 flex flex-col overflow-hidden w-full relative bg-white">
        <Routes>
          {/* Default Route redirects directly to our main Chat route */}
          <Route path="/" element={<Navigate to="/chat" replace />} />
          
          {/* Placeholder for the full-screen Ops Chat workspace (implemented in later phases) */}
          <Route 
            path="/chat" 
            element={
              <div className="flex-1 flex flex-col items-center justify-center text-sm text-[#71717a]">
                <span>Ops Chat Workspace Placeholder</span>
              </div>
            } 
          />
        </Routes>
        
        {/* Mobile Sidebar Overlay */}
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
