export const invitationAudioMaxBytes = 12 * 1024 * 1024;
const uploadPattern = /^\/uploads\/invitation-audio\/[a-f0-9]{32}\/[a-f0-9]{32}\.mp3$/;
const videoPattern = /^[A-Za-z0-9_-]{11}$/;
const youtubeHosts = new Set(["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com"]);
const shortHosts = new Set(["youtu.be", "www.youtu.be"]);
const embedHosts = new Set(["youtube-nocookie.com", "www.youtube-nocookie.com"]);

export type InvitationMusicSource =
  | { kind: "none" }
  | { kind: "upload" | "legacy"; url: string }
  | { kind: "youtube"; url: string; videoId: string }
  | { kind: "invalid"; message: string };

/** Derive source from the existing persisted MusicUrl; never guess an iframe URL. */
export function invitationMusicSource(value?: string | null): InvitationMusicSource {
  const raw = value?.trim() || "";
  if (!raw) return { kind: "none" };
  const invalid = (): InvitationMusicSource => ({ kind: "invalid", message: "Chọn tệp MP3 hoặc nhập liên kết YouTube hợp lệ (video đơn, không phải playlist)." });
  if (raw.length > 500 || raw.includes("\\") || Array.from(raw).some(char => {
    const code = char.charCodeAt(0);
    return code <= 32 || (code >= 127 && code <= 159);
  })) return invalid();
  if (uploadPattern.test(raw)) return { kind: "upload", url: raw };
  let url: URL;
  try { url = new URL(raw); } catch { return invalid(); }
  if (!["http:", "https:"].includes(url.protocol) || url.username || url.password) return invalid();
  const host = url.hostname.toLowerCase();
  if (youtubeHosts.has(host) || shortHosts.has(host) || embedHosts.has(host)) {
    if (url.protocol !== "https:" || url.port) return invalid();
    let id = "";
    const parts = url.pathname.split("/").filter(Boolean);
    if (shortHosts.has(host) && parts.length === 1 && url.pathname === `/${parts[0]}`) id = parts[0];
    if (youtubeHosts.has(host) && url.pathname === "/watch" && url.searchParams.getAll("v").length === 1) {
      // Keep the raw ID: URLSearchParams decodes percent escapes before validation.
      try {
        const pair = url.search.slice(1).split("&").map(part => part.split("=", 2)).find(part => decodeURIComponent(part[0]) === "v");
        id = pair?.[1] || "";
      } catch { return invalid(); }
    }
    if ((youtubeHosts.has(host) || embedHosts.has(host)) && parts.length === 2 && url.pathname === `/${parts.join("/")}` && (parts[0] === "embed" || (!embedHosts.has(host) && ["shorts", "live"].includes(parts[0])))) id = parts[1];
    if (!videoPattern.test(id)) return invalid();
    return { kind: "youtube", url: `https://www.youtube.com/watch?v=${id}`, videoId: id };
  }
  if (host.includes("youtube") || host.includes("youtu.be")) return invalid();
  return { kind: "legacy", url: raw };
}

export function invitationAudioSrc(url: string, apiBase = import.meta.env?.VITE_API_URL || "") {
  return uploadPattern.test(url) ? `${apiBase.replace(/\/$/, "")}${url}` : url;
}

export function invitationAudioFileError(file: Pick<File, "name" | "type" | "size">): string | null {
  if (!/\.mp3$/i.test(file.name) || !["audio/mpeg", "audio/mp3", ""].includes(file.type.toLowerCase()))
    return "Chỉ hỗ trợ tệp MP3. Hãy xuất bản nhạc sang MP3 trước khi tải lên.";
  if (file.size <= 0 || file.size > invitationAudioMaxBytes) return "Tệp nhạc cần có dữ liệu và không vượt quá 12 MB.";
  return null;
}
