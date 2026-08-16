namespace LazuarApi.Crm;

// Models-only surface (no TypeSpec routes).
// CRM has no public Minimal API; these DTOs are shared inter-module contracts via
// generated Lazuar.ApiTypes (e.g. ICrmQueryService, CreateClientProfileCommand).
// Imported by main.tsp so NSwag emits them for backend modules — not for product docs.
// Do not add routes here unless CRM gains a real HTTP edge.

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
  company_name?: string;
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
