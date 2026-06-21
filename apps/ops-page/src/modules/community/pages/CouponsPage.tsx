import PageLayout from "../../core/components/PageLayout";

export default function CouponsPage() {
  return (
    <PageLayout 
      title="Promotions" 
      description="Create and track discount codes."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Promotions" }]}
    >
      <div className="p-12 text-center border border-dashed border-[#e5e5e5] text-[#71717a] text-sm bg-white">
        Coupons Table (Under Construction)
      </div>
    </PageLayout>
  );
}
