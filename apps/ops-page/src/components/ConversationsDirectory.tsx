import { useState, useEffect } from "react";
import { Link, useOutletContext } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { MessageSquare, ArrowRight, MoreVertical } from "lucide-react";
import { toast } from "sonner";
import { client } from "../lib/api-client";

export default function ConversationsDirectory() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const queryClient = useQueryClient();
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);

  useEffect(() => {
    const closeMenu = () => setOpenMenuId(null);
    document.addEventListener("click", closeMenu);
    return () => document.removeEventListener("click", closeMenu);
  }, []);

  const { data: conversationData } = useQuery({
    queryKey: ["conversations", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/ops/chat/conversations", { params: { query: { limit: 20, offset: 0 } } });
      if (error) throw new Error(error.detail);
      return data.data;
    },
    enabled: !!activeWorkspaceId
  });

  const handleRenameConversation = async (id: string, currentTitle: string) => {
    const newTitle = window.prompt("Enter new title:", currentTitle);
    if (!newTitle || newTitle.trim() === "" || newTitle === currentTitle) return;
    
    try {
      const { error } = await client.PUT("/ops/chat/conversations/{id}/title", {
        params: { path: { id } },
        body: { title: newTitle.trim() }
      });
      if (error) throw new Error(error.detail);
      
      toast.success("Conversation renamed");
      queryClient.invalidateQueries({ queryKey: ["conversations", activeWorkspaceId] });
    } catch (err: any) {
      toast.error("Failed to rename conversation", { description: err.message });
    }
  };

  const handleDeleteConversation = async (id: string) => {
    if (!window.confirm("Are you sure you want to delete this conversation?")) return;
    
    try {
      const { error } = await client.DELETE("/ops/chat/conversations/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
      
      toast.success("Conversation deleted");
      queryClient.invalidateQueries({ queryKey: ["conversations", activeWorkspaceId] });
    } catch (err: any) {
      toast.error("Failed to delete conversation", { description: err.message });
    }
  };

  return (
    <div className="flex-1 flex flex-col h-full overflow-y-auto bg-[#fafafa] p-6 md:p-12">
      <div className="max-w-4xl mx-auto w-full">
        <div className="mb-8">
          <h1 className="text-xl font-bold text-[#09090b]">Active Operational Threads</h1>
          <p className="text-xs text-[#71717a] mt-1">Review historical troubleshooting sessions</p>
        </div>

        {!conversationData || conversationData.length === 0 ? (
          <div className="border border-dashed border-[#e5e5e5] p-12 text-center bg-white">
            <p className="text-sm text-[#71717a]">No active operations threads found.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {conversationData.map((conv) => (
              <Link 
                to={`/chat/${conv.id}`}
                key={conv.id} 
                className="bg-white border border-[#e5e5e5] p-5 hover:bg-[#fafafa] transition-all cursor-pointer flex flex-col justify-between h-32 relative group block"
              >
                <div className="flex items-start justify-between min-w-0">
                  <div className="flex items-start gap-3 min-w-0">
                    <div className="h-8 w-8 shrink-0 bg-[#09090b] text-white flex items-center justify-center">
                      <MessageSquare size={14} />
                    </div>
                    <div className="min-w-0">
                      <h3 className="text-[14px] font-bold text-[#09090b] truncate pr-8">{conv.title}</h3>
                      <p className="text-[11px] text-[#71717a] mt-1">
                        {new Date(conv.updated_at).toLocaleString()}
                      </p>
                    </div>
                  </div>
                  <div className="relative shrink-0 ml-2" onClick={(e) => { e.preventDefault(); e.stopPropagation(); }}>
                    <button 
                      onClick={() => setOpenMenuId(openMenuId === conv.id ? null : conv.id)}
                      className="p-1 text-[#a1a1aa] hover:text-[#09090b] transition-colors rounded-sm focus:outline-none"
                    >
                      <MoreVertical size={16} />
                    </button>
                    {openMenuId === conv.id && (
                      <div className="absolute right-0 top-full mt-1 w-32 bg-white border border-[#e5e5e5] shadow-lg rounded-sm py-1 z-50">
                        <button 
                          onClick={() => { setOpenMenuId(null); handleRenameConversation(conv.id, conv.title); }}
                          className="w-full text-left px-3 py-1.5 text-xs text-[#09090b] hover:bg-[#f4f4f5] transition-colors"
                        >
                          Rename
                        </button>
                        <button 
                          onClick={() => { setOpenMenuId(null); handleDeleteConversation(conv.id); }}
                          className="w-full text-left px-3 py-1.5 text-xs text-rose-600 hover:bg-rose-50 transition-colors"
                        >
                          Delete
                        </button>
                      </div>
                    )}
                  </div>
                </div>
                <div className="flex items-center justify-between mt-4 pt-3 border-t border-[#f4f4f5]">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#71717a]">ID: {conv.id.substring(0,8)}</span>
                  <span className="text-[11px] font-bold text-[#09090b] flex items-center gap-1">Open <ArrowRight size={12} /></span>
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
