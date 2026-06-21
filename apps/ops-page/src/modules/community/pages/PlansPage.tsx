import PageLayout from "../../core/components/PageLayout";

export default function PlansPage() {
  return (
    <PageLayout 
      title="Plans & Products" 
      description="Manage your subscription tiers and pricing."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Plans" }]}
    >
      <div className="p-12 text-center border border-dashed border-[#e5e5e5] text-[#71717a] text-sm bg-white">
        Plans Gallery (Under Construction)
      </div>
    </PageLayout>
  );
}
