namespace LazuarApi.Community;

model CreateCommunitySpaceRequestDto {
  product_ids: string[]; // GUID array
  name: string;
  telegram_link?: string;
  zoom_link?: string;
}

model PortalCommunitySpaceDto {
  product_ids: string[]; // GUID array
  name: string;
  telegram_link?: string;
  zoom_link?: string;
}
