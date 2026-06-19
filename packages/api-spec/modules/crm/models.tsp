namespace LazuarApi.Crm;

model BillingAddressDto {
  line1: string;
  line2?: string;
  line3?: string;
  city: string;
  postal_code: string;
  state_code: string;
  country_code: string;
}

model ClientProfileDto {
  id: string; // GUID
  global_user_id?: string; // GUID
  full_name: string;
  email: string;
  phone: string;
  tin?: string;
  id_type?: string;
  id_value?: string;
  billing_address?: BillingAddressDto;
  consented_to_marketing: boolean;
}

model CreateClientProfileRequestDto {
  full_name: string;
  email: string;
  phone: string;
  tin?: string;
  id_type?: string;
  id_value?: string;
  billing_address?: BillingAddressDto;
  global_user_id?: string;
}

model UpdateClientProfileRequestDto {
  full_name?: string;
  email?: string;
  phone?: string;
  tin?: string;
  id_type?: string;
  id_value?: string;
  billing_address?: BillingAddressDto;
  consented_to_marketing?: boolean;
}
