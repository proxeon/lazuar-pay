import { useEffect } from "react";

import { cn } from "../../lib/utils";
import { useIsMobile } from "../../hooks/use-mobile";
import { AppSidebarNavItem } from "./nav-item";
import { AppSidebarUserMenu } from "./user-menu";
import type { AppSidebarProps } from "./types";

/**
 * Ops day rail — sky brand slab with light type (HRMS console structure, Aura sky).
 * Presentational — apps inject header + navigation handlers.
 */
export function AppSidebar({
  navGroups,
  pathname,
  onNavigate,
  user,
  onLogout,
  onProfileClick,
  onSettingsClick,
  isOpen = true,
  onClose,
  header,
  className,
}: AppSidebarProps) {
  const isMobile = useIsMobile();

  useEffect(() => {
    if (isMobile && isOpen) {
      onClose?.();
    }
    // Only auto-close when the route changes on mobile
    // eslint-disable-next-line react-hooks/exhaustive-deps -- intentional: pathname-driven close
  }, [pathname]);

  return (
    <aside
      className={cn(
        "fixed inset-y-0 left-0 z-40 flex w-[17.5rem] flex-col border-r border-sky-700 bg-sky-600 text-white transition-transform duration-300 ease-in-out dark:border-sky-800 dark:bg-sky-700 md:relative md:z-0 md:w-64 md:translate-x-0",
        isOpen
          ? "translate-x-0 shadow-2xl shadow-sky-900/20 md:shadow-none"
          : "-translate-x-full",
        className,
      )}
    >
      <div className="shrink-0 px-3 pb-2 pt-4 md:px-3.5 md:pt-5">{header}</div>

      <div className="flex-1 overflow-y-auto px-2.5 pb-4 pt-2 [scrollbar-width:none] md:px-3">
        <div className="space-y-6">
          {navGroups.map((group, groupIndex) => (
            <div key={group.label ?? groupIndex}>
              {group.label ? (
                <h4 className="mb-2 px-2.5 text-[10px] font-semibold uppercase tracking-wider text-sky-100/80">
                  {group.label}
                </h4>
              ) : null}
              <nav
                className="flex flex-col gap-0.5"
                aria-label={group.label ?? "Navigation"}
              >
                {group.items.map((item) => (
                  <AppSidebarNavItem
                    key={item.path}
                    item={item}
                    pathname={pathname}
                    onNavigate={onNavigate}
                    onClick={onClose}
                  />
                ))}
              </nav>
            </div>
          ))}
        </div>
      </div>

      <div className="shrink-0 border-t border-sky-500/60 p-2 dark:border-sky-600/60">
        <AppSidebarUserMenu
          user={user}
          onLogout={onLogout}
          onProfileClick={onProfileClick}
          onSettingsClick={onSettingsClick}
        />
      </div>
    </aside>
  );
}
