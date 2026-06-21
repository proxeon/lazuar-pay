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
    <div className="flex-1 flex flex-col h-full overflow-y-auto bg-[#fafafa]">
      <div className="px-6 py-5 md:px-8 border-b border-[#e5e5e5] bg-white sticky top-0 z-10">
        <div className="max-w-6xl mx-auto w-full">
          {breadcrumbs && breadcrumbs.length > 0 && (
            <nav className="flex items-center gap-1.5 mb-3">
              {breadcrumbs.map((bc, idx) => (
                <div key={idx} className="flex items-center gap-1.5">
                  {bc.href ? (
                    <Link to={bc.href} className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] hover:text-[#09090b] transition-colors">
                      {bc.label}
                    </Link>
                  ) : (
                    <span className="text-[10px] font-bold uppercase tracking-widest text-[#09090b]">
                      {bc.label}
                    </span>
                  )}
                  {idx < breadcrumbs.length - 1 && <ChevronRight size={12} className="text-[#d4d4d8]" />}
                </div>
              ))}
            </nav>
          )}
          
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h1 className="text-xl font-bold text-[#09090b] tracking-tight">{title}</h1>
              {description && <p className="text-[13px] text-[#71717a] mt-1">{description}</p>}
            </div>
            {actionButton && <div>{actionButton}</div>}
          </div>
        </div>
      </div>
      
      <div className="flex-1 p-6 md:p-8">
        <div className="max-w-6xl mx-auto w-full h-full">
          {children}
        </div>
      </div>
    </div>
  );
}
