export type VerificationActor = { id: string; role: string; token: string; revision: number; vendorId?: string };
type Session = { user: { id: string; role: string } | null; token: string | null; sessionRevision: number };
export type UploadedVerificationDocument = { url: string; originalName: string; size?: number; extension?: string };

export function verificationActorIsCurrent(actor: VerificationActor, session: Session): boolean {
  return !!actor.id && !!actor.token && actor.id === session.user?.id && actor.role === session.user.role &&
    actor.token === session.token && actor.revision === session.sessionRevision;
}
export function managedVerificationPath(reference: string, vendorId: string): string | null {
  const owner = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  if (!owner.test(vendorId)) return null;
  const prefix = `/uploads/documents/${vendorId}/`;
  return reference.startsWith(prefix) && /^[0-9a-f]{32}\.(pdf|doc|docx|jpg|png)$/.test(reference.slice(prefix.length)) ? reference : null;
}
export function legacyVerificationHref(reference: string): string | null {
  try {
    if (reference !== reference.trim() || reference.length > 2048 || reference.includes('\\')) return null;
    const url = new URL(reference);
    return url.protocol === 'https:' && !!url.hostname && !url.username && !url.password ? reference : null;
  } catch { return null; }
}
export function validateVerificationFiles(files: readonly { name: string; type: string; size: number }[], existingCount = 0): void {
  if (!files.length || files.length + existingCount > 10) throw new Error('Mỗi hồ sơ tối đa 10 tài liệu.');
  const types: Record<string, string> = { pdf: 'application/pdf', doc: 'application/msword', docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', jpg: 'image/jpeg', jpeg: 'image/jpeg', png: 'image/png' };
  let total = 0;
  for (const file of files) {
    const expected = types[file.name.split('.').pop()?.toLowerCase() ?? ''];
    if (!expected || file.type.toLowerCase() !== expected || file.size <= 0 || file.size > 20 * 1024 * 1024)
      throw new Error('Chọn PDF, Word DOC/DOCX hoặc ảnh JPG/PNG, tối đa 20 MB mỗi tệp.');
    total += file.size;
  }
  if (total > 32 * 1024 * 1024) throw new Error('Mỗi lần tải lên tối đa 32 MB. Hãy chia thành các lần nhỏ hơn.');
}
export async function uploadVerificationFiles(files: File[], vendorId: string, options: {
  existingCount: number; signal: AbortSignal; isCurrent: () => boolean;
  send: (files: File[], signal: AbortSignal) => Promise<unknown>;
}): Promise<UploadedVerificationDocument[]> {
  const requireCurrent = () => { if (options.signal.aborted || !options.isCurrent()) throw new Error('Phiên đăng nhập đã thay đổi. Vui lòng mở lại hồ sơ.'); };
  requireCurrent(); validateVerificationFiles(files, options.existingCount);
  const value = await options.send(files, options.signal);
  requireCurrent();
  if (!Array.isArray(value) || value.length !== files.length || value.some(item => !item || typeof item.url !== 'string' ||
    !managedVerificationPath(item.url, vendorId) || typeof item.originalName !== 'string'))
    throw new Error('Máy chủ chưa trả đủ tài liệu đã lưu của bạn. Vui lòng tải lại.');
  return value;
}
export async function fetchVerificationDocument(reference: string, vendorId: string, options: {
  signal: AbortSignal; isCurrent: () => boolean; send: (path: string, signal: AbortSignal) => Promise<Blob>;
}): Promise<Blob> {
  const path = managedVerificationPath(reference, vendorId);
  const requireCurrent = () => { if (options.signal.aborted || !options.isCurrent()) throw new Error('Phiên đăng nhập đã thay đổi.'); };
  requireCurrent();
  if (!path) throw new Error('Đường dẫn tài liệu không hợp lệ.');
  const result = await options.send(path, options.signal);
  requireCurrent();
  if (!(result instanceof Blob) || result.size === 0) throw new Error('Chưa nhận được tài liệu.');
  return result;
}
