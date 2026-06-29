import { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ChevronRight } from "lucide-react";

interface Breadcrumb {
  label: string;
  href?: string;
}

interface PageLayoutProps {
  title: string;
  description?: string;
  breadcrumbs?: Breadcrumb[];
  actionButton?: ReactNode;
  children: ReactNode;
}

export default function PageLayout({ title, description, breadcrumbs, actionButton, children }: PageLayoutProps) {
  return (
    <div className="flex-1 flex flex-col h-full overflow-hidden bg-[#fafafa]">
      
      <div className="flex h-14 items-center px-6 md:px-8 border-b border-[#e5e5e5] bg-white shrink-0 z-20">
        <div className="max-w-6xl mx-auto w-full flex items-center justify-between">
          <nav className="flex items-center gap-1.5 shrink-0 pr-4">
            {breadcrumbs && breadcrumbs.map((bc, idx) => (
              <div key={idx} className="flex items-center gap-1.5">
                {bc.href ? (
                  <Link 
                    to={bc.href} 
                    className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] hover:text-[#09090b] transition-colors"
                  >
                    {bc.label}
                  </Link>
                ) : (
                  <span className="text-[10px] font-bold uppercase tracking-widest text-[#09090b]">
                    {bc.label}
                  </span>
                )}
                {idx < breadcrumbs.length - 1 && (
                  <ChevronRight size={12} className="text-[#d4d4d8] shrink-0" />
                )}
              </div>
            ))}
          </nav>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-6 md:p-8">
        <div className="max-w-6xl mx-auto w-full">
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 mb-8 pb-6 border-b border-[#e5e5e5]">
            <div className="space-y-1.5 max-w-2xl">
              <h1 className="text-xl font-bold text-[#09090b] tracking-tight">
                {title}
              </h1>
              {description && (
                <p className="text-[13px] text-[#71717a] leading-normal">
                  {description}
                </p>
              )}
            </div>
            {actionButton && (
              <div className="shrink-0 pt-0.5">
                {actionButton}
              </div>
            )}
          </div>

          <div className="w-full">
            {children}
          </div>
        </div>
      </div>
    </div>
  );
}
