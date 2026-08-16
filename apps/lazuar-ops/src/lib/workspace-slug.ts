/** Align with OrganizationSlugMustBeValidRule on the API. */
export const RESERVED_SLUGS = new Set([
  "api", "app", "admin", "dashboard", "portal", "system",
  "www", "support", "help", "mail", "blog", "docs",
  "stripe", "billplz", "lazuar", "one", "auth", "login",
]);

/** Normalize workspace name / slug edits to a valid tenant slug. */
export function slugify(input: string): string {
  return input
    .toLowerCase()
    .trim()
    .replace(/\s+/g, "-")
    .replace(/[^a-z0-9-]/g, "")
    .replace(/-+/g, "-")
    .replace(/^-+|-+$/g, "");
}

export function validateSlug(slug: string): string | null {
  if (!slug || slug.length < 3) {
    return "Workspace slug must be at least 3 characters (e.g. acme-corp).";
  }
  if (slug.length > 63) {
    return "Workspace slug must be at most 63 characters.";
  }
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug)) {
    return "Use only lowercase letters, numbers, and single hyphens (no leading/trailing hyphens).";
  }
  if (RESERVED_SLUGS.has(slug)) {
    return `"${slug}" is reserved. Choose another workspace slug.`;
  }
  return null;
}
