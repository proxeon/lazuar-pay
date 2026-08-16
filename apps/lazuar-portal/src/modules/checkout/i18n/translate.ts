import { interpolate } from "./format";
import type { Locale } from "./locales";
import { messages, type MessageKey } from "./messages";

export function t(
  locale: Locale,
  key: MessageKey,
  vars?: Record<string, string | number>,
): string {
  return interpolate(messages[locale][key], vars);
}
