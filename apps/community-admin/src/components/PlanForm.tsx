// apps/community-admin/src/components/PlanForm.tsx

import { useState, useEffect } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../lib/api";
import { ArrowLeft, Link2, Video, Plus, Trash2, Users, Clock } from "lucide-react";

interface FaqItem {
  id: string;
  question: string;
  answer: string;
}

export default function PlanForm() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEditing = !!id;

  const [isLoading, setIsLoading] = useState(isEditing);
  const [error, setError] = useState("");

  const [slug, setSlug] = useState("");
  const [name, setName] = useState("");
  const [audience, setAudience] = useState("");
  const [shortDescription, setShortDescription] = useState("");
  const [longDescription, setLongDescription] = useState("");
  const [price, setPrice] = useState(0);
  const [interval, setInterval] = useState("mo");
  const [featuresText, setFeaturesText] = useState("");
  const [methodology, setMethodology] = useState("");
  const [faq, setFaq] = useState<FaqItem[]>([]);
  const [isActive, setIsActive] = useState(true);
  const [displayOrder, setDisplayOrder] = useState(0);
  const [maxCapacity, setMaxCapacity] = useState<number | "">("");
  const [gracePeriodDays, setGracePeriodDays] = useState<number>(7);
  const [telegramLink, setTelegramLink] = useState("");
  const [meetingLink, setMeetingLink] = useState("");

  useEffect(() => {
    if (isEditing && id) {
      api.getPlanById(id)
        .then(plan => {
          setSlug(plan.slug);
          setName(plan.name);
          setAudience(plan.audience);
          setShortDescription(plan.short_description);
          setLongDescription(plan.long_description);
          setPrice(plan.price);
          setInterval(plan.interval);
          setFeaturesText((plan.features || []).join("\n"));
          setMethodology(plan.methodology || "");
          setFaq(plan.faq || []);
          setIsActive(plan.is_active);
          setDisplayOrder(plan.display_order);
          setMaxCapacity(plan.max_capacity ?? "");
          setGracePeriodDays(plan.grace_period_days ?? 7);
          setTelegramLink(plan.telegram_invite_link || "");
          setMeetingLink(plan.weekly_meeting_link || "");
          setIsLoading(false);
        })
        .catch(err => {
          setError(err.message);
          setIsLoading(false);
        });
    }
  }, [id, isEditing]);

  const createMutation = useMutation({
    mutationFn: api.createPlan,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      navigate("/plans");
    },
    onError: (err: any) => setError(err.message),
  });

  const updateMutation = useMutation({
    mutationFn: ({ planId, data }: { planId: string; data: any }) => api.updatePlan(planId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      navigate("/plans");
    },
    onError: (err: any) => setError(err.message),
  });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    // Generate a clean base slug. The backend handles incremental uniqueness (e.g. al-quran-2)
    const baseSlug = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)+/g, '');
    const finalSlug = isEditing ? slug : baseSlug;

    const payload = {
      slug: finalSlug,
      name,
      audience,
      short_description: shortDescription,
      long_description: longDescription,
      price,
      interval,
      features: featuresText.split("\n").map(s => s.trim()).filter(Boolean),
      methodology,
      faq,
      is_active: isActive,
      display_order: displayOrder,
      max_capacity: maxCapacity === "" || maxCapacity === 0 ? null : maxCapacity,
      grace_period_days: gracePeriodDays,
      telegram_invite_link: telegramLink || null,
      weekly_meeting_link: meetingLink || null,
    };

    if (isEditing && id) {
      updateMutation.mutate({ planId: id, data: payload });
    } else {
      createMutation.mutate(payload);
    }
  }

  function addFaqItem() {
    setFaq([...faq, { id: crypto.randomUUID(), question: "", answer: "" }]);
  }

  function updateFaqItem(index: number, field: "question" | "answer", value: string) {
    const updated = [...faq];
    updated[index] = { ...updated[index], [field]: value };
    setFaq(updated);
  }

  function removeFaqItem(index: number) {
    setFaq(faq.filter((_, i) => i !== index));
  }

  const isSaving = createMutation.isPending || updateMutation.isPending;

  if (isLoading) {
    return (
      <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px]">
        <p className="text-sm font-medium uppercase tracking-widest text-muted-foreground">Loading plan...</p>
      </div>
    );
  }

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px] flex flex-col gap-6">
      <header className="flex items-center gap-3">
        <Link to="/plans" className="inline-flex items-center gap-1.5 text-muted-foreground hover:text-foreground font-medium uppercase tracking-widest transition-colors text-[11px]">
          <ArrowLeft size={14} />
          Back
        </Link>
      </header>

      <div>
        <h1 className="text-[20px] font-semibold tracking-tight text-foreground">
          {isEditing ? "Edit Plan" : "Create New Plan"}
        </h1>
        <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mt-1">
          {isEditing ? "Update the plan details below." : "Fill in the details to create a new subscription plan."}
        </p>
      </div>

      {error && (
        <div className="p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-none">
          <p className="text-xs font-medium text-red-600 dark:text-red-400">{error}</p>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* ─── Basic Info ─────────────────────────────────────── */}
        <section className="bg-card border border-border/60 rounded-none shadow-sm p-6 space-y-5">
          <h2 className="text-xs font-bold uppercase tracking-widest text-foreground">Basic Information</h2>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Plan Name *</label>
              <input type="text" required value={name} onChange={e => setName(e.target.value)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="e.g. Pro Membership" />
            </div>
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Audience *</label>
              <input type="text" required value={audience} onChange={e => setAudience(e.target.value)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="e.g. Beginners" />
            </div>
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Billing Interval *</label>
              <select value={interval} onChange={e => setInterval(e.target.value)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
                <option value="mo">Monthly</option>
                <option value="yr">Yearly</option>
              </select>
            </div>
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Price (RM) *</label>
              <input type="number" required step="0.01" min="0" value={price}
                onChange={e => setPrice(parseFloat(e.target.value) || 0)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="99.00" />
            </div>
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Display Order</label>
              <input type="number" value={displayOrder}
                onChange={e => setDisplayOrder(parseInt(e.target.value) || 0)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="0" />
            </div>
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground flex items-center gap-1.5">
                <Clock size={12} /> Grace Period (days)
              </label>
              <input type="number" min="0" required value={gracePeriodDays}
                onChange={e => setGracePeriodDays(parseInt(e.target.value) || 0)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="7" />
              <p className="text-[10px] text-muted-foreground mt-1">Number of days after due date before access is suspended.</p>
            </div>
          </div>

          {/* Max Capacity */}
          <div className="space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground flex items-center gap-1.5">
              <Users size={12} /> Max Capacity
            </label>
            <input type="number" min="0" value={maxCapacity}
              onChange={e => setMaxCapacity(e.target.value === "" ? "" : parseInt(e.target.value) || 0)}
              className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring md:max-w-[200px]"
              placeholder="e.g. 50" />
            <p className="text-[11px] text-muted-foreground mt-1">
              Limits how many subscribers can be active at once. Leave blank or 0 for unlimited.
            </p>
          </div>

          <div className="space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Short Description *</label>
            <input type="text" required value={shortDescription} onChange={e => setShortDescription(e.target.value)}
              className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              placeholder="Brief summary of the plan..." />
          </div>

          <div className="space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Long Description</label>
            <textarea value={longDescription} onChange={e => setLongDescription(e.target.value)}
              rows={3} className="flex w-full rounded-none border border-border/60 bg-background px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-y"
              placeholder="Detailed description shown on the sales page..." />
          </div>

          <div className="flex items-center gap-2 pt-2">
            <input type="checkbox" id="is_active" checked={isActive}
              onChange={e => setIsActive(e.target.checked)} className="h-4 w-4 rounded-none border-border/60 text-foreground focus:ring-foreground accent-foreground" />
            <label htmlFor="is_active" className="text-sm font-medium text-foreground">Active (visible on public page)</label>
          </div>
        </section>

        {/* ─── Features & Content ─────────────────────────────── */}
        <section className="bg-card border border-border/60 rounded-none shadow-sm p-6 space-y-5">
          <h2 className="text-xs font-bold uppercase tracking-widest text-foreground">Features & Content</h2>

          <div className="space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Features (one per line)</label>
            <textarea value={featuresText} onChange={e => setFeaturesText(e.target.value)}
              rows={5} className="flex w-full rounded-none border border-border/60 bg-background px-3 py-2 text-sm font-mono shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-y"
              placeholder={"Live weekly class via Zoom\nPrivate Telegram group\nMonthly progress report"} />
          </div>

          <div className="space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Methodology / Standard</label>
            <textarea value={methodology} onChange={e => setMethodology(e.target.value)}
              rows={2} className="flex w-full rounded-none border border-border/60 bg-background px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-y"
              placeholder="Your methodology or curriculum standards..." />
          </div>
        </section>

        {/* ─── FAQ ────────────────────────────────────────────── */}
        <section className="bg-card border border-border/60 rounded-none shadow-sm p-6 space-y-5">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-bold uppercase tracking-widest text-foreground">FAQ</h2>
            <button type="button" onClick={addFaqItem}
              className="inline-flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-foreground hover:bg-secondary px-3 py-1.5 border border-border/60 rounded-none transition-colors">
              <Plus size={12} /> Add Question
            </button>
          </div>

          {faq.length === 0 && (
            <p className="text-xs text-muted-foreground py-2">No FAQ items yet. Click "Add Question" to create one.</p>
          )}

          <div className="space-y-4">
            {faq.map((item, index) => (
              <div key={item.id} className="border border-border/60 bg-secondary/20 rounded-none p-4 space-y-3">
                <div className="flex items-start justify-between gap-3">
                  <input type="text" value={item.question}
                    onChange={e => updateFaqItem(index, "question", e.target.value)}
                    className="flex-1 h-10 px-3 border border-border/60 bg-background text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded-none"
                    placeholder="Question..." />
                  <button type="button" onClick={() => removeFaqItem(index)}
                    className="shrink-0 h-10 w-10 flex items-center justify-center border border-border/60 bg-card hover:bg-red-50 hover:text-red-500 hover:border-red-200 transition-colors text-muted-foreground rounded-none">
                    <Trash2 size={14} />
                  </button>
                </div>
                <textarea value={item.answer}
                  onChange={e => updateFaqItem(index, "answer", e.target.value)}
                  rows={2} className="w-full px-3 py-2 border border-border/60 bg-background text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded-none resize-y"
                  placeholder="Answer..." />
              </div>
            ))}
          </div>
        </section>

        {/* ─── Fulfillment ────────────────────────────────────── */}
        <section className="bg-card border border-border/60 rounded-none shadow-sm p-6 space-y-5">
          <div>
            <h2 className="text-xs font-bold uppercase tracking-widest text-foreground flex items-center gap-2">
              <Link2 size={14} className="text-muted-foreground" /> Fulfillment & Access Links
            </h2>
            <p className="text-[11px] text-muted-foreground mt-1">
              These links are sent to subscribers automatically upon payment.
            </p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground flex items-center gap-1.5">
                <Link2 size={12} /> Telegram Group Invite
              </label>
              <input type="url" value={telegramLink} onChange={e => setTelegramLink(e.target.value)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="https://t.me/+abc123..." />
            </div>
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground flex items-center gap-1.5">
                <Video size={12} /> Weekly Meeting Link
              </label>
              <input type="url" value={meetingLink} onChange={e => setMeetingLink(e.target.value)}
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="https://zoom.us/j/123456789" />
            </div>
          </div>
        </section>

        {/* ─── Actions ────────────────────────────────────────── */}
        <div className="flex items-center justify-between pt-4 pb-8">
          <Link to="/plans" className="text-xs font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">
            Cancel
          </Link>
          <button type="submit" disabled={isSaving}
            className="h-12 px-8 bg-foreground text-background text-sm font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95">
            {isSaving ? "Saving..." : (isEditing ? "Update Plan" : "Create Plan")}
          </button>
        </div>
      </form>
    </div>
  );
}
