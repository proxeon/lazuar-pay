import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import Dashboard from "./components/Dashboard";
import Users from "./components/Users";
import type { MockUser } from "./components/Users";
import UserDetailsPage from "./components/UserDetailsPage";
import Onboard from "./components/Onboard";
import type { OnboardingRequest } from "./components/Onboard"; // Imported as type
import OnboardDetailsPage from "./components/OnboardDetailsPage"; // Imported new page

const SIDEBAR_STATE_KEY = "one_admin_sidebar_state";

export default function App() {
  const [isMobile, setIsMobile] = useState(false);

  // 1. Elevated Global Clients Directory State
  const [users, setUsers] = useState<MockUser[]>([
    {
      id: "usr_018f3a3f-3610-73bf-baef-c07a3c3df9ee",
      name: "Ahmad Firdaus",
      email: "ahmad.firdaus@gmail.com",
      role: "CLIENT",
      isActive: true,
      authorizedApps: ["COMMUNITY", "VAULT", "ACADEMY"],
      createdAt: "2025-01-10T04:20:00Z",
    },
    {
      id: "usr_018f3a3f-3610-73bf-baef-c07a3c3df9ef",
      name: "Siti Aminah",
      email: "siti.aminah@yahoo.com",
      role: "CLIENT",
      isActive: true,
      authorizedApps: ["FUNNEL", "CONSULT", "COMMUNITY"],
      createdAt: "2025-02-05T09:15:00Z",
    },
    {
      id: "usr_018f3a3f-3610-73bf-baef-c07a3c3df9f0",
      name: "Chong Wei",
      email: "chong.wei@outlook.com",
      role: "CLIENT",
      isActive: false, 
      authorizedApps: ["VAULT"],
      createdAt: "2025-02-12T11:45:00Z",
    }
  ]);

  // 2. Elevated Global Onboarding Approvals State
  const [pendingRequests, setPendingRequests] = useState<OnboardingRequest[]>([
    {
      id: "req_018f3a3f-3610-73bf-baef-c07a3c3df9fa",
      name: "John Doe",
      email: "john.doe@gmail.com",
      requestedApps: ["FUNNEL", "COMMUNITY"],
      createdAt: "2025-02-14T10:00:00Z",
    },
    {
      id: "req_018f3a3f-3610-73bf-baef-c07a3c3df9fb",
      name: "Sarah Connor",
      email: "sarah.connor@sky.net",
      requestedApps: ["VAULT", "ACADEMY"],
      createdAt: "2025-02-15T12:30:00Z",
    }
  ]);

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
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      <Sidebar isOpen={isSidebarOpen} setIsOpen={setIsSidebarOpen} isMobile={isMobile} />
      
      <main className="flex-1 flex flex-col overflow-y-auto w-full relative">
        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<Dashboard isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          
          <Route path="/users" element={<Users users={users} setUsers={setUsers} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/users/:id" element={<UserDetailsPage users={users} setUsers={setUsers} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          
          {/* Synchronized state parameters passed to Onboard routes */}
          <Route path="/onboard" element={<Onboard pendingRequests={pendingRequests} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
          <Route path="/onboard/:id" element={<OnboardDetailsPage pendingRequests={pendingRequests} setPendingRequests={setPendingRequests} setUsers={setUsers} isMobile={isMobile} toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />} />
        </Routes>
        
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
