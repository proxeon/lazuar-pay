// apps/community-admin/src/lib/api.ts

const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";
const TENANT_SLUG = import.meta.env.VITE_TENANT_SLUG || "lazuar-hq";

// ==========================================
// 1. Core State Machine Interfaces & Schemas
// ==========================================

export interface Subscriber {
  id: string;
  customer_name: string;
  customer_email: string;
  customer_phone?: string;
  plan_id?: string;
  plan_name: string;
  plan_price: number;
  status: 'PENDING' | 'ACTIVE' | 'GRACE_PERIOD' | 'SUSPENDED' | 'PAST_DUE' | 'CANCELED' | 'CANCELLED';
  source?: string;
  is_reminder_only?: boolean;
  preferred_channel?: string;
  admin_notes?: string;
  created_at: string;
  next_billing_date: string;
  days_overdue?: number;
  last_payment_date?: string | null;
  total_payments?: number;
  reminders_paused_until?: string | null;
}

export interface Plan {
  id: string;
  slug: string;
  name: string;
  audience: string;
  short_description: string;
  long_description?: string;
  price: number;
  interval: string;
  features?: string[];
  methodology?: string;
  faq?: Array<{ id: string; question: string; answer: string }>;
  is_active: boolean;
  display_order: number;
  max_capacity?: number | null;
  grace_period_days?: number;
  enrolled_count?: number;
  telegram_invite_link?: string | null;
  weekly_meeting_link?: string | null;
}

export interface MessageTemplate {
  id: string;
  name: string;
  subject: string;
  body: string;
  is_default: boolean;
  updated_at: string;
}

export interface ReminderSchedule {
  id: string;
  plan_id: string | null;
  plan_name?: string;
  template_id: string;
  template_name?: string;
  channel: string;
  days_relative_to_due: number;
  time_of_day: string;
  is_enabled: boolean;
  created_at: string;
}

export interface CreateSubscriberRequest {
  name: string;
  email: string;
  phone: string;
  plan_id: string;
  source?: string;
  payment_method?: string;
  reference_number?: string;
  amount_paid?: number;
  notes?: string;
  is_reminder_only?: boolean;
  preferred_channel?: string;
}

export interface UpdateSubscriberRequest {
  name?: string;
  email?: string;
  phone?: string;
  is_reminder_only?: boolean;
  preferred_channel?: string;
  notes?: string;
  next_renewal_date?: string; 
}

export interface PaymentRecord {
  id: string;
  amount: number;
  currency: string;
  payment_method: string;
  reference_number?: string;
  receipt_url?: string;
  recorded_by?: string;
  period_start: string;
  period_end: string;
  status: string;
  notes?: string;
  created_at: string;
}

export interface DeliveryHistoryItem {
  id: string;
  channel: string;
  recipient: string;
  template_name?: string;
  subject?: string;
  status: string;
  error_message?: string;
  created_at: string;
}

export interface SendReminderRequest {
  template_name?: string;
  custom_message?: string;
  channel?: string;
}

export interface ScheduleOneOffRequest {
  subscriber_id: string;
  template_name?: string;
  custom_message?: string;
  channel?: string;
  scheduled_at: string;
}

export interface TestReminderRequest {
  template_name: string;
  channel?: string;
}

// Phase 2.1: Robust Analytics Interface
export interface CommunityStatsResponse {
  mrr: number;
  active_subscribers: number;
  past_due_subscribers: number;
  cancelled_subscribers: number;
  net_new_last_30_days: number;
  churn_rate_percentage: number;
  average_revenue_per_user: number;
  reminder_effectiveness_percentage: number;
  total_revenue_collected: number;
  cash_flow_trend: Array<{ month: string; amount: number }>;
  payment_methods: Array<{ method: string; count: number; total_amount: number }>;
}

function getHeaders() {
  const token = localStorage.getItem("community_admin_token");
  return {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

// ==========================================
// 2. System API Wrapper
// ==========================================

export const api = {
  login: async (email: string, password: string) => {
    try {
      const res = await fetch(`${API_URL}/platform/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      if (!res.ok) {
        if (res.status === 401) throw new Error("Invalid email or password.");
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Login failed");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getMe: async () => {
    try {
      const res = await fetch(`${API_URL}/platform/auth/me`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Not authenticated");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getStats: async (): Promise<CommunityStatsResponse> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/stats?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch stats");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getPlans: async (): Promise<Plan[]> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/plans?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch plans");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getPlanById: async (id: string): Promise<Plan> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/plans/${id}?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Plan not found");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  createPlan: async (data: any) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/plans?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to create plan");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  updatePlan: async (id: string, data: any) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/plans/${id}?tenant=${TENANT_SLUG}`, {
        method: "PUT",
        headers: getHeaders(),
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to update plan");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getSubscribers: async (): Promise<Subscriber[]> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch subscribers");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  exportSubscribersCsv: async () => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/export?tenant=${TENANT_SLUG}`, {
        method: "GET",
        headers: getHeaders(),
      });
      if (!res.ok) throw new Error("Failed to export CSV");
      
      const blob = await res.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      
      const cd = res.headers.get("Content-Disposition");
      let filename = `Subscribers_Export.csv`;
      if (cd && cd.includes("filename=")) {
        filename = cd.split("filename=")[1].replace(/"/g, "");
      }
      
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    } catch (err: any) {
      throw new Error(err.message || "Failed to export CSV");
    }
  },

  createSubscriber: async (data: CreateSubscriberRequest) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to create subscriber");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  updateSubscriber: async (id: string, data: UpdateSubscriberRequest) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}?tenant=${TENANT_SLUG}`, {
        method: "PUT",
        headers: getHeaders(),
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to update subscriber");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getPaymentConfig: async () => {
    try {
      const res = await fetch(`${API_URL}/admin/community/payment-config?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch payment config");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  updatePaymentConfig: async (data: any) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/payment-config?tenant=${TENANT_SLUG}`, {
        method: "PUT",
        headers: getHeaders(),
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to save payment config");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  // ==========================================
  // 3. Communications & Automations (Templates)
  // ==========================================

  getTemplates: async (): Promise<MessageTemplate[]> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/templates?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch templates");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  updateTemplate: async (id: string, data: { subject: string; body: string }) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/templates/${id}?tenant=${TENANT_SLUG}`, {
        method: "PUT",
        headers: getHeaders(),
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to update template");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  deleteTemplate: async (id: string) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/templates/${id}?tenant=${TENANT_SLUG}`, {
        method: "DELETE",
        headers: getHeaders(),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to reset template");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  testReminder: async (data: TestReminderRequest) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/reminders/test?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to send test reminder");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  // ==========================================
  // Reminder Schedules
  // ==========================================

  getReminderSchedule: async (): Promise<ReminderSchedule[]> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/reminder-schedule?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch reminder schedule");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  createReminderSchedule: async (data: any) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/reminder-schedule?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to create reminder schedule");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  updateReminderSchedule: async (id: string, data: any) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/reminder-schedule/${id}?tenant=${TENANT_SLUG}`, {
        method: "PUT",
        headers: getHeaders(),
        body: JSON.stringify(data)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to update reminder schedule");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  deleteReminderSchedule: async (id: string) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/reminder-schedule/${id}?tenant=${TENANT_SLUG}`, {
        method: "DELETE",
        headers: getHeaders(),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to delete reminder schedule");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  scheduleOneOff: async (data: ScheduleOneOffRequest) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/reminders/schedule-one-off?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to schedule reminder");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  pauseReminders: async (id: string, data: { pause_until: string | null }) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}/pause-reminders?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to pause reminders");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  // ==========================================
  // 4. Interactive State Machine Action Hooks
  // ==========================================

  recordPayment: async (id: string, data: { amount: number; payment_method: string; reference_number?: string; notes?: string; receipt_file?: string }) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}/record-payment?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to record payment");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getPaymentHistory: async (id: string): Promise<PaymentRecord[]> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}/payments?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch payment history");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  getReminderHistory: async (id: string): Promise<DeliveryHistoryItem[]> => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}/reminders?tenant=${TENANT_SLUG}`, { headers: getHeaders() });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to fetch reminder history");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  sendReminder: async (id: string, data: SendReminderRequest) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}/send-reminder?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify(data)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to send reminder");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  extendGracePeriod: async (id: string, days: number = 5) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}/extend-grace?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders(),
        body: JSON.stringify({ days })
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to extend grace period limit");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  },

  cancelSubscription: async (id: string) => {
    try {
      const res = await fetch(`${API_URL}/admin/community/subscribers/${id}/cancel?tenant=${TENANT_SLUG}`, {
        method: "POST",
        headers: getHeaders()
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || "Failed to flag cancellation on remote endpoint");
      }
      return await res.json();
    } catch (err: any) {
      throw new Error(err.message || "Failed to process request");
    }
  }
};
