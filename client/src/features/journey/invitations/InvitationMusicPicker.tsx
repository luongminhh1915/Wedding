import { useEffect, useId, useRef, useState } from "react";
import axiosClient from "../../../services/axiosClient";
import { apiError } from "../api";
import { invitationAudioFileError, invitationMusicSource } from "./invitationMusic";
import { InvitationMusicPlayer } from "./InvitationMusicPlayer";

export function InvitationMusicPicker({ value = "", onChange, onUploadingChange, disabled = false }: {
  value?: string; onChange: (url: string) => void; onUploadingChange: (busy: boolean) => void; disabled?: boolean;
}) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const abort = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState("");
  const [progress, setProgress] = useState(0);
  const source = invitationMusicSource(value);
  const [youtube, setYoutube] = useState(() => source.kind === "youtube" || source.kind === "invalid");
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; abort.current?.abort(); };
  }, []);
  const upload = async (file?: File) => {
    if (!file || busy || disabled) return;
    setError(""); setStatus("");
    const validation = invitationAudioFileError(file);
    if (validation) { setError(validation); if (input.current) input.current.value = ""; return; }
    setBusy(true); setProgress(0); onUploadingChange(true);
    const controller = new AbortController(); abort.current = controller;
    try {
      const form = new FormData(); form.append("file", file);
      const response = await axiosClient.post<{ url: string }>("/api/upload/invitation-audio", form, {
        signal: controller.signal,
        onUploadProgress: e => { if (mounted.current && e.total) setProgress(Math.round(e.loaded * 100 / e.total)); },
      });
      if (mounted.current && !controller.signal.aborted) {
        if (invitationMusicSource(response.data.url).kind !== "upload") throw new Error("Máy chủ trả về đường dẫn nhạc không hợp lệ.");
        onChange(response.data.url); setYoutube(false);
        setStatus("Nhạc đã tải lên. Nghe thử bên dưới rồi bấm Lưu nháp hoặc Lưu & xuất bản để lưu vào thiệp.");
      }
    } catch (e) { if (mounted.current && !controller.signal.aborted) setError(apiError(e)); }
    finally {
      if (mounted.current) { setBusy(false); onUploadingChange(false); if (input.current) input.current.value = ""; }
    }
  };
  return <section aria-labelledby={`${id}-title`} className="rounded-2xl border border-[var(--line)] p-4 space-y-3">
    <h3 id={`${id}-title`} className="label !mb-0">Nhạc nền thiệp cưới</h3>
    <p className="text-xs muted">Tải bản nhạc bạn có quyền sử dụng hoặc chọn video YouTube. Thiệp không tự phát nhạc.</p>
    <div className="flex flex-wrap gap-2">
      <button type="button" className={`btn ${!youtube ? "btn-primary" : "btn-ghost"}`} disabled={busy || disabled} aria-pressed={!youtube} onClick={() => setYoutube(false)}>Tệp MP3</button>
      <button type="button" className={`btn ${youtube ? "btn-primary" : "btn-ghost"}`} disabled={busy || disabled} aria-pressed={youtube} onClick={() => setYoutube(true)}>YouTube</button>
    </div>
    {youtube ? <label className="block">
      <span className="label">Liên kết video YouTube</span>
      <input type="url" className="input" maxLength={500} placeholder="https://www.youtube.com/watch?v=..." value={source.kind === "youtube" || source.kind === "invalid" ? value : ""} disabled={busy || disabled} onChange={e => { onChange(e.target.value); setStatus(""); setError(""); }} />
      <small className="block muted mt-2">Hỗ trợ watch, youtu.be, Shorts hoặc Live của một video. Không tải hoặc tách âm thanh từ YouTube.</small>
    </label> : <>
      <input ref={input} type="file" accept=".mp3,audio/mpeg,audio/mp3" className="sr-only" aria-label="Chọn tệp nhạc MP3" disabled={busy || disabled} onChange={e => void upload(e.target.files?.[0])} />
      <button type="button" className="btn btn-outline w-full" disabled={busy || disabled} onClick={() => input.current?.click()}>{busy ? "Đang tải nhạc..." : source.kind === "upload" ? "Thay bằng tệp MP3 khác" : "Tải nhạc MP3 từ thiết bị"}</button>
      <p className="text-xs muted">MP3 · tối đa 12 MB/tệp. Giới hạn lưu trữ kỹ thuật: 20 tệp / 100 MB mỗi hồ sơ.</p>
    </>}
    {busy && <progress className="w-full" max={100} value={progress} aria-label="Tiến độ tải nhạc" />}
    {source.kind === "invalid" && <p role="alert" className="text-xs text-red-700">{source.message}</p>}
    {source.kind !== "none" && source.kind !== "invalid" && <InvitationMusicPlayer value={value} />}
    {source.kind === "legacy" && <p className="text-xs muted">Đang giữ liên kết nhạc cũ của thiệp. Chọn MP3 hoặc YouTube để thay thế.</p>}
    {value && <button type="button" className="btn btn-ghost text-red-700" disabled={busy || disabled} onClick={() => { onChange(""); setError(""); setStatus("Đã bỏ nhạc khỏi bản thiết kế. Bấm Lưu để áp dụng; tệp cũ không bị xóa."); }}>Bỏ nhạc khỏi thiệp</button>}
    {status && <p role="status" className="text-xs muted">{status}</p>}
    {error && <p role="alert" className="text-xs text-red-700">{error} Nhạc hiện tại vẫn được giữ.</p>}
  </section>;
}
