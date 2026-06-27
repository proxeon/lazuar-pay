namespace LazuarApi.Vault;

model CreateVaultAssetRequestDto {
  name: string;
  slug: string;
  price: float64;
  cloudflare_r2_url: string;
}

model VaultAssetDto {
  id: string; // GUID
  product_id: string; // GUID
  name: string;
  cloudflare_r2_url: string;
}
