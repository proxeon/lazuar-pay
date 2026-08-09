/**
 * Product → Community space / Vault asset associations.
 *
 * ADR 022 removed Community & Vault modules. There are no
 * `/admin/community/spaces` or `/admin/vault/assets` endpoints.
 * This hook remains as a no-op so product UI can call it without
 * phantom OpenAPI paths; restore when fulfillment modules return.
 */
export interface ProductAssociation {
  type: "space" | "asset";
  id: string;
  name: string;
}

export function useProductAssociations(_activeWorkspaceId: string | null) {
  const getAssociations = (_productId: string): ProductAssociation[] => [];
  const isAssociated = (_productId: string): boolean => false;
  return { getAssociations, isAssociated, isLoading: false };
}
