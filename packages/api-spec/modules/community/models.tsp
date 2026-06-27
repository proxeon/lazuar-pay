namespace LazuarApi.Community;

model CreateCommunitySpaceRequestDto {
  product_ids: string[]; // GUID array
  name: string;
  telegram_link?: string;
  zoom_link?: string;
}

model AdminCommunitySpaceDto {
  id: string; // GUID
  name: string;
  telegram_link?: string;
  zoom_link?: string;
  product_ids: string[]; // GUID array
}

model PortalCommunitySpaceDto {
  product_ids: string[]; // GUID array
  name: string;
  telegram_link?: string;
  zoom_link?: string;
}
