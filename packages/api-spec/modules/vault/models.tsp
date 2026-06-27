namespace LazuarApi.Vault;

model CreateVaultAssetRequestDto {
  product_id: string; // GUID
  name: string;
  cloudflare_r2_url: string;
}

model VaultAssetDto {
  id: string; // GUID
  product_id: string; // GUID
  name: string;
  cloudflare_r2_url: string;
}

model PortalVaultAssetDto {
  product_id: string; // GUID
  name: string;
  cloudflare_r2_url: string;
}
