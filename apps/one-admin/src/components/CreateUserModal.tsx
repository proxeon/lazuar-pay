import { useState, useEffect } from "react";
import { X, Loader2, Key } from "lucide-react";
import { toast } from "sonner";
import type { UserRole } from "./Users";

interface CreateUserModalProps {
  onClose: () => void;
  onSuccess: (userData: { name: string; email: string; role: UserRole }) => void;
}

export default function CreateUserModal({ onClose, onSuccess }: CreateUserModalProps) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Helper to generate a random mock secure password
  const handleGeneratePassword = () => {
    const chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()";
    let generated = "";
    for (let i = 12; i > 0; i--) {
      generated += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    setPassword(generated);
    setConfirmPassword(generated);
    toast.success("Secure password generated and applied.");
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !email.trim() || !password.trim() || !confirmPassword.trim()) return;

    if (password !== confirmPassword) {
      toast.error("Passwords do not match.");
      return;
    }

    if (password.length < 6) {
      toast.error("Password must be at least 6 characters.");
      return;
    }

    setIsSubmitting(true);

    // Simulate 500ms network execution latency before triggers
    setTimeout(() => {
      setIsSubmitting(false);
      onSuccess({
        name: name.trim(),
        email: email.trim().toLowerCase(),
        role: "CLIENT" as const,
      });
    }, 500);
  };

  const passwordsMatch = password && password === confirmPassword;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" 
        onClick={onClose} 
      />
      
      {/* Modal Container */}
      <div className="relative bg-white border border-[#e5e5e5] rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-md overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
        
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] shrink-0">
          <div>
            <h3 className="text-[14px] font-semibold tracking-tight text-[#09090b]">Register Client</h3>
            <p className="text-[11px] text-[#71717a] mt-0.5">Manually configure access credentials for a customer.</p>
          </div>
          <button 
            onClick={onClose} 
            className="text-[#a1a1aa] hover:bg-[#f4f4f5] hover:text-[#09090b] rounded-none transition-colors p-1"
          >
            <X size={16} />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Client Full Name</label>
            <input 
              type="text" 
              required 
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Ahmad Firdaus"
              className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address</label>
            <input 
              type="email" 
              required 
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="name@email.com"
              className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
            />
          </div>

          <div className="space-y-1.5 relative">
            <div className="flex justify-between items-center">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Master Password</label>
              <button 
                type="button" 
                onClick={handleGeneratePassword}
                className="text-[10px] font-bold uppercase tracking-widest text-blue-600 hover:text-blue-800 transition-colors flex items-center gap-1 focus:outline-none"
              >
                <Key size={10} /> Generate Secure
              </button>
            </div>
            <input 
              type="text" 
              required 
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••••••"
              className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm font-mono shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
            />
          </div>

          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Confirm Password</label>
              {passwordsMatch && (
                <span className="text-emerald-600 flex items-center gap-0.5 text-[10px] font-bold uppercase tracking-wider">
                  ✓ Match
                </span>
              )}
            </div>
            <input 
              type="password" 
              required 
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="••••••••••••"
              className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm font-mono shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
            />
          </div>

          {/* Footer Actions */}
          <div className="flex items-center justify-end gap-3 pt-4 border-t border-[#f4f4f5] mt-2">
            <button 
              type="button" 
              onClick={onClose} 
              className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors px-2 py-1"
            >
              Cancel
            </button>
            <button 
              type="submit" 
              disabled={isSubmitting || !name.trim() || !email.trim() || !password.trim() || !confirmPassword.trim()}
              className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-all flex items-center justify-center gap-2 whitespace-nowrap shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95"
            >
              {isSubmitting && <Loader2 size={12} className="animate-spin" />}
              {isSubmitting ? "Registering..." : "Register Client"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
