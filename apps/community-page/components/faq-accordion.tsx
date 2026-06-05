// components/faq-accordion.tsx
"use client";

import { useState } from "react";
import { ChevronDownIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface FaqItem {
  id: string;
  question: string;
  answer: string;
}

interface FaqAccordionProps {
  items: FaqItem[];
  defaultValue?: string;
  className?: string;
}

export function FaqAccordion({ items, defaultValue, className }: FaqAccordionProps) {
  const [openItem, setOpenItem] = useState<string | null>(defaultValue || items[0]?.id || null);

  return (
    <div className={cn("w-full flex flex-col", className)}>
      {items.map((faq) => {
        const isOpen = openItem === faq.id;

        return (
          <div key={faq.id} className="border-b border-border/40 last:border-0">
            
            <button
              type="button"
              onClick={() => setOpenItem(isOpen ? null : faq.id)}
              aria-expanded={isOpen}
              // Added relative z-10 to guarantee it is clickable and not covered by other divs
              className="relative z-10 flex w-full items-center justify-between py-4 text-left text-sm font-medium transition-colors hover:text-foreground/70 outline-none focus-visible:ring-2 focus-visible:ring-ring text-foreground/90 rounded-none cursor-pointer"
            >
              <span>{faq.question}</span>
              <ChevronDownIcon 
                className={cn(
                  "shrink-0 size-4 text-muted-foreground transition-transform duration-300", 
                  isOpen && "rotate-180" 
                )} 
              />
            </button>

            {/* Foolproof max-height animation (avoids all Tailwind Grid/Compiler bugs) */}
            <div 
              className={cn(
                "overflow-hidden transition-all duration-300 ease-in-out",
                isOpen ? "max-h-[500px] opacity-100" : "max-h-0 opacity-0"
              )}
            >
              <div className="pb-5 pr-6 text-sm text-muted-foreground leading-relaxed">
                {faq.answer}
              </div>
            </div>

          </div>
        );
      })}
    </div>
  );
}
