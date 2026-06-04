import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../lib/api";
import type { Plan } from "../lib/api";
import { Plus, Menu, Link2, Video, Users, Share2, Copy, Check, X, Download, RefreshCw } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { toast } from "sonner";

export default function Plans({ isMobile, toggleSidebar }: any) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: plans, isLoading, isFetching } = useQuery({ queryKey: ["community-plans"], queryFn: api.getPlans });

  // Share Modal State
  const [sharePlan, setSharePlan] = useState<Plan | null>(null);
  const [copied, setCopied] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);

  const getShareUrl = (slug: string) => {
    // Generate URL targeting the community enrollment frontend
    const isLocal = typeof window !== 'undefined' && window.location.hostname === 'localhost';
    const baseUrl = isLocal ? 'http://localhost:3020' : 'https://community.lazuar.com';
    return `${baseUrl}/${slug}`;
  };

  const handleCopy = async (url: string) => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      toast.success("Link copied to clipboard!");
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      toast.error("Failed to copy link");
    }
  };

  const handleDownloadQR = async () => {
    if (!sharePlan) return;
    setIsDownloading(true);
    
    // Request a larger size (500x500) for a higher quality download
    const qrUrl = `https://api.qrserver.com/v1/create-qr-code/?size=500x500&data=${encodeURIComponent(getShareUrl(sharePlan.slug))}`;
    
    try {
      const response = await fetch(qrUrl);
      const blob = await response.blob();
      const blobUrl = window.URL.createObjectURL(blob);
      
      const a = document.createElement("a");
      a.style.display = "none";
      a.href = blobUrl;
      a.download = `${sharePlan.slug}-qr.png`; // Set a clean filename
      document.body.appendChild(a);
      
      a.click(); // Trigger the download
      
      // Cleanup
      window.URL.revokeObjectURL(blobUrl);
      document.body.removeChild(a);
      toast.success("QR Code downloaded successfully!");
    } catch (error) {
      toast.error("Failed to download QR code. Please try again.");
    } finally {
      setIsDownloading(false);
    }
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 relative">
      <header className="flex items-center justify-between pb-2">
        <div className="flex items-center gap-3">
          {isMobile && <button onClick={toggleSidebar} className="p-1.5 hover:bg-secondary rounded-none transition-colors"><Menu size={20} /></button>}
          <div>
            <h1 className="text-[20px] font-semibold tracking-tight text-foreground">Plans & Pricing</h1>
            <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mt-1">Manage your community subscription tiers.</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button 
            onClick={() => queryClient.invalidateQueries({ queryKey: ["community-plans"] })}
            disabled={isFetching}
            className="h-10 w-10 border border-border/60 bg-card hover:bg-secondary rounded-none transition-colors text-foreground flex items-center justify-center disabled:opacity-50"
            title="Refresh Plans"
          >
            <RefreshCw size={16} className={isFetching ? "animate-spin text-muted-foreground" : ""} />
          </button>
          <Link to="/plans/new"
            className="inline-flex items-center h-10 px-4 bg-foreground text-background text-sm font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 transition-colors">
            <Plus className="w-4 h-4 mr-2" /> Create Plan
          </Link>
        </div>
      </header>

      {isLoading ? (
        <p className="text-sm font-medium uppercase tracking-widest text-muted-foreground">Loading...</p>
      ) : plans?.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <p className="text-sm font-medium text-muted-foreground mb-4">No plans created yet.</p>
          <Link to="/plans/new"
            className="inline-flex items-center h-10 px-4 bg-foreground text-background text-sm font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 transition-colors">
            <Plus className="w-4 h-4 mr-2" /> Create Your First Plan
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {plans?.map((plan: Plan) => {
            const isFull = plan.max_capacity && (plan.enrolled_count || 0) >= plan.max_capacity;
            const hasCapacity = plan.max_capacity != null;

            return (
              <div key={plan.id} className="bg-card border border-border/60 rounded-none shadow-sm hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] hover:border-foreground/40 overflow-hidden flex flex-col transition-all duration-200">
                <div className="p-5 pb-3">
                  <div className="flex justify-between items-start">
                    <span className="inline-flex items-center px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest bg-secondary/30 text-muted-foreground border border-border/60 rounded-none">
                      {plan.audience}
                    </span>
                    <div className="flex gap-1">
                      {!plan.is_active && (
                        <span className="inline-flex items-center px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest bg-red-50 text-red-600 border border-red-200/60 dark:bg-red-950/30 dark:border-red-900 rounded-none">
                          Archived
                        </span>
                      )}
                      {plan.telegram_invite_link && (
                        <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-widest border border-border/60 rounded-none bg-secondary/10" title="Telegram Group Attached">
                          <Link2 size={10} /> TG
                        </span>
                      )}
                      {plan.weekly_meeting_link && (
                        <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-widest border border-border/60 rounded-none bg-secondary/10" title="Zoom Link Attached">
                          <Video size={10} /> Zoom
                        </span>
                      )}
                    </div>
                  </div>
                  <h3 className="text-lg font-semibold mt-3 text-foreground leading-tight">{plan.name}</h3>
                </div>
                <div className="px-5 pb-5 flex flex-col flex-1">
                  <div className="text-3xl font-bold mb-2 text-foreground tracking-tighter">
                    RM {plan.price.toFixed(2)}
                    <span className="text-sm font-normal text-muted-foreground tracking-normal ml-1">/{plan.interval}</span>
                  </div>

                  {/* Capacity Badge */}
                  {hasCapacity && (
                    <div className="mb-4">
                      {isFull ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest bg-red-50 text-red-600 border border-red-200/60 dark:bg-red-950/30 dark:border-red-900 rounded-none">
                          <Users size={10} /> FULL ({plan.enrolled_count}/{plan.max_capacity})
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest bg-amber-50 text-amber-700 border border-amber-200/60 dark:bg-amber-950/30 dark:border-amber-900 rounded-none">
                          <Users size={10} /> {plan.enrolled_count}/{plan.max_capacity} enrolled
                        </span>
                      )}
                    </div>
                  )}

                  <p className="text-sm text-muted-foreground mb-6 line-clamp-2 leading-relaxed">{plan.short_description}</p>
                  
                  {/* Card Actions */}
                  <div className="mt-auto flex flex-col gap-2">
                    <button onClick={() => navigate(`/plans/${plan.id}/edit`)}
                      className="w-full h-10 border border-border/60 rounded-none text-xs font-bold uppercase tracking-wide hover:bg-secondary transition-colors text-foreground">
                      Edit Plan
                    </button>
                    <button onClick={() => setSharePlan(plan)}
                      className="w-full h-10 border border-border/60 rounded-none text-xs font-bold uppercase tracking-wide hover:bg-secondary transition-colors flex items-center justify-center gap-1.5 text-foreground">
                      <Share2 size={13} className="text-muted-foreground" /> Share & QR
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Share & QR Code Modal Overlay */}
      {sharePlan && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm transition-opacity" onClick={() => setSharePlan(null)} />
          <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-sm overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-4 border-b border-border/60">
              <h3 className="text-sm font-bold uppercase tracking-widest text-foreground">Share Link</h3>
              <button onClick={() => setSharePlan(null)} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1">
                <X size={16} />
              </button>
            </div>
            
            <div className="p-6 flex flex-col items-center gap-6">
              
              {/* QR Code Section */}
              <div className="flex flex-col items-center gap-3">
                <div className="bg-white p-3 border border-border/60 rounded-none shadow-sm">
                  <img 
                    src={`https://api.qrserver.com/v1/create-qr-code/?size=250x250&data=${encodeURIComponent(getShareUrl(sharePlan.slug))}`} 
                    alt="QR Code" 
                    className="w-[200px] h-[200px]"
                  />
                </div>
                <button 
                  onClick={handleDownloadQR}
                  disabled={isDownloading}
                  className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground flex items-center gap-1.5 transition-colors disabled:opacity-50 mt-2"
                >
                  <Download size={13} /> 
                  {isDownloading ? "Downloading..." : "Download QR Code"}
                </button>
              </div>
              
              {/* Link Copy Section */}
              <div className="w-full space-y-2">
                <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Public Link</label>
                <div className="flex items-center gap-2">
                  <input 
                    readOnly 
                    value={getShareUrl(sharePlan.slug)} 
                    className="flex-1 h-10 px-3 border border-border/60 rounded-none text-xs bg-secondary/50 text-foreground focus:outline-none font-mono"
                  />
                  <button 
                    onClick={() => handleCopy(getShareUrl(sharePlan.slug))}
                    className="shrink-0 h-10 px-4 border border-border/60 rounded-none text-xs font-bold uppercase tracking-widest hover:bg-secondary transition-colors flex items-center gap-1.5 bg-card text-foreground"
                  >
                    {copied ? <Check size={14} className="text-emerald-600" /> : <Copy size={14} />}
                    {copied ? "Copied" : "Copy"}
                  </button>
                </div>
              </div>

            </div>
          </div>
        </div>
      )}
    </div>
  );
}
