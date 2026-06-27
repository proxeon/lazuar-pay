namespace LazuarApi.Community;

model CreateCommunitySpaceRequestDto {
  product_id: string; // GUID
  name: string;
  telegram_link?: string;
  zoom_link?: string;
}

model PortalCommunitySpaceDto {
  product_id: string; // GUID
  name: string;
  telegram_link?: string;
  zoom_link?: string;
}
