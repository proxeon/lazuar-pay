import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function isValidReturnUrl(url: string | null | undefined): boolean {
  if (!url) return false;

  if (url.startsWith('/') && !url.startsWith('//')) {
    return true;
  }

  try {
    const parsedUrl = new URL(url, window.location.origin);
    const hostname = parsedUrl.hostname;

    if (hostname === 'localhost' || hostname === '127.0.0.1') {
      return true;
    }

    if (hostname === 'lazuar.com' || hostname.endsWith('.lazuar.com')) {
      return true;
    }

    return false;
  } catch (e) {
    return false;
  }
}
