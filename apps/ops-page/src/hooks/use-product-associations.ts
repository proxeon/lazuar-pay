import { useQuery } from "@tanstack/react-query";
import { client } from "../lib/api-client";

export interface ProductAssociation {
  type: "space" | "asset";
  id: string;
  name: string;
}

export function useProductAssociations(activeWorkspaceId: string | null) {
  const { data: spaces } = useQuery({
    queryKey: ["community-spaces-list"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/community/spaces");
      return data || [];
    },
    enabled: !!activeWorkspaceId
  });

  const { data: assets } = useQuery({
    queryKey: ["vault-assets"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/vault/assets");
      return data || [];
    },
    enabled: !!activeWorkspaceId
  });

  const getAssociations = (productId: string): ProductAssociation[] => {
    const associations: ProductAssociation[] = [];

    if (spaces) {
      spaces.forEach((space) => {
        if (space.product_ids?.includes(productId)) {
          associations.push({
            type: "space",
            id: space.id,
            name: space.name
          });
        }
      });
    }

    if (assets) {
      assets.forEach((asset) => {
        if (asset.product_ids?.includes(productId)) {
          associations.push({
            type: "asset",
            id: asset.id,
            name: asset.name
          });
        }
      });
    }

    return associations;
  };

  const isAssociated = (productId: string): boolean => {
    return getAssociations(productId).length > 0;
  };

  return { getAssociations, isAssociated, isLoading: !spaces || !assets };
}
