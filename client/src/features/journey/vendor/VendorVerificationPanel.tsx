import { useState, useRef, useEffect } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BadgeCheck, ShieldQuestion, UploadCloud, FileText, X, ExternalLink, Loader2, AlertCircle } from "lucide-react";
import { apiError, type Verification } from "../api";
import { ErrorText, Panel, StatusPill } from "../ui";
import { vnDate } from "../format";
import { VendorTaxProfileForm } from "../admin/OperationsPanels";
import { useAuthStore } from "../../../store/authStore";
import axiosClient from "../../../services/axiosClient";
import { VerificationDocumentLink } from "../VerificationDocumentLink";
import { uploadVerificationFiles, verificationActorIsCurrent, type VerificationActor } from "../verificationDocumentModel";

interface UploadedDoc {
  url: string;
  originalName: string;
  size?: number;
  extension?: string;
}

/** P6 – NCC nộp hồ sơ xác minh (GPKD + giấy tờ PDF/Word/ảnh) để nhận huy hiệu "Đã xác minh". */
export function VendorVerificationPanel() {
  const { user, token, sessionRevision } = useAuthStore();
  if (!user || !token || user.role !== "VendorOwner") return <p>Vui lòng đăng nhập tài khoản nhà cung cấp.</p>;
  return <VendorVerificationEditor key={`${user.id}:${sessionRevision}`} actor={{ id: user.id, role: user.role, vendorId: user.vendorId, token, revision: sessionRevision }} />;
}

function VendorVerificationEditor({ actor }: { actor: VerificationActor }) {
  const qc = useQueryClient();
  const current = () => verificationActorIsCurrent(actor, useAuthStore.getState());
  const queryKey = ["my-verifications", actor.id, actor.revision];
  const { data = [], refetch } = useQuery({ queryKey, queryFn: async ({ signal }) => {
    if (!current()) throw new Error("Phiên đăng nhập đã thay đổi.");
    const result = await axiosClient.get<Verification[]>("/api/verifications/my", { signal, headers: { Authorization: `Bearer ${actor.token}` } });
    if (!current()) throw new Error("Phiên đăng nhập đã thay đổi.");
    return result.data;
  } });
  const requests = useRef(new Set<AbortController>());
  const uploadController = useRef<AbortController | null>(null);
  useEffect(() => () => { for (const request of requests.current) request.abort(); requests.current.clear(); }, []);
  const submit = useMutation({ mutationFn: async (body: { businessLicenseNumber: string; documentUrls: string[] }) => {
    if (!current() || uploadController.current) throw new Error("Vui lòng chờ tải xong tài liệu hoặc mở lại hồ sơ.");
    const controller = new AbortController(); requests.current.add(controller);
    try {
      const result = await axiosClient.post<Verification>("/api/verifications", body, { signal: controller.signal, headers: { Authorization: `Bearer ${actor.token}` } });
      if (!current() || controller.signal.aborted) throw new Error("Phiên đăng nhập đã thay đổi.");
      return result.data;
    } finally { requests.current.delete(controller); }
  }, onSuccess: () => { if (current()) void qc.invalidateQueries({ queryKey }); } });
  const [license, setLicense] = useState("");
  const [uploadedDocs, setUploadedDocs] = useState<UploadedDoc[]>([]);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const latest = [...data].sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];

  const handleFileUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(event.target.files ?? []);
    if (!files.length || uploadController.current || submit.isPending || !current()) return;
    if (!actor.vendorId) { setUploadError("Chưa xác định được shop. Vui lòng đăng nhập lại trước khi tải tài liệu."); return; }
    const controller = new AbortController(); uploadController.current = controller; requests.current.add(controller);
    setUploadError("");
    setUploading(true);

    try {
      const newDocs = await uploadVerificationFiles(files, actor.vendorId, { existingCount: uploadedDocs.length, signal: controller.signal, isCurrent: current,
        send: async (selected, signal) => {
          const formData = new FormData(); for (const file of selected) formData.append("files", file);
          return (await axiosClient.post<{ items: UploadedDoc[] }>("/api/upload/documents", formData, { signal,
            headers: { "Content-Type": "multipart/form-data", Authorization: `Bearer ${actor.token}` } })).data.items;
        } });
      setUploadedDocs((prev) => [...prev, ...newDocs]);
    } catch (err) {
      if (!controller.signal.aborted && current()) setUploadError(apiError(err, err instanceof Error ? err.message : undefined));
    } finally {
      requests.current.delete(controller); if (uploadController.current === controller) uploadController.current = null;
      if (current()) { setUploading(false); if (fileInputRef.current) fileInputRef.current.value = ""; }
    }
  };

  const removeDoc = (index: number) => {
    setUploadedDocs((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = () => {
    if (uploadController.current || uploading || submit.isPending || !current()) return;
    const allUrls = uploadedDocs.map((d) => d.url);
    submit.mutate(
      { businessLicenseNumber: license, documentUrls: allUrls },
      {
        onSuccess: () => {
          if (!current()) return;
          setUploadedDocs([]);
          setLicense("");
          void refetch();
        },
      }
    );
  };

  return (
    <div className="wd space-y-6">
      <VendorTaxProfileForm />
      <Panel eyebrow="Xác minh đối tác" title="Huy hiệu “Đã xác minh”">
        <p className="text-sm muted mb-4">
          Gửi giấy tờ pháp lý (hỗ trợ tệp <strong>PDF</strong>, <strong>Word (.doc, .docx)</strong> hoặc ảnh scan) để ban quản trị kiểm tra hồ sơ và cấp huy hiệu. Bạn vẫn có thể tạo dịch vụ bình thường trong khi chờ xét duyệt hồ sơ.
        </p>

        {latest && (
          <div className="rounded-2xl border border-[var(--line)] p-5 mb-6 bg-[var(--paper)]">
            <div className="flex items-start gap-3">
              {latest.status === "Approved" ? (
                <BadgeCheck className="text-[var(--gold)] shrink-0 mt-0.5" size={24} />
              ) : (
                <ShieldQuestion className="text-[var(--brand-2)] shrink-0 mt-0.5" size={24} />
              )}
              <div className="text-sm flex-1 space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-semibold text-base">Hồ sơ GPKD: {latest.businessLicenseNumber}</span>
                  <StatusPill status={latest.status} />
                  <span className="muted text-xs">· Nộp ngày {vnDate(latest.createdAt)}</span>
                </div>
                {latest.reviewNote && (
                  <p className="text-xs rounded-xl bg-amber-50 border border-amber-200 p-2.5 text-amber-900">
                    <strong>Ghi chú từ quản trị viên:</strong> {latest.reviewNote}
                  </p>
                )}
                {latest.documentUrls?.length > 0 && (
                  <div className="pt-2">
                    <p className="text-xs font-semibold text-slate-600 mb-1.5">Tài liệu đã nộp ({latest.documentUrls.length}):</p>
                    <div className="flex flex-wrap gap-2">
                      {latest.documentUrls.map((url, i) => (
                        <VerificationDocumentLink
                          key={url}
                          reference={url}
                          vendorId={latest.vendorId}
                          className="inline-flex items-center gap-1.5 text-xs bg-white border border-slate-200 px-3 py-1.5 rounded-lg text-slate-700 hover:text-emerald-700 hover:border-emerald-300 transition-colors shadow-2xs"
                        >
                          <FileText size={13} className="text-slate-400" />
                          <span>Tệp {i + 1}</span>
                          <ExternalLink size={11} className="text-slate-400" />
                        </VerificationDocumentLink>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {(!latest || latest.status === "Rejected") && (
          <div className="space-y-4 rounded-2xl border border-[var(--line)] bg-white p-5">
            <h4 className="font-semibold text-base text-[#233a42]">
              {latest?.status === "Rejected" ? "Nộp lại hồ sơ xác minh" : "Nộp hồ sơ xác minh mới"}
            </h4>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Số giấy phép kinh doanh / Mã số thuế (MST) <span className="text-red-500">*</span>
              </label>
              <input
                className="input w-full"
                placeholder="VD: 0101234567 hoặc GPKD-2024-..."
                value={license}
                onChange={(e) => setLicense(e.target.value)}
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Đính kèm giấy tờ chứng minh (Hỗ trợ file PDF, Word, Ảnh) <span className="text-red-500">*</span>
              </label>
              <p className="text-xs text-slate-500 mb-3">
                Chỉ đính kèm giấy tờ cần thiết để đối chiếu. Tệp tải lên được giữ riêng cho shop và bộ phận xét duyệt; tối đa 20 MB/tệp, 32 MB/lần tải. Hãy hoàn thiện hồ sơ pháp nhân và ngân hàng thật ở trên trước khi nộp.
              </p>

              {/* Upload trigger button */}
              <div className="flex flex-wrap items-center gap-3">
                <input
                  type="file"
                  ref={fileInputRef}
                  multiple
                  accept=".pdf,.doc,.docx,.png,.jpg,.jpeg,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,image/png,image/jpeg"
                  className="hidden"
                  onChange={handleFileUpload}
                />
                <button
                  type="button"
                  className="btn btn-outline inline-flex items-center gap-2"
                  disabled={uploading || submit.isPending}
                  onClick={() => fileInputRef.current?.click()}
                >
                  {uploading ? <Loader2 size={16} className="animate-spin" /> : <UploadCloud size={16} />}
                  <span>{uploading ? "Đang tải tệp lên..." : "Tải lên tệp PDF / Word / Ảnh"}</span>
                </button>
              </div>

              {uploadError && (
                <p className="mt-2 text-xs text-red-600 flex items-center gap-1.5">
                  <AlertCircle size={14} /> {uploadError}
                </p>
              )}

              {/* Uploaded files list */}
              {uploadedDocs.length > 0 && (
                <div className="mt-3 space-y-2">
                  <p className="text-xs font-semibold text-slate-600">Các tệp đã đính kèm ({uploadedDocs.length}):</p>
                  <div className="grid gap-2 sm:grid-cols-2">
                    {uploadedDocs.map((doc, index) => (
                      <div
                        key={doc.url + index}
                        className="flex items-center justify-between gap-2 p-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs"
                      >
                        <div className="flex items-center gap-2 min-w-0">
                          <FileText size={16} className="text-emerald-600 shrink-0" />
                          <span className="truncate font-medium text-slate-700" title={doc.originalName}>
                            {doc.originalName}
                          </span>
                        </div>
                        <button
                          type="button"
                          className="text-slate-400 hover:text-red-600 p-1 rounded-md"
                          onClick={() => removeDoc(index)}
                          title="Gỡ tệp này"
                        >
                          <X size={14} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

            </div>

            <div className="flex justify-between items-center pt-3 border-t border-slate-100">
              <ErrorText>{submit.isError ? apiError(submit.error) : ""}</ErrorText>
              <button
                className="btn btn-primary"
                disabled={submit.isPending || uploading || !license.trim() || uploadedDocs.length === 0}
                onClick={handleSubmit}
              >
                {submit.isPending ? "Đang gửi hồ sơ..." : "Nộp hồ sơ xác minh"}
              </button>
            </div>
          </div>
        )}
      </Panel>
    </div>
  );
}
