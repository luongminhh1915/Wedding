import { useEffect, useId, useRef, useState } from "react";
import {
  ImagePlus,
  Loader2,
  Trash2,
  ArrowLeft,
  ArrowRight,
} from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import { apiError } from "../api";
import { invitationImageSrc, prepareInvitationImage } from "./invitationImages";

interface Props {
  label: string;
  images: string[];
  max: number;
  onChange: (urls: string[]) => void;
  onUploadingChange: (uploading: boolean) => void;
  disabled?: boolean;
}
export function InvitationImagePicker({
  label,
  images,
  max,
  onChange,
  onUploadingChange,
  disabled = false,
}: Props) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const abort = useRef<AbortController | null>(null);
  const previews = useRef<string[]>([]);
  const mounted = useRef(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [progress, setProgress] = useState(0);
  const [status, setStatus] = useState("");
  const [pending, setPending] = useState<{ name: string; src: string }[]>([]);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      abort.current?.abort();
      previews.current.forEach(URL.revokeObjectURL);
    };
  }, []);
  const upload = async (files: File[]) => {
    if (!files.length || busy) return;
    const available = max === 1 ? 1 : max - images.length;
    if (files.length > available) {
      setError(`Chỉ có thể chọn thêm ${available} ảnh (tối đa ${max} ảnh).`);
      if (input.current) input.current.value = "";
      return;
    }
    setError("");
    setBusy(true);
    onUploadingChange(true);
    setProgress(0);
    abort.current = new AbortController();
    const failures: string[] = [];
    let completed = max === 1 ? [] : [...images];
    previews.current = files.map((file) => URL.createObjectURL(file));
    setPending(
      files.map((file, n) => ({ name: file.name, src: previews.current[n] })),
    );
    try {
      for (let n = 0; n < files.length; n++) {
        if (abort.current.signal.aborted) break;
        try {
          setStatus(`Đang tối ưu ảnh ${n + 1}/${files.length}...`);
          const prepared = await prepareInvitationImage(files[n]);
          const body = new FormData();
          body.append("file", prepared);
          setStatus(`Đang tải ảnh ${n + 1}/${files.length}...`);
          const result = await axiosClient.post<{ url: string }>(
            "/api/upload/invitation-images",
            body,
            {
              headers: { "Content-Type": "multipart/form-data" },
              signal: abort.current.signal,
              timeout: 60000,
              onUploadProgress: (e) => {
                if (mounted.current)
                  setProgress(
                    Math.round(
                      ((n + (e.total ? e.loaded / e.total : 0)) /
                        files.length) *
                        100,
                    ),
                  );
              },
            },
          );
          if (!result.data.url)
            throw new Error("Máy chủ chưa trả về ảnh đã lưu.");
          completed = [...completed, result.data.url];
          if (mounted.current) onChange(completed);
        } catch (e) {
          if (!abort.current.signal.aborted)
            failures.push(
              `${files[n].name}: ${e instanceof Error && !("response" in e) ? e.message : apiError(e)}`,
            );
        }
        if (mounted.current)
          setProgress(Math.round(((n + 1) / files.length) * 100));
      }
      if (mounted.current) {
        setError(failures.join("\n"));
        setStatus(
          failures.length
            ? "Một số ảnh chưa tải xong. Các ảnh tải thành công vẫn được giữ lại."
            : "Ảnh đã tải lên. Bấm Lưu nháp hoặc Lưu & xuất bản để lưu vào thiệp.",
        );
      }
    } finally {
      previews.current.forEach(URL.revokeObjectURL);
      previews.current = [];
      if (mounted.current) {
        setPending([]);
        setBusy(false);
        onUploadingChange(false);
        if (input.current) input.current.value = "";
      }
    }
  };
  const move = (index: number, direction: number) => {
    const next = [...images];
    [next[index], next[index + direction]] = [
      next[index + direction],
      next[index],
    ];
    onChange(next);
  };
  return (
    <section aria-labelledby={`${id}-label`} className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <h3 id={`${id}-label`} className="label !mb-0">
          {label}
        </h3>
        {max > 1 && (
          <span className="text-xs muted">
            {images.length}/{max} ảnh
          </span>
        )}
      </div>
      {!!images.length && (
        <div
          className={`grid gap-3 ${max === 1 ? "grid-cols-1" : "grid-cols-2 sm:grid-cols-3"}`}
        >
          {images.map((url, n) => (
            <figure
              key={`${url}-${n}`}
              className="rounded-xl border border-[var(--line)] overflow-hidden bg-white"
            >
              <img
                src={invitationImageSrc(url)}
                alt={`${label} ${n + 1}`}
                className={`w-full object-cover ${max === 1 ? "aspect-[4/3] max-h-64" : "aspect-square"}`}
              />
              <figcaption className="flex items-center justify-between px-2 py-2 gap-1">
                <span className="text-[11px] muted">
                  {max === 1 ? "Ảnh bìa hiện tại" : `Ảnh ${n + 1}`}
                </span>
                <div className="flex gap-1">
                  {max > 1 && (
                    <>
                      <button
                        type="button"
                        className="p-1 rounded hover:bg-slate-100 disabled:opacity-30"
                        disabled={busy || disabled || n === 0}
                        onClick={() => move(n, -1)}
                        aria-label={`Đưa ảnh ${n + 1} lên trước`}
                      >
                        <ArrowLeft size={13} />
                      </button>
                      <button
                        type="button"
                        className="p-1 rounded hover:bg-slate-100 disabled:opacity-30"
                        disabled={busy || disabled || n === images.length - 1}
                        onClick={() => move(n, 1)}
                        aria-label={`Đưa ảnh ${n + 1} ra sau`}
                      >
                        <ArrowRight size={13} />
                      </button>
                    </>
                  )}
                  <button
                    type="button"
                    className="p-1 rounded text-red-700 hover:bg-red-50 disabled:opacity-30"
                    disabled={busy || disabled}
                    onClick={() => {
                      onChange(images.filter((_, i) => i !== n));
                      setError("");
                      setStatus("Đã bỏ ảnh khỏi thiệp. Bấm Lưu để áp dụng.");
                    }}
                    aria-label={`Bỏ ${label.toLowerCase()} ${n + 1}`}
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
              </figcaption>
            </figure>
          ))}
        </div>
      )}
      <input
        ref={input}
        id={id}
        type="file"
        accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
        multiple={max > 1}
        disabled={busy || disabled || (max > 1 && images.length >= max)}
        onChange={(e) => void upload(Array.from(e.target.files ?? []))}
        className="sr-only"
      />
      <button
        type="button"
        className="btn btn-outline w-full"
        disabled={busy || disabled || (max > 1 && images.length >= max)}
        onClick={() => input.current?.click()}
      >
        {busy ? (
          <Loader2 className="animate-spin" size={16} />
        ) : (
          <ImagePlus size={16} />
        )}
        {busy
          ? "Đang tải ảnh..."
          : max === 1 && images.length
            ? "Chọn ảnh bìa khác từ máy"
            : max === 1
              ? "Tải ảnh bìa từ máy"
              : "Thêm ảnh từ máy"}
      </button>
      <p className="text-[11px] muted">
        JPG, PNG, WebP · ảnh gốc tối đa 20 MB. Ảnh được tối ưu tối đa 2400 px,
        bỏ thông tin vị trí; không cần dán đường dẫn.
      </p>
      {busy && (
        <div className="space-y-2" role="status">
          <div className="flex gap-2 overflow-x-auto">
            {pending.map((p, n) => (
              <img
                key={n}
                src={p.src}
                alt={`Đang tải ${p.name}`}
                className="w-14 h-14 rounded-lg object-cover shrink-0 opacity-75"
              />
            ))}
          </div>
          <progress
            className="w-full accent-[#25334c]"
            max={100}
            value={progress}
            aria-label="Tiến độ tải ảnh"
          />
          <p className="text-xs muted">
            {status} {progress}%
          </p>
        </div>
      )}
      {!busy && status && (
        <p role="status" className="text-xs muted">
          {status}
        </p>
      )}
      {error && (
        <div
          role="alert"
          className="rounded-xl p-3 bg-red-50 text-red-800 text-xs whitespace-pre-wrap"
        >
          <p>{error}</p>
          <button
            type="button"
            className="underline mt-2"
            onClick={() => setError("")}
          >
            Đóng thông báo
          </button>
        </div>
      )}
    </section>
  );
}
