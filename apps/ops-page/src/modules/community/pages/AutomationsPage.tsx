import PageLayout from "../../core/components/PageLayout";

export default function AutomationsPage() {
  return (
    <PageLayout 
      title="Automations & Broadcasts" 
      description="Manage scheduled reminders and manual mass announcements."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Automations" }]}
    >
      <div className="p-12 text-center border border-dashed border-[#e5e5e5] text-[#71717a] text-sm bg-white">
        Automations Hub (Under Construction)
      </div>
    </PageLayout>
  );
}
