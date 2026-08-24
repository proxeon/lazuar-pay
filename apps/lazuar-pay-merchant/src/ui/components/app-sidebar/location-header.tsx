import { cn } from "../../lib/utils";

/**
 * Static location / org header for Storybook on sky rail.
 * Live app uses LocationSwitcher with matching translucent styles.
 */
export function AppSidebarLocationHeader({
  orgName,
  locationName,
  className,
}: {
  orgName: string;
  locationName: string;
  /** @deprecated unused — icon tile removed from header */
  locked?: boolean;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex w-full items-center rounded-lg border border-white/20 bg-white/10 px-3 py-2.5 shadow-none",
        className,
      )}
    >
      <div className="flex min-w-0 flex-col text-left">
        <span className="truncate text-[11px] font-medium leading-tight text-sky-100/85">
          {orgName}
        </span>
        <span className="truncate text-sm font-semibold leading-snug text-white">
          {locationName}
        </span>
      </div>
    </div>
  );
}
