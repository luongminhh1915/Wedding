export const ACCOUNT_TABS = ['personal', 'edit', 'security', 'settings'] as const;
export type AccountTab = typeof ACCOUNT_TABS[number];
export function accountTab(value: string | null): AccountTab {
  return ACCOUNT_TABS.includes(value as AccountTab) ? value as AccountTab : 'personal';
}
export function validateDisplayName(value: string): string | null {
  const hasControl = [...value].some(char => { const code = char.charCodeAt(0); return code < 32 || (code >= 127 && code <= 159); });
  if (!value.trim() || value.trim().length > 150 || hasControl)
    return 'Họ và tên cần từ 1 đến 150 ký tự, không chứa ký tự điều khiển.';
  return null;
}
