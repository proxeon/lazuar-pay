import { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

export default function SettingsLayout({ children }: { children: ReactNode }) {
  const location = useLocation();

  const tabs = [
    { label: "Payment Gateway", href: "/community/settings/payment" },
    { label: "Message Templates", href: "/community/settings/templates" }
  ];

  return (
    <PageLayout 
      title="Module Configuration" 
      description="Manage low-level integrations and automated copy for the Community module."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Settings" }]}
    >
      <div className="flex flex-col md:flex-row gap-8 items-start">
        <aside className="w-full md:w-56 shrink-0 flex flex-col gap-1">
          {tabs.map((tab) => {
            const isActive = location.pathname === tab.href;
            return (
              <Link
                key={tab.href}
                to={tab.href}
                className={cn(
                  "px-4 py-2.5 text-[12px] font-bold uppercase tracking-widest transition-colors rounded-none border-l-2",
                  isActive ? "border-[#09090b] bg-[#f4f4f5] text-[#09090b]" : "border-transparent text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]"
                )}
              >
                {tab.label}
              </Link>
            );
          })}
        </aside>

        <div className="flex-1 w-full min-w-0">
          {children}
        </div>
      </div>
    </PageLayout>
  );
}
