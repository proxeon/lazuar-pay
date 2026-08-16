namespace LazuarApi.One;

model PublicCreditPackageDto {
  amount_myr: float64;
  credits: int32;
}

model PublicHubPlanDto {
  code: string;
  name: string;
  amount_myr: float64;
  interval: string;
  currency: string;
}

/** Honest Hub commercial card. `gmv_take_percent` is always 0. */
model PublicPricingDto {
  gmv_take_percent: float64;
  starter_credits: int32;
  packages: PublicCreditPackageDto[];
  sst_rate: float64;
  sst_note: string;
  checkout_is_free: boolean;
  lhdn_credits_live: boolean;
  whatsapp_credits_live: boolean;
  lhdn_submit_credits: int32;
  whatsapp_send_credits: int32;
  hub_plan: PublicHubPlanDto;
}
