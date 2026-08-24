import type { ElementType, ReactNode } from "react";

export type AppSidebarUser = {
  name?: string | null;
  email?: string | null;
  imageUrl?: string | null;
  /** Display role label, e.g. "BRANCH MANAGER" */
  roleLabel?: string | null;
};

export type AppSidebarNavItem = {
  name: string;
  icon: ElementType;
  path: string;
};

export type AppSidebarNavGroup = {
  label?: string;
  items: AppSidebarNavItem[];
};

export type AppSidebarProps = {
  /** Nav groups (ops-style sections) */
  navGroups: AppSidebarNavGroup[];
  /** Current path for active state (e.g. "/overview") */
  pathname: string;
  /** Navigate handler — apps wire to router */
  onNavigate: (path: string) => void;
  /** User block in footer */
  user?: AppSidebarUser | null;
  onLogout?: () => void;
  onProfileClick?: () => void;
  onSettingsClick?: () => void;
  /** Mobile drawer open */
  isOpen?: boolean;
  onClose?: () => void;
  /**
   * Header slot (location switcher, org picker, brand).
   * Pass a static header in Storybook; apps inject real switchers.
   */
  header?: ReactNode;
  /** Optional className on <aside> */
  className?: string;
};
