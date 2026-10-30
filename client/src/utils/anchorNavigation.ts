/** Decode URL fragments without allowing malformed external links to crash a layout effect. */
export function safeAnchorId(hash: string | null | undefined): string | null {
  if (!hash?.startsWith('#') || hash.length === 1) return null;
  try {
    return decodeURIComponent(hash.slice(1));
  } catch {
    return null;
  }
}
