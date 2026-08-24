import { cn } from "../../lib/utils";
import { Button } from "../button";
import type { AppSidebarNavItem } from "./types";

function isPathActive(pathname: string, path: string) {
  if (path === "/" || path === "/overview") {
    return pathname === path || pathname === "/";
  }
  return pathname === path || pathname.startsWith(`${path}/`);
}

export function AppSidebarNavItem({
  item,
  pathname,
  onNavigate,
  onClick,
}: {
  item: AppSidebarNavItem;
  pathname: string;
  onNavigate: (path: string) => void;
  onClick?: () => void;
}) {
  const isActive = isPathActive(pathname, item.path);

  return (
    <Button
      variant="ghost"
      size="sm"
      type="button"
      aria-current={isActive ? "page" : undefined}
      onClick={() => {
        onNavigate(item.path);
        onClick?.();
      }}
      className={cn(
        "group relative h-auto w-full justify-start gap-3 rounded-md px-2.5 py-2 text-left font-normal shadow-none transition-colors duration-150",
        isActive
          ? "bg-sky-500 text-white shadow-sm hover:bg-sky-500 hover:text-white dark:bg-sky-600 dark:hover:bg-sky-600"
          : "text-sky-50/90 hover:bg-sky-500/50 hover:text-white",
      )}
    >
      <span
        className={cn(
          "flex h-8 w-8 shrink-0 items-center justify-center rounded-md transition-colors",
          isActive
            ? "bg-white/20 text-white"
            : "bg-white/10 text-sky-50/90 group-hover:bg-white/15 group-hover:text-white",
        )}
      >
        <item.icon className="h-4 w-4" />
      </span>
      <span
        className={cn(
          "truncate text-sm tracking-normal",
          isActive ? "font-semibold text-white" : "font-medium",
        )}
      >
        {item.name}
      </span>
    </Button>
  );
}
