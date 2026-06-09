import { useState } from "react";
import { X, Loader2, Key, Check } from "lucide-react";
import { toast } from "sonner";

interface ResetPasswordModalProps {
  userEmail: string;
  onClose: () => void;
  onSuccess: (password: string) => void;
}

export default function ResetPasswordModal({ userEmail, onClose, onSuccess }: ResetPasswordModalProps) {
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Generate a random mock secure password
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
    if (!password.trim() || !confirmPassword.trim()) return;

    if (password !== confirmPassword) {
      toast.error("Passwords do not match.");
      return;
    }

    if (password.length < 6) {
      toast.error("Password must be at least 6 characters.");
      return;
    }

    setIsSubmitting(true);

    // Simulate 600ms database crypt write latency
    setTimeout(() => {
      setIsSubmitting(false);
      onSuccess(password);
    }, 600);
  };

  const passwordsMatch = password && password === confirmPassword;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      {/* Backdrop overlay */}
      <div 
        className="absolute inset-0 bg-black/40 backdrop-blur-sm" 
        onClick={onClose} 
      />
      
      {/* Modal Container */}
      <div className="relative bg-white border border-[#e5e5e5] rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-sm overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
        
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] shrink-0">
          <div>
            <h3 className="text-[14px] font-semibold tracking-tight text-[#09090b]">Reset Password</h3>
            <p className="text-[11px] text-[#71717a] mt-0.5">Assign a new password for {userEmail}.</p>
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
          
          <div className="space-y-1.5 relative">
            <div className="flex justify-between items-center">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">New Password</label>
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
                  <Check size={10} /> Match
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

          {/* Actions */}
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
              disabled={isSubmitting || !password.trim() || !confirmPassword.trim()}
              className="h-10 px-5 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2"
            >
              {isSubmitting && <Loader2 size={12} className="animate-spin" />}
              {isSubmitting ? "Updating..." : "Save Password"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
