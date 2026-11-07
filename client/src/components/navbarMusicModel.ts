export interface NavbarMusicTrack {
  id: string;
  title: string;
  url?: string;
  type: "synth" | "audio" | "youtube";
  youtubeId?: string;
  isCustom?: boolean;
  hidden?: boolean;
}

export const DEFAULT_NAVBAR_TRACKS: NavbarMusicTrack[] = [
  { id: "canon-synth", title: "Canon in D (Piano Synth)", type: "synth" },
  { id: "canon-romantic", title: "Âm thanh lễ hội ngoài trời", url: "https://actions.google.com/sounds/v1/ambiences/outdoor_celebration.ogg", type: "audio" },
];

const managedMusic = /^\/uploads\/invitation-audio\/c8e62673872e40a09bed56b3d42c8ebf\/[a-f0-9]{32}\.mp3$/;
const hasControls = (value: string) => Array.from(value).some(char => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127);

export function extractYouTubeId(raw: unknown): string | null {
  if (typeof raw !== "string") return null;
  const value = raw.trim();
  if (!value || value.length > 500 || hasControls(value) || value.includes("\\")) return null;
  if (/^[A-Za-z0-9_-]{11}$/.test(value)) return value;
  try {
    const url = new URL(value);
    if (url.protocol !== "https:" || !url.hostname || url.username || url.password || (url.port && url.port !== "443")) return null;
    const host = url.hostname.toLowerCase();
    if (host === "youtu.be") {
      const id = url.pathname.replace(/^\/+/, "");
      return /^[A-Za-z0-9_-]{11}$/.test(id) ? id : null;
    }
    if (host === "www.youtube.com" || host === "youtube.com" || host === "m.youtube.com") {
      if (url.pathname.startsWith("/shorts/") || url.pathname.startsWith("/embed/")) {
        const parts = url.pathname.split("/").filter(Boolean);
        if (parts.length >= 2 && /^[A-Za-z0-9_-]{11}$/.test(parts[1])) return parts[1];
      }
      const v = url.searchParams.get("v");
      if (v && /^[A-Za-z0-9_-]{11}$/.test(v)) return v;
    }
  } catch {
    return null;
  }
  return null;
}

export function navbarMusicUrl(raw: unknown): string {
  if (typeof raw !== "string") throw new Error("Cần đường dẫn âm thanh hợp lệ.");
  const value = raw.trim();
  if (!value || value.length > 500 || hasControls(value) || value.includes("\\") || /%(?:25)*(?:2e|2f|5c|00)/i.test(value))
    throw new Error("Đường dẫn âm thanh không hợp lệ.");
  if (managedMusic.test(value)) return value;
  let url: URL;
  try { url = new URL(value); } catch { throw new Error("Dùng liên kết HTTPS trực tiếp đến tệp MP3 hoặc OGG."); }
  if (url.protocol !== "https:" || !url.hostname || url.username || url.password || (url.port && url.port !== "443") || !/\.(mp3|ogg)$/i.test(url.pathname))
    throw new Error("Dùng liên kết HTTPS trực tiếp đến tệp MP3 hoặc OGG, không chứa thông tin đăng nhập.");
  return url.href;
}

export function normalizeNavbarPlaylist(value: unknown): NavbarMusicTrack[] {
  if (!Array.isArray(value) || value.length > 20) throw new Error("Danh sách nhạc chỉ được có tối đa 20 bài.");
  const ids = new Set<string>();
  return value.map(item => {
    if (!item || typeof item !== "object") throw new Error("Thông tin bài hát không hợp lệ.");
    const track = item as Record<string, unknown>;
    if (typeof track.id !== "string" || !/^[A-Za-z0-9_-]{1,64}$/.test(track.id) || ids.has(track.id))
      throw new Error("Mã bài hát không hợp lệ hoặc bị trùng.");
    ids.add(track.id);
    if (typeof track.title !== "string" || !track.title.trim() || !track.title.trim().length || track.title.trim().length > 150 || hasControls(track.title))
      throw new Error("Tên bài hát cần từ 1 đến 150 ký tự.");
    const hidden = track.hidden === true;
    if (track.type === "synth") {
      if (track.id !== "canon-synth" || (track.url != null && track.url !== "")) throw new Error("Bản phối tổng hợp không hợp lệ.");
      return { id: track.id, title: track.title.trim(), type: "synth", isCustom: false, hidden };
    }
    if (track.type === "youtube") {
      const ytId = extractYouTubeId(track.youtubeId || track.url);
      if (!ytId) throw new Error("Cần đường dẫn YouTube HTTPS hợp lệ (ví dụ: https://www.youtube.com/watch?v=...) hoặc mã video 11 ký tự.");
      const validUrl = typeof track.url === "string" && track.url.trim().startsWith("https://")
        ? track.url.trim()
        : `https://www.youtube.com/watch?v=${ytId}`;
      return { id: track.id, title: track.title.trim(), type: "youtube", url: validUrl, youtubeId: ytId, isCustom: track.isCustom === true, hidden };
    }
    if (track.type !== "audio") throw new Error("Loại bài hát không hợp lệ.");
    return { id: track.id, title: track.title.trim(), type: "audio", url: navbarMusicUrl(track.url), isCustom: track.isCustom === true, hidden };
  });
}

export function navbarMusicVolume(raw: unknown): number {
  const value = typeof raw === "number" ? raw : typeof raw === "string" && raw.trim() ? Number(raw) : NaN;
  return Number.isFinite(value) ? Math.max(0, Math.min(1, value)) : 0.45;
}

type MusicSession = { user: { id: string; role: string } | null; token: string | null; validated: boolean; sessionRevision: number };
export type NavbarMusicActor = { id: string; role: string; token: string; revision: number };
export function navbarMusicActor(session: MusicSession): NavbarMusicActor | null {
  return session.validated && !!session.token && !!session.user?.id && ["SuperAdmin", "Moderator"].includes(session.user.role)
    ? { id: session.user.id, role: session.user.role, token: session.token, revision: session.sessionRevision } : null;
}
export function navbarMusicActorIsCurrent(actor: NavbarMusicActor, session: MusicSession): boolean {
  const current = navbarMusicActor(session);
  return !!current && current.id === actor.id && current.role === actor.role && current.token === actor.token && current.revision === actor.revision;
}

export async function saveNavbarPlaylist(value: unknown, options: {
  isCurrent: () => boolean;
  signal: AbortSignal;
  send: (tracks: NavbarMusicTrack[], signal: AbortSignal) => Promise<unknown>;
}): Promise<NavbarMusicTrack[]> {
  const requireCurrent = () => { if (options.signal.aborted || !options.isCurrent()) throw new Error("Phiên quản trị đã thay đổi. Vui lòng đăng nhập lại."); };
  requireCurrent();
  const proposed = normalizeNavbarPlaylist(value);
  const response = await options.send(proposed, options.signal);
  requireCurrent();
  if (!response || typeof response !== "object" || (response as { success?: unknown }).success !== true)
    throw new Error("Máy chủ chưa xác nhận lưu danh sách nhạc.");
  return normalizeNavbarPlaylist((response as { tracks?: unknown }).tracks);
}
