export const ARTICLE_IMAGE_MAX_BYTES = 8 * 1024 * 1024;
const managedImage = /^\/uploads\/articles\/[a-fA-F0-9]{32}\.(jpg|png|webp)$/;
export type CmsActor = { id: string | undefined; token: string | null; revision: number; role: string | undefined };
type CmsSession = { user: { id: string; role: string } | null; token: string | null; sessionRevision: number };

export function isCurrentCmsActor(actor: CmsActor, session: CmsSession): boolean {
  return Boolean(actor.id && actor.token && (actor.role === "SuperAdmin" || actor.role === "Moderator") &&
    actor.id === session.user?.id && actor.role === session.user.role &&
    actor.token === session.token && actor.revision === session.sessionRevision);
}

export function validateArticleImageFile(file: Pick<File, "name" | "type" | "size">): string {
  if (!/\.(jpe?g|png|webp)$/i.test(file.name) || !["image/jpeg", "image/png", "image/webp"].includes(file.type))
    return "Chọn ảnh JPG, PNG hoặc WebP. Ảnh SVG/HEIC chưa được hỗ trợ.";
  if (file.size <= 0 || file.size > ARTICLE_IMAGE_MAX_BYTES) return "Ảnh cần có dữ liệu và không vượt quá 8 MB.";
  return "";
}

export function requireUploadedArticleImage(value: unknown): string {
  if (typeof value !== "string" || !managedImage.test(value))
    throw new Error("Máy chủ chưa trả về đường dẫn ảnh đã lưu hợp lệ. Ảnh cũ vẫn được giữ lại.");
  return value;
}

/** Keep stored upload paths portable when frontend and API use different origins. */
export function articleImageSrc(value?: string | null, apiBase = import.meta.env?.VITE_API_URL || ""): string {
  if (!value) return "";
  if (managedImage.test(value)) return `${apiBase.replace(/\/$/, "")}${value}`;
  try {
    const url = new URL(value);
    return url.protocol === "https:" && !url.username && !url.password ? value : "";
  } catch { return ""; }
}

export function articleSlug(title: string): string {
  return title.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[đĐ]/g, "d")
    .toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 200);
}
