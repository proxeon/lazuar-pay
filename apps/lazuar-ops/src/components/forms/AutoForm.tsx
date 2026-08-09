// apps/lazuar-ops/src/components/forms/AutoForm.tsx
import { useState } from "react";
import { Send } from "lucide-react";

interface AutoFormProps {
  schema: any;
  prefillData?: any;
  onSubmit: (data: any) => void;
  onCancel: () => void;
}

const formatDateTimeLocal = (isoString?: string) => {
  if (!isoString) return "";
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return "";
    const tzOffset = date.getTimezoneOffset() * 60000;
    return new Date(date.getTime() - tzOffset).toISOString().slice(0, 16);
  } catch {
    return "";
  }
};

export default function AutoForm({ schema, prefillData, onSubmit, onCancel }: AutoFormProps) {
  const properties = schema?.properties || {};
  const requiredFields: string[] = schema?.required || [];

  const [formData, setFormData] = useState<Record<string, any>>(() => {
    const initial: Record<string, any> = {};
    Object.entries(properties).forEach(([key, propSchema]: [string, any]) => {
      if (prefillData && prefillData[key] !== undefined) {
        initial[key] = prefillData[key];
      } else if (propSchema.enum && propSchema.enum.length > 0) {
        initial[key] = propSchema.enum[0];
      } else if (propSchema.type === "boolean") {
        initial[key] = false;
      } else {
        initial[key] = "";
      }
    });
    return initial;
  });

  const handleChange = (key: string, value: any, type: string, format?: string) => {
    setFormData((prev) => {
      const updated = { ...prev };
      if (type === "number" || type === "integer") {
        updated[key] = value === "" ? "" : Number(value);
      } else if (type === "boolean") {
        updated[key] = Boolean(value);
      } else if (format === "date-time" && value) {
        updated[key] = new Date(value).toISOString();
      } else {
        updated[key] = value;
      }
      return updated;
    });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    
    const cleanedData = { ...formData };
    Object.keys(cleanedData).forEach(key => {
      if (cleanedData[key] === "" || cleanedData[key] === undefined) {
        delete cleanedData[key];
      }
    });

    onSubmit(cleanedData);
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col font-sans">
      <div className="p-4 space-y-4">
        {Object.entries(properties).map(([key, propSchema]: [string, any]) => {
          if (key === "_meta") return null;

          const isRequired = requiredFields.includes(key);
          const type = propSchema.type || "string";
          const format = propSchema.format;
          const enumValues = propSchema.enum;
          const description = propSchema.description;
          const value = formData[key] ?? "";

          return (
            <div key={key} className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] flex items-center gap-1">
                {key.replace(/([A-Z])/g, " $1").trim()}
                {isRequired && <span className="text-rose-500">*</span>}
              </label>

              {enumValues ? (
                <select
                  required={isRequired}
                  value={value}
                  onChange={(e) => handleChange(key, e.target.value, type)}
                  className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                >
                  {enumValues.map((opt: string) => (
                    <option key={opt} value={opt}>{opt}</option>
                  ))}
                </select>
              ) : format === "date-time" ? (
                <input
                  type="datetime-local"
                  required={isRequired}
                  value={formatDateTimeLocal(value)}
                  onChange={(e) => handleChange(key, e.target.value, type, format)}
                  className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                />
              ) : type === "boolean" ? (
                <label className="relative inline-flex items-center cursor-pointer mt-1">
                  <input
                    type="checkbox"
                    className="sr-only peer"
                    checked={value}
                    onChange={(e) => handleChange(key, e.target.checked, type)}
                  />
                  <div className="w-9 h-5 bg-[#e5e5e5] peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-[#09090b]/20 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-[#e5e5e5] after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-[#09090b]"></div>
                </label>
              ) : type === "array" ? (
                <textarea
                  required={isRequired}
                  value={Array.isArray(value) ? value.join(", ") : value}
                  onChange={(e) => handleChange(key, e.target.value.split(",").map(s => s.trim()).filter(Boolean), type)}
                  placeholder={description || "Comma separated values"}
                  className="flex min-h-[60px] w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-2 text-[13px] transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b] resize-y"
                />
              ) : (
                <input
                  type={type === "number" || type === "integer" ? "number" : "text"}
                  step={type === "number" ? "any" : undefined}
                  required={isRequired}
                  value={value}
                  placeholder={description}
                  onChange={(e) => handleChange(key, e.target.value, type)}
                  className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                />
              )}
            </div>
          );
        })}
      </div>

      <div className="px-4 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 mt-2">
        <button
          type="button"
          onClick={onCancel}
          className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors"
        >
          Cancel
        </button>
        <button
          type="submit"
          className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Send size={13} /> Submit Data
        </button>
      </div>
    </form>
  );
}
