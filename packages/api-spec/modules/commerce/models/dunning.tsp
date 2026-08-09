namespace LazuarApi.Commerce;

model DunningStepDto {
  day_offset: int32;
  action_type: string;
  subject?: string;
  email_body?: string;
  whatsapp_body?: string;
}

model DunningCampaignDto {
  id: string; // GUID
  name: string;
  is_active: boolean;
  final_action: string;
  grace_period_days: int32;
  priority_order: int32;
  recovered_revenue: float64;
  saved_subscriptions: int32;
  churned_subscriptions: int32;
  target_product_ids: string[]; // GUID array
  target_payment_methods: string[];
  steps: DunningStepDto[];
  created_at: utcDateTime;
}

model CreateDunningCampaignRequestDto {
  name: string;
  final_action: string;
  grace_period_days: int32;
  priority_order?: int32 = 0;
  target_product_ids?: string[]; // GUID array
  target_payment_methods?: string[];
  steps: DunningStepDto[];
}

model UpdateDunningCampaignRequestDto {
  name: string;
  final_action: string;
  grace_period_days: int32;
  priority_order?: int32 = 0;
  target_product_ids?: string[]; // GUID array
  target_payment_methods?: string[];
  steps: DunningStepDto[];
  is_active?: boolean;
}

model PauseDunningRequestDto {
  pause_until: utcDateTime;
}
