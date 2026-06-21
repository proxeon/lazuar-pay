import { useState, useCallback, useRef } from "react";
import { Copy, Check } from "lucide-react";
import { cn } from "../../../lib/utils";

interface QuickCopyProps {
  text: string;
  className?: string;
  iconSize?: number;
}

export default function QuickCopy({ text, className, iconSize = 14 }: QuickCopyProps) {
  const [copied, setCopied] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleCopy = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();

    if (timeoutRef.current) clearTimeout(timeoutRef.current);

    const onSuccess = () => {
      setCopied(true);
      timeoutRef.current = setTimeout(() => setCopied(false), 2000);
    };

    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).then(onSuccess).catch(() => fallbackCopy(text, onSuccess));
    } else {
      fallbackCopy(text, onSuccess);
    }
  }, [text]);

  return (
    <button
      onClick={handleCopy}
      className={cn(
        "p-1.5 transition-colors rounded-sm flex items-center justify-center shrink-0",
        copied ? "text-emerald-600 bg-emerald-50/50" : "text-[#a1a1aa] hover:text-[#09090b] hover:bg-[#f4f4f5]",
        className
      )}
      title={copied ? "Copied!" : "Copy"}
    >
      {copied ? <Check size={iconSize} /> : <Copy size={iconSize} />}
    </button>
  );
}

function fallbackCopy(text: string, onSuccess: () => void) {
  try {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    Object.assign(textarea.style, { position: "fixed", left: "-9999px", top: "-9999px", opacity: "0" });
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand("copy");
    document.body.removeChild(textarea);
    onSuccess();
  } catch {
    console.warn("Copy failed");
  }
}
