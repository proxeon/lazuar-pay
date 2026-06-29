import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, Users, ExternalLink, Video } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import CreateSpaceModal from "../components/CreateSpaceModal";
import SpaceDetailPanel from "../components/SpaceDetailPanel";
import { cn } from "../../../lib/utils";

type AdminCommunitySpaceDto = components["schemas"]["Community.AdminCommunitySpaceDto"];

export default function SpacesPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedSpace, setSelectedSpace] = useState<AdminCommunitySpaceDto | null>(null);

  const { data: products } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    },
    enabled: !!activeWorkspaceId
  });

  const { data: spaces, isLoading } = useQuery({
    queryKey: ["community-spaces-list"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/spaces");
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  const renderStatusIndicator = (space: AdminCommunitySpaceDto) => {
    const hasTelegram = !!space.telegram_link?.trim();
    const hasZoom = !!space.zoom_link?.trim();

    if (hasTelegram && hasZoom) {
      return (
        <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block bg-emerald-50 text-emerald-700 border-emerald-200">
          Fully Configured
        </span>
      );
    }
    if (hasTelegram || hasZoom) {
      return (
        <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block bg-amber-50 text-amber-700 border-amber-200">
          Partial Config
        </span>
      );
    }
    return (
      <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block bg-rose-50 text-rose-700 border-rose-200">
        Missing Setup Links
      </span>
    );
  };

  return (
    <PageLayout 
      title="Community Spaces" 
      description="Manage the private Telegram and Zoom links associated with your community products."
      breadcrumbs={[{ label: "Community" }, { label: "Spaces" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Space
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="w-full overflow-x-auto min-h-[320px]">
          <table className="w-full text-left text-[13px] min-w-[750px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[30%]">Space Name</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[25%]">Access Links</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[30%]">Linked Product Tiers</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-[#a1a1aa]">
                    <Loader2 className="animate-spin mx-auto" size={20} />
                  </td>
                </tr>
              ) : spaces?.length === 0 ? (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-[13px] text-[#71717a] leading-relaxed">
                    No community spaces configured yet.<br /> 
                    Click "Create Space" to link Telegram and Zoom details to your Commerce products.
                  </td>
                </tr>
              ) : (
                spaces?.map((space) => (
                  <tr key={space.id} onClick={() => setSelectedSpace(space)} className="hover:bg-[#fafafa] transition-colors cursor-pointer group">
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-2 mb-1">
                        <Users size={14} className="text-indigo-600" />
                        <span className="font-bold text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{space.name}</span>
                      </div>
                    </td>
                    <td className="px-5 py-4 space-y-1.5">
                      {space.telegram_link && (
                        <a href={space.telegram_link} target="_blank" rel="noopener noreferrer" onClick={e => e.stopPropagation()} className="flex items-center gap-1.5 text-[11px] font-mono text-blue-600 hover:underline">
                          <ExternalLink size={12} className="shrink-0" /> {space.telegram_link}
                        </a>
                      )}
                      {space.zoom_link && (
                        <a href={space.zoom_link} target="_blank" rel="noopener noreferrer" onClick={e => e.stopPropagation()} className="flex items-center gap-1.5 text-[11px] font-mono text-indigo-600 hover:underline">
                          <Video size={12} className="shrink-0" /> {space.zoom_link}
                        </a>
                      )}
                    </td>
                    <td className="px-5 py-4">
                      <div className="flex flex-wrap gap-1.5">
                        {space.linked_checkouts && space.linked_checkouts.length > 0 ? (
                          space.linked_checkouts.map((checkout) => (
                            <span 
                              key={checkout.id} 
                              className="text-[10px] font-bold uppercase tracking-widest bg-zinc-100 text-[#09090b] border border-zinc-200 px-1.5 py-0.5 rounded-sm"
                            >
                              {checkout.name}
                            </span>
                          ))
                        ) : (
                          <span className="text-[11px] text-rose-600 font-semibold uppercase tracking-wider">Unlinked Space</span>
                        )}
                      </div>
                    </td>
                    <td className="px-5 py-4">
                      {renderStatusIndicator(space)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <CreateSpaceModal 
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
      />

      <SpaceDetailPanel
        space={selectedSpace}
        products={products}
        onClose={() => setSelectedSpace(null)}
        onUpdate={setSelectedSpace}
      />
    </PageLayout>
  );
}
