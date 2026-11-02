import { useEffect, useId, useRef, useState } from "react";
import { ImagePlus, Loader2, Trash2 } from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import { apiError } from "../api";
import { prepareInvitationImage } from "../invitations/invitationImages";
import { articleImageSrc, isCurrentCmsActor, requireUploadedArticleImage, validateArticleImageFile } from "./articleCmsModel";
import type { CmsActor } from "./articleCmsModel";

export function ArticleCoverPicker({ value, onChange, onUploadingChange, disabled, actor }: {
  value: string; onChange: (value: string) => void; onUploadingChange: (busy: boolean) => void;
  disabled: boolean; actor: CmsActor;
}) {
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);
  const mounted = useRef(true);
  const inFlight = useRef(false);
  const abort = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState("");
  const current = () => mounted.current && isCurrentCmsActor(actor, useAuthStore.getState());
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; abort.current?.abort(); };
  }, []);
  const upload = async (file?: File) => {
    if (!file || disabled || inFlight.current) return;
    const validation = validateArticleImageFile(file);
    if (validation) { setError(validation); if (input.current) input.current.value = ""; return; }
    if (!current()) { setError("Phiên biên tập đã thay đổi. Vui lòng mở lại bài viết."); return; }
    inFlight.current = true;
    setBusy(true); onUploadingChange(true); setError(""); setStatus("Đang tối ưu ảnh...");
    const controller = new AbortController(); abort.current = controller;
    try {
      const prepared = await prepareInvitationImage(file);
      if (!current() || controller.signal.aborted) return;
      const body = new FormData(); body.append("file", prepared);
      setStatus("Đang tải ảnh lên...");
      const result = await axiosClient.post<{ url: string }>("/api/admin/articles/images", body, {
        headers: { "Content-Type": "multipart/form-data", Authorization: `Bearer ${actor.token}` },
        signal: controller.signal, timeout: 60000,
      });
      if (!current() || controller.signal.aborted) return;
      onChange(requireUploadedArticleImage(result.data.url));
      setStatus("Ảnh đã tải lên. Bấm Lưu bài để lưu ảnh vào bài viết.");
    } catch (failure: unknown) {
      if (current() && !controller.signal.aborted) {
        setStatus("");
        setError(failure instanceof Error && !("response" in failure) ? failure.message : apiError(failure));
      }
    } finally {
      inFlight.current = false;
      if (mounted.current) {
        setBusy(false); onUploadingChange(false);
        if (input.current) input.current.value = "";
      }
    }
  };
  const src = articleImageSrc(value);
  return <section className="rounded-2xl border border-[#eadbd4] bg-[#fcf6ea] p-4 space-y-3" aria-labelledby={`${inputId}-title`} aria-busy={busy}>
    <h4 id={`${inputId}-title`} className="font-semibold text-[#233a42]">Ảnh bìa bài viết</h4>
    <p className="text-xs leading-relaxed text-slate-600">Chọn ảnh từ điện thoại hoặc máy tính. Hỗ trợ JPG, PNG, WebP, tối đa 8 MB.</p>
    {src ? <img src={src} alt="Ảnh bìa đang chọn" className="max-h-64 w-full rounded-xl object-cover border border-[#eadbd4]" /> : <div className="flex min-h-32 items-center justify-center rounded-xl border border-dashed border-[#d5b9ab] bg-[#fffaf4] text-sm text-slate-500"><ImagePlus size={24} className="mr-2" aria-hidden="true" />Chưa có ảnh bìa</div>}
    <div className="flex flex-wrap items-center gap-3">
      <input id={inputId} ref={input} type="file" aria-label="Tải ảnh bìa bài viết" accept="image/jpeg,image/png,image/webp" className="sr-only" disabled={disabled || busy} onChange={event => void upload(event.target.files?.[0])} />
      <button type="button" className="btn btn-outline" disabled={disabled || busy} onClick={() => input.current?.click()}>{busy ? <Loader2 size={16} className="animate-spin" aria-hidden="true" /> : <ImagePlus size={16} aria-hidden="true" />}{busy ? "Đang tải ảnh..." : value ? "Thay ảnh bìa" : "Chọn ảnh bìa"}</button>
      {value && <button type="button" className="btn btn-outline" disabled={disabled || busy} onClick={() => { if (!current()) return; onChange(""); setError(""); setStatus("Đã bỏ ảnh khỏi bản đang sửa. Bấm Lưu bài để cập nhật."); }}><Trash2 size={15} aria-hidden="true" />Bỏ ảnh</button>}
    </div>
    {status && <p role="status" className="text-xs text-slate-600">{status}</p>}
    {error && <p role="alert" className="text-sm text-red-700">{error}{value && " Ảnh hiện tại vẫn được giữ lại."}</p>}
  </section>;
}
