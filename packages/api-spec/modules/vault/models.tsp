namespace LazuarApi.Vault;

model CreateVaultAssetRequestDto {
  product_ids: string[]; // GUID array
  name: string;
  cloudflare_r2_url: string;
}

model VaultAssetDto {
  id: string; // GUID
  product_ids: string[]; // GUID array
  name: string;
  cloudflare_r2_url: string;
}

model PortalVaultAssetDto {
  product_ids: string[]; // GUID array
  name: string;
  cloudflare_r2_url: string;
}

model GetPresignedUrlRequestDto {
  file_name: string;
  content_type: string;
}

model GetPresignedUrlResponseDto {
  upload_url: string;
  final_url: string;
}
