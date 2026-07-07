/** Minimal HTML-escaping for admin-authored label text rendered directly into a custom Form.io
 *  component's raw HTML template (bypassing Form.io's own label-rendering template system). */
export function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
