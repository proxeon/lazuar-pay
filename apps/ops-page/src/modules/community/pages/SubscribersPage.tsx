import PageLayout from "../../core/components/PageLayout";

export default function SubscribersPage() {
  return (
    <PageLayout 
      title="Subscribers" 
      description="Manage active members, billing cycles, and access."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Subscribers" }]}
    >
      <div className="p-12 text-center border border-dashed border-[#e5e5e5] text-[#71717a] text-sm bg-white">
        Subscribers Table (Under Construction)
      </div>
    </PageLayout>
  );
}
