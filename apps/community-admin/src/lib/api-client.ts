import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";
export const TENANT_SLUG = import.meta.env.VITE_TENANT_SLUG || "lazuar-hq";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  fetch: (url, init) => fetch(url, { ...init, credentials: "include" })
});

// ==========================================
// Mapped Type Aliases for UI Components
// ==========================================
export type Plan = components["schemas"]["Community.CommunityPlanDto"];
export type Subscriber = components["schemas"]["Community.CommunitySubscriptionDto"];

export type MessageTemplate = components["schemas"]["Community.MessageTemplateDto"]; 

export type ReminderSchedule = components["schemas"]["Community.CommunityReminderScheduleDto"];
export type CommunityStatsResponse = components["schemas"]["Community.CommunitySubscriberStatsDto"];
export type DeliveryHistoryItem = components["schemas"]["Community.DeliveryHistoryItemDto"];
export type PaymentConfig = components["schemas"]["Community.PaymentConfigDto"];
export type PaymentRecord = components["schemas"]["Community.PaymentRecordDto"];
