namespace LazuarApi.One;

model GetPresignedUrlRequestDto {
  file_name: string;
  content_type: string;
}

model GetPresignedUrlResponseDto {
  upload_url: string;
  final_url: string;
}
