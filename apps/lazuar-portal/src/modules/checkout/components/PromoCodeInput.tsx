// apps/lazuar-portal/src/modules/checkout/components/PromoCodeInput.tsx
import { useState } from "react";

export function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(" ");
}

interface PromoCodeInputProps {
  isApplied: boolean;
  isValidating: boolean;
  error: string | null;
  onApply: (code: string) => void;
  onRemove: () => void;
}

export function PromoCodeInput({
  isApplied,
  isValidating,
  error,
  onApply,
  onRemove
}: PromoCodeInputProps) {
  const [code, setCode] = useState("");

  const handleApplyClick = () => {
    if (code.trim()) {
      onApply(code.trim().toUpperCase());
    }
  };

  const handleRemoveClick = () => {
    setCode("");
    onRemove();
  };

  return (
    <div className="space-y-3">
      <label className="text-xs font-bold uppercase tracking-widest text-muted-foreground">
        Promo Code
      </label>
      <div className="flex gap-2">
        <input
          type="text"
          value={code}
          onChange={(e) => {
            setCode(e.target.value.toUpperCase());
            if (isApplied) {
              onRemove();
            }
          }}
          placeholder="ENTER CODE"
          disabled={isValidating}
          className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono uppercase shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground disabled:opacity-50"
        />
        {isApplied ? (
          <button
            type="button"
            onClick={handleRemoveClick}
            className="h-10 px-4 border border-border bg-background hover:bg-accent hover:text-accent-foreground rounded-none text-xs font-bold uppercase tracking-widest transition-colors"
          >
            Remove
          </button>
        ) : (
          <button
            type="button"
            onClick={handleApplyClick}
            disabled={isValidating || !code.trim()}
            className="h-10 px-4 border border-border bg-background hover:bg-accent hover:text-accent-foreground rounded-none text-xs font-bold uppercase tracking-widest disabled:opacity-50 transition-colors flex items-center justify-center min-w-[70px]"
          >
            {isValidating ? (
              <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 12a9 9 0 1 1-6.219-8.56" />
              </svg>
            ) : (
              "Apply"
            )}
          </button>
        )}
      </div>
      {error && (
        <p className="text-xs font-medium text-red-500 flex items-center gap-1">
          <svg className="h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
          {error}
        </p>
      )}
    </div>
  );
}
