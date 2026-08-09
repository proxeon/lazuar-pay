import { UserPlus, Megaphone, GraduationCap, CreditCard, Building2, DollarSign, type LucideIcon } from "lucide-react";

export interface PromptItem {
  label: string;
  query: string;
}

export interface PromptCategory {
  id: string;
  icon: LucideIcon;
  title: string;
  description: string;
  prompts: PromptItem[];
}

export const PROMPT_LIBRARY: PromptCategory[] = [
  {
    id: "onboarding",
    icon: UserPlus,
    title: "Member Onboarding",
    description: "Enrollments, links, and access",
    prompts: [
      { label: "Manual Enrollment", query: "Manually enroll a new member who paid via cash or bank transfer." },
      { label: "Share Sign-up Link", query: "Get the public registration link for a specific class so I can share it on WhatsApp." },
      { label: "Send Checkout Link", query: "Send a payment checkout link to a prospective member's email." },
      { label: "Resend Welcome Kit", query: "Resend the onboarding email and portal links to a member who lost them." },
      { label: "Fix Login Issues", query: "Send a secure magic login link to a member who is locked out of the portal." },
    ]
  },
  {
    id: "communication",
    icon: Megaphone,
    title: "Communication",
    description: "Broadcasts, templates, and scheduling",
    prompts: [
      { label: "Mass Announcement", query: "Send a broadcast message to all active subscribers about an upcoming holiday schedule." },
      { label: "Targeted Broadcast", query: "Send a broadcast message only to members enrolled in a specific class." },
      { label: "Personalized Message", query: "Send a direct WhatsApp or Email message to a specific member." },
      { label: "Schedule Message", query: "Schedule a reminder to go out to a specific member next Friday." },
      { label: "Update Templates", query: "Update the wording on our automated payment reminder templates to sound friendlier." },
    ]
  },
  {
    id: "classes",
    icon: GraduationCap,
    title: "Classes & Plans",
    description: "Rosters, capacity, and pricing",
    prompts: [
      { label: "Check Roster", query: "Show me exactly who is currently enrolled in a specific class." },
      { label: "Check Capacity", query: "How many spots are left in a specific class?" },
      { label: "Create New Tier", query: "Create a new community plan or tier." },
      { label: "Update Details", query: "Update a specific class price and add a new Zoom link to the description." },
      { label: "Archive Class", query: "Archive an old class plan so no new people can sign up, but keep current members active." },
    ]
  },
  {
    id: "billing",
    icon: CreditCard,
    title: "Support & Billing",
    description: "Payments, refunds, and grace periods",
    prompts: [
      { label: "Lookup Member", query: "Find the subscription details and billing status for a specific member." },
      { label: "Log Offline Payment", query: "Record a manual bank transfer payment for a specific member." },
      { label: "Extend Grace Period", query: "Extend the grace period for a specific member by 7 days so their access isn't suspended." },
      { label: "Pause Reminders", query: "Pause automated payment reminders for a specific member until the end of the month." },
      { label: "Process Refund", query: "Issue a refund for a specific member's last payment." },
      { label: "Change Plan", query: "Upgrade a specific member to a different plan." },
      { label: "Cancel / Ban", query: "Cancel a specific member's subscription at the end of the month." },
      { label: "GDPR Deletion", query: "Permanently anonymize and delete all data for a member who requested account deletion." },
    ]
  },
  {
    id: "financials",
    icon: DollarSign,
    title: "Financial Health",
    description: "Revenue, fees, and tax liabilities",
    prompts: [
      { label: "Net Profit & Fees", query: "What is our exact Net Cash in Bank after deducting Stripe and Billplz gateway fees?" },
      { label: "Tax Liabilities", query: "How much SST/Tax liability do we currently owe the government from our recent sales?" },
      { label: "Gross vs Net Revenue", query: "Give me a breakdown of our Gross Revenue versus our actual Recognized Revenue." },
    ]
  },
  {
    id: "workspace",
    icon: Building2,
    title: "Workspace Admin",
    description: "Staff, modules, and health metrics",
    prompts: [
      { label: "Invite Staff", query: "Invite a new teacher or staff member to the workspace with Admin access." },
      { label: "Revoke Access", query: "Remove a staff member who is no longer working with us." },
      { label: "Toggle Modules", query: "Turn the COMMUNITY module on or off for our workspace." },
      { label: "Business Health", query: "Give me a summary of our community's MRR (Monthly Recurring Revenue) and total active subscribers." },
    ]
  }
];
