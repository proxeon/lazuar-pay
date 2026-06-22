import { useEffect, useState } from "react";
import { Outlet, Link, useLocation, useNavigate } from "react-router-dom";
import { User, Shield, CreditCard, LogOut, Loader2, LayoutGrid } from "lucide-react";
import { client, AUTH_URL } from "../lib/api-client";
import { cn } from "../lib/utils";

export default function IdentityHubLayout() {
  const [isLoading, setIsLoading] = useState(true);
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    async function verifySession() {
      try {
        const { data, error } = await client.GET("/one/auth/me");
        if (error || !data) {
          window.location.href = `${AUTH_URL}/login?returnUrl=${encodeURIComponent(window.location.href)}`;
          return;
        }
        setIsLoading(false);
      } catch {
        window.location.href = `${AUTH_URL}/login?returnUrl=${encodeURIComponent(window.location.href)}`;
      }
    }
    verifySession();
  }, []);

  const handleLogout = async () => {
    await client.POST("/one/auth/logout");
    window.location.href = `${AUTH_URL}/login`;
  };

  const navLinks = [
    { label: "Dashboard", href: "/hub", icon: LayoutGrid },
    { label: "Global Profile", href: "/settings/profile", icon: User },
    { label: "Security & Password", href: "/settings/security", icon: Shield }
  ];

  if (isLoading) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5]">
        <Loader2 className="animate-spin text-[#a1a1aa]" />
      </div>
    );
  }

  return (
    <div className="flex min-h-screen w-full bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      {/* Sidebar */}
      <aside className="w-64 shrink-0 border-r border-[#e5e5e5] bg-white flex flex-col hidden md:flex">
        <div className="h-14 flex items-center px-6 border-b border-[#e5e5e5] shrink-0">
          <span className="text-[14px] font-bold tracking-tight text-[#09090b]">Identity Hub</span>
        </div>
        <div className="flex-1 py-6 px-4 space-y-1">
          {navLinks.map((link) => {
            const isActive = location.pathname === link.href;
            return (
              <Link
                key={link.href}
                to={link.href}
                className={cn(
                  "flex items-center gap-3 px-3 py-2 text-[13px] font-medium rounded-sm transition-colors",
                  isActive ? "bg-[#f4f4f5] text-[#09090b]" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]"
                )}
              >
                <link.icon size={16} />
                {link.label}
              </Link>
            );
          })}
        </div>
        <div className="p-4 border-t border-[#e5e5e5]">
          <button
            onClick={handleLogout}
            className="flex w-full items-center gap-3 px-3 py-2 text-[13px] font-medium text-rose-600 rounded-sm hover:bg-rose-50 transition-colors"
          >
            <LogOut size={16} />
            Log Out
          </button>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 flex flex-col overflow-hidden">
        {/* Mobile Header */}
        <header className="h-14 md:hidden border-b border-[#e5e5e5] bg-white flex items-center justify-between px-4 shrink-0">
          <span className="text-[14px] font-bold tracking-tight text-[#09090b]">Identity Hub</span>
          <button onClick={handleLogout} className="p-2 text-rose-600">
            <LogOut size={18} />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto p-4 md:p-8">
          <div className="max-w-4xl mx-auto w-full">
            <Outlet />
          </div>
        </div>
      </main>
    </div>
  );
}
