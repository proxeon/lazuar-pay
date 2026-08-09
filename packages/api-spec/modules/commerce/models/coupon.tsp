namespace LazuarApi.Commerce;

model CouponDto {
  id: string; // GUID
  code: string;
  discount_type: string;
  amount: float64;
  max_uses: int32;
  used_count: int32;
  reserved_count: int32;
  minimum_original_price: float64;
  expires_at?: utcDateTime;
  applicable_product_ids?: string[]; // GUID array
  is_active: boolean;
}

model CreateCouponRequestDto {
  code: string;
  discount_type: string;
  amount: float64;
  max_uses: int32;
  minimum_original_price?: float64;
  expires_at?: utcDateTime;
  applicable_product_ids?: string[]; // GUID array
}

model UpdateCouponRequestDto {
  code?: string;
  discount_type?: string;
  amount?: float64;
  max_uses?: int32;
  minimum_original_price?: float64;
  expires_at?: utcDateTime;
  applicable_product_ids?: string[]; // GUID array
  is_active?: boolean;
}
