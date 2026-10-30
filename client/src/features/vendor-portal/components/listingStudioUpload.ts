import { isServiceStudioImage } from "../../listings/serviceDetailsModel.ts";

export type ListingStudioActor = { id?: string; token: string | null; revision: number };
type StudioSession = { user: { id: string; role: string } | null; token: string | null; sessionRevision: number };
export function listingStudioActorIsCurrent(actor: ListingStudioActor, session: StudioSession): boolean {
  return !!actor.id && !!actor.token && actor.id === session.user?.id && session.user.role === "VendorOwner" &&
    actor.token === session.token && actor.revision === session.sessionRevision;
}
export function validateStudioUploadFiles(files: readonly { name: string; type: string; size: number }[]): void {
  if (!files.length || files.length > 10) throw new Error("Mỗi lần chọn từ 1 đến 10 ảnh.");
  for (const file of files) {
    const extension = file.name.split(".").pop()?.toLowerCase();
    const expected = extension === "jpg" || extension === "jpeg" ? "image/jpeg" : extension === "png" ? "image/png" : extension === "webp" ? "image/webp" : null;
    if (!expected || file.type !== expected || file.size <= 0 || file.size > 8 * 1024 * 1024)
      throw new Error("Chọn ảnh JPG, PNG hoặc WebP, tối đa 8 MB mỗi ảnh.");
  }
}
export async function uploadStudioImages(files: File[], options: {
  isCurrent: () => boolean;
  signal: AbortSignal;
  prepare: (file: File) => Promise<File>;
  send: (prepared: File[], signal: AbortSignal) => Promise<unknown>;
}): Promise<string[]> {
  const requireCurrent = () => { if (options.signal.aborted || !options.isCurrent()) throw new Error("Phiên nhà cung cấp đã thay đổi. Vui lòng mở lại trình soạn thảo."); };
  requireCurrent(); validateStudioUploadFiles(files);
  const prepared: File[] = [];
  for (const file of files) { prepared.push(await options.prepare(file)); requireCurrent(); }
  const urls = await options.send(prepared, options.signal);
  requireCurrent();
  if (!Array.isArray(urls) || urls.length !== files.length || urls.some(url => typeof url !== "string" || !isServiceStudioImage(url)))
    throw new Error("Máy chủ chưa trả đủ đường dẫn ảnh đã lưu. Vui lòng tải lại ảnh.");
  return urls;
}
