import { useQuery } from "@tanstack/react-query";
import { Loader2, Users, ExternalLink, Video } from "lucide-react";
import { client } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";

export default function SpacesPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();

  // Note: Since we are in development and focusing on UI structure, we will use a dummy query here 
  // until the new /admin/community/spaces GET endpoint is fully built in the backend in future phases.
  const { data: spaces, isLoading } = useQuery({
    queryKey: ["community-spaces-list"],
    queryFn: async () => {
      return [];
    }
  });

  return (
    <PageLayout 
      title="Spaces & Access" 
      description="Manage the private Telegram and Zoom links associated with your community products."
      breadcrumbs={[{ label: "Community" }, { label: "Spaces" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="w-full overflow-x-auto min-h-[320px]">
          <table className="w-full text-left text-[13px] min-w-[750px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[35%]">Space Name</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[35%]">Access Links</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Linked Product ID</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr>
                  <td colSpan={3} className="py-12 text-center text-[#a1a1aa]">
                    <Loader2 className="animate-spin mx-auto" size={20} />
                  </td>
                </tr>
              ) : spaces?.length === 0 ? (
                <tr>
                  <td colSpan={3} className="py-12 text-center text-[13px] text-[#71717a] leading-relaxed">
                    No community spaces configured yet.<br /> 
                    Go to <strong>Commerce → Products</strong> and enable the Community toggle when building a product.
                  </td>
                </tr>
              ) : (
                spaces?.map((space: any) => (
                  <tr key={space.id} className="hover:bg-[#fafafa] transition-colors">
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-2 mb-1">
                        <Users size={14} className="text-[#a1a1aa]" />
                        <span className="font-bold text-[#09090b] text-[13px]">{space.name}</span>
                      </div>
                    </td>
                    <td className="px-5 py-4 space-y-1.5">
                      {space.telegram_link && (
                        <a href={space.telegram_link} target="_blank" rel="noopener noreferrer" className="flex items-center gap-1.5 text-[11px] font-mono text-blue-600 hover:underline">
                          <ExternalLink size={12} /> {space.telegram_link}
                        </a>
                      )}
                      {space.zoom_link && (
                        <a href={space.zoom_link} target="_blank" rel="noopener noreferrer" className="flex items-center gap-1.5 text-[11px] font-mono text-indigo-600 hover:underline">
                          <Video size={12} /> {space.zoom_link}
                        </a>
                      )}
                    </td>
                    <td className="px-5 py-4 font-mono text-[11px] text-[#71717a]">
                      {space.product_id}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </PageLayout>
  );
}
