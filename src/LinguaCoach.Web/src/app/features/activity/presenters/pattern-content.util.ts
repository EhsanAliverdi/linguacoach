import { ActivityDto } from '../../../core/models/activity.models';

export function parsePatternContent(activity: ActivityDto): Record<string, unknown> {
  const content: unknown = activity.contentJson;
  if (!content) return {};
  if (typeof content !== 'string') return content as Record<string, unknown>;

  try {
    const parsed = JSON.parse(content);
    return parsed && typeof parsed === 'object' ? parsed as Record<string, unknown> : {};
  } catch {
    return {};
  }
}

export function stringValue(raw: Record<string, unknown>, key: string): string | undefined {
  const v = raw[key];
  return typeof v === 'string' && v.trim() ? v : undefined;
}

export function stringArray(raw: Record<string, unknown>, key: string): string[] | undefined {
  const v = raw[key];
  if (!Array.isArray(v)) return undefined;
  const items = v.filter((x): x is string => typeof x === 'string' && x.trim().length > 0);
  return items.length ? items : undefined;
}
