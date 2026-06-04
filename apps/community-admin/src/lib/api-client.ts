import createClient, { type Middleware } from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";
export const TENANT_SLUG = import.meta.env.VITE_TENANT_SLUG || "lazuar-hq";

const authMiddleware: Middleware = {
  async onRequest({ request }) {
    const token = localStorage.getItem("community_admin_token");
    if (token) {
      request.headers.set("Authorization", `Bearer ${token}`);
    }
    return request;
  },
};

export const client = createClient<paths>({ baseUrl: API_URL });
client.use(authMiddleware);

// ==========================================
// Mapped Type Aliases for UI Components
// ==========================================
export type Plan = components["schemas"]["Models.Community.CommunityPlanDto"];
export type Subscriber = components["schemas"]["Models.Community.CommunitySubscriptionDto"];
export type MessageTemplate = components["schemas"]["Models.Messaging.MessageTemplateDto"];
export type ReminderSchedule = components["schemas"]["Models.Community.CommunityReminderScheduleDto"];
export type CommunityStatsResponse = components["schemas"]["Models.Community.CommunitySubscriberStatsDto"];
export type DeliveryHistoryItem = components["schemas"]["Models.Community.DeliveryHistoryItemDto"];
export type PaymentConfig = components["schemas"]["Models.Community.PaymentConfigDto"];
export type PaymentRecord = components["schemas"]["Models.Community.PaymentRecordDto"];
