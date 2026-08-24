import { ChevronsUpDown, LogOut, Settings } from "lucide-react";

import { Avatar, AvatarFallback, AvatarImage } from "../avatar";
import { Button } from "../button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../dropdown-menu";
import type { AppSidebarUser } from "./types";

/**
 * Avatar account menu on sky day rail.
 * Trigger uses light-on-sky; dropdown stays light paper for readability.
 */
export function AppSidebarUserMenu({
  user,
  onLogout,
  onSettingsClick,
}: {
  user?: AppSidebarUser | null;
  onLogout?: () => void;
  /** @deprecated unused — profile is only under Settings nav */
  onProfileClick?: () => void;
  onSettingsClick?: () => void;
}) {
  const initials =
    user?.name?.substring(0, 2).toUpperCase() ||
    user?.email?.substring(0, 2).toUpperCase() ||
    "US";

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="group relative flex h-auto w-full items-center justify-between gap-3 rounded-lg border border-white/20 bg-white/10 px-3 py-2 text-left text-white shadow-none transition-colors hover:bg-white/15 hover:text-white data-[state=open]:bg-white/15 data-[state=open]:text-white"
        >
          <div className="flex min-w-0 items-center gap-2.5 overflow-hidden">
            <Avatar className="h-8 w-8 rounded-full border border-white/30 shadow-sm">
              {user?.imageUrl ? (
                <AvatarImage
                  src={user.imageUrl}
                  alt={user?.name ?? "User"}
                  className="object-cover"
                />
              ) : null}
              <AvatarFallback className="rounded-full bg-white/20 text-[11px] font-semibold text-white">
                {initials}
              </AvatarFallback>
            </Avatar>
            <div className="flex min-w-0 flex-col truncate">
              <span className="truncate text-sm font-semibold text-white">
                {user?.name || "Signed in"}
              </span>
              <span className="truncate text-xs font-medium text-sky-100/85">
                {user?.roleLabel || "Staff"}
              </span>
            </div>
          </div>
          <ChevronsUpDown className="h-4 w-4 shrink-0 text-sky-100/70 transition-colors group-hover:text-white" />
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent
        className="w-[var(--radix-dropdown-menu-trigger-width)] min-w-56 rounded-lg border-slate-200 bg-white p-1.5 text-slate-900 shadow-lg dark:border-slate-700 dark:bg-slate-900 dark:text-slate-50"
        side="top"
        align="start"
        sideOffset={4}
      >
        <div className="rounded-md bg-slate-50 px-3 py-2.5 dark:bg-slate-800/60">
          <p className="text-xs font-medium text-slate-500 dark:text-slate-400">
            Account
          </p>
          <p className="mt-0.5 truncate text-sm font-medium text-slate-900 dark:text-slate-100">
            {user?.email || "—"}
          </p>
        </div>
        <DropdownMenuSeparator className="my-1.5" />
        {onSettingsClick ? (
          <DropdownMenuGroup>
            <DropdownMenuItem
              onClick={onSettingsClick}
              className="cursor-pointer rounded-md px-2.5 py-2 text-sm font-medium"
            >
              <Settings className="mr-2.5 h-4 w-4 text-slate-400" />
              Settings
            </DropdownMenuItem>
          </DropdownMenuGroup>
        ) : null}
        {onSettingsClick ? (
          <DropdownMenuSeparator className="my-1.5" />
        ) : null}
        <DropdownMenuItem
          onClick={onLogout}
          className="cursor-pointer rounded-md px-2.5 py-2 text-sm font-medium text-red-600 focus:bg-red-50 focus:text-red-600 dark:text-red-400 dark:focus:bg-red-950/40 dark:focus:text-red-400"
        >
          <LogOut className="mr-2.5 h-4 w-4" />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
