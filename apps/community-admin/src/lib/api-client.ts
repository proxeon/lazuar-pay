import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";
export const TENANT_SLUG = import.meta.env.VITE_TENANT_SLUG || "lazuar-hq";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  // This ensures HttpOnly cookies are attached to all cross-origin requests automatically
  fetch: (url, init) => fetch(url, { ...init, credentials: "include" })
});

// ==========================================
// Mapped Type Aliases for UI Components
// ==========================================
export type Plan = components["schemas"]["LazuarApi.Community.CommunityPlanDto"];
export type Subscriber = components["schemas"]["LazuarApi.Community.CommunitySubscriptionDto"];
export type MessageTemplate = components["schemas"]["LazuarApi.Messaging.MessageTemplateDto"];
export type ReminderSchedule = components["schemas"]["LazuarApi.Community.CommunityReminderScheduleDto"];
export type CommunityStatsResponse = components["schemas"]["LazuarApi.Community.CommunitySubscriberStatsDto"];
export type DeliveryHistoryItem = components["schemas"]["LazuarApi.Community.DeliveryHistoryItemDto"];
export type PaymentConfig = components["schemas"]["LazuarApi.Community.PaymentConfigDto"];
export type PaymentRecord = components["schemas"]["LazuarApi.Community.PaymentRecordDto"];
