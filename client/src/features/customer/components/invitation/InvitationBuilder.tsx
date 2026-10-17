import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Save, Eye, Plus, Trash2 } from "lucide-react";
import {
  useMyInvitation,
  useInvitationRsvps,
} from "../../hooks/useInvitations";
import { usePlannerOverview, apiError } from "../../../journey/api";
import {
  useInvitationTemplates,
  parseInvitationContent,
  type InvitationContent,
  type InvitationTemplate,
} from "../../../journey/invitations/invitationApi";
import { InvitationView } from "../../../journey/invitations/InvitationView";
import type {
  SaveInvitationRequest,
  WeddingInvitation,
} from "../../../../types/invitation.types";
import type { WeddingProfile } from "../../../journey/api";
import { InvitationImagePicker } from "../../../journey/invitations/InvitationImagePicker";

const slugify = (s: string) =>
  s
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/gi, "d")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
const inputDate = (s?: string) =>
  s && Number.isFinite(new Date(s).getTime())
    ? new Date(s)
        .toLocaleString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" })
        .replace(" ", "T")
        .slice(0, 16)
    : "";
export function InvitationBuilder() {
  const mine = useMyInvitation();
  const { data: planner } = usePlannerOverview();
  const templates = useInvitationTemplates();
  const [params] = useSearchParams();
  if (mine.isLoading || templates.isLoading)
    return <p className="p-6">Đang tải trình thiết kế...</p>;
  if (mine.isError || templates.isError)
    return (
      <p className="p-6 text-red-700" role="alert">
        {mine.error?.message || apiError(templates.error)}
      </p>
    );
  return (
    <BuilderForm
      key={`${mine.invitation?.id ?? "new"}-${params.get("template") ?? ""}`}
      invitation={mine.invitation ?? null}
      profile={planner?.profile}
      templates={templates.data ?? []}
      selected={params.get("template")}
      save={mine.saveInvitation}
      saving={mine.isSaving}
    />
  );
}
function BuilderForm({
  invitation: i,
  profile: p,
  templates,
  selected,
  save,
  saving,
}: {
  invitation: WeddingInvitation | null;
  profile?: WeddingProfile | null;
  templates: InvitationTemplate[];
  selected: string | null;
  save: (r: SaveInvitationRequest) => Promise<WeddingInvitation>;
  saving: boolean;
}) {
  const [f, setF] = useState<SaveInvitationRequest>(() => ({
    slug: i?.slug || slugify(`${p?.groomName ?? ""}-${p?.brideName ?? ""}`),
    groomName: i?.groomName || p?.groomName || "",
    brideName: i?.brideName || p?.brideName || "",
    eventDate:
      inputDate(i?.eventDate) ||
      (p?.weddingDate ? `${p.weddingDate.slice(0, 10)}T18:00` : ""),
    venueName: i?.venueName || "",
    venueAddress: i?.venueAddress || p?.city || "",
    mapUrl: i?.mapUrl || "",
    coverImageUrl: i?.coverImageUrl || "",
    loveStory: i?.loveStory || "",
    musicUrl: i?.musicUrl || "",
    bankInfo: i?.bankInfo || "",
    templateStyle:
      templates.find((t) => t.key === selected)?.key ||
      i?.templateStyle ||
      "navy-cream",
    lang: i?.lang || "vi",
    isPublished: i?.isPublished ?? false,
  }));
  const [content, setContent] = useState<InvitationContent>(() =>
    parseInvitationContent(i?.contentJson),
  );
  const [advanced, setAdvanced] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [coverUploading, setCoverUploading] = useState(false);
  const [albumUploading, setAlbumUploading] = useState(false);
  const uploading = coverUploading || albumUploading;
  const { data: rsvps = [], isError: rsvpError } = useInvitationRsvps();
  const set = <K extends keyof SaveInvitationRequest>(
    key: K,
    value: SaveInvitationRequest[K],
  ) => setF((x) => ({ ...x, [key]: value }));
  const template = templates.find((t) => t.key === f.templateStyle);
  const serialize = () => JSON.stringify(content);
  const submit = async (publish: boolean) => {
    if (uploading) {
      setError("Vui lòng đợi tải ảnh xong trước khi lưu thiệp.");
      return;
    }
    setError("");
    setMessage("");
    if (
      !f.groomName.trim() ||
      !f.brideName.trim() ||
      !f.venueName.trim() ||
      !f.eventDate
    ) {
      setError("Vui lòng nhập tên hai bạn, thời gian và địa điểm tổ chức.");
      return;
    }
    try {
      const result = await save({
        ...f,
        eventDate: `${f.eventDate}:00+07:00`,
        contentJson: serialize(),
        templateId: template?.id,
        isPublished: publish,
      });
      set("slug", result.slug);
      set("isPublished", result.isPublished);
      setMessage(
        publish
          ? `Đã xuất bản thiệp: /${result.slug}`
          : "Đã lưu bản nháp. Thiệp chưa được chia sẻ công khai.",
      );
    } catch (e) {
      setError((e as Error).message || apiError(e));
    }
  };
  return (
    <div className="wd space-y-5">
      <div className="card p-5 flex flex-wrap justify-between gap-4 items-center">
        <div>
          <h2 className="serif text-2xl font-bold">
            Thiết kế thiệp của hai bạn
          </h2>
          <p className="muted text-sm mt-1">
            Bản xem trước cập nhật ngay. Lưu nháp để tiếp tục sau, xuất bản khi
            đã sẵn sàng.
          </p>
        </div>
        <div className="flex gap-2">
          <button
            className="btn btn-ghost"
            disabled={saving || uploading}
            onClick={() => void submit(false)}
          >
            <Save size={14} />
            Lưu nháp
          </button>
          <button
            className="btn btn-primary"
            disabled={saving || uploading}
            onClick={() => void submit(true)}
          >
            <Eye size={14} />
            {uploading
              ? "Đang tải ảnh..."
              : saving
                ? "Đang lưu..."
                : "Lưu & xuất bản"}
          </button>
        </div>
      </div>
      {message && (
        <p className="p-4 rounded-xl bg-green-50 text-green-800" role="status">
          {message}
        </p>
      )}
      {error && (
        <p className="p-4 rounded-xl bg-red-50 text-red-700" role="alert">
          {error}
        </p>
      )}
      <div className="grid lg:grid-cols-2 gap-6 items-start">
        <div className="card p-5 space-y-5">
          <div>
            <span className="label">Mẫu thiệp</span>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
              {templates.map((t) => (
                <button
                  key={t.id}
                  className={`rounded-xl p-3 border text-center ${f.templateStyle === t.key ? "ring-2 ring-[#25334c]" : "border-[#dedccf]"}`}
                  style={{
                    background: t.backgroundColor,
                    color: t.primaryColor,
                  }}
                  onClick={() => set("templateStyle", t.key)}
                  aria-pressed={f.templateStyle === t.key}
                >
                  <span className="text-2xl block">{t.motif}</span>
                  <span className="block text-[10px] mt-1">{t.name}</span>
                </button>
              ))}
            </div>
          </div>
          <div className="grid sm:grid-cols-2 gap-3">
            <label>
              <span className="label">Tên chú rể *</span>
              <input
                className="input"
                maxLength={100}
                value={f.groomName}
                onChange={(e) => set("groomName", e.target.value)}
              />
            </label>
            <label>
              <span className="label">Tên cô dâu *</span>
              <input
                className="input"
                maxLength={100}
                value={f.brideName}
                onChange={(e) => set("brideName", e.target.value)}
              />
            </label>
          </div>
          <label className="block">
            <span className="label">Đường dẫn thiệp *</span>
            <div className="flex items-center gap-1">
              <span className="text-xs muted shrink-0">
                {window.location.host}/
              </span>
              <input
                className="input min-w-0"
                value={f.slug}
                maxLength={120}
                onChange={(e) => set("slug", slugify(e.target.value))}
              />
            </div>
          </label>
          <div className="grid sm:grid-cols-2 gap-3">
            <label>
              <span className="label">Ngày giờ (Việt Nam) *</span>
              <input
                className="input"
                type="datetime-local"
                value={f.eventDate}
                onChange={(e) => set("eventDate", e.target.value)}
              />
            </label>
            <label>
              <span className="label">Ngôn ngữ thiệp</span>
              <select
                className="input"
                value={f.lang}
                onChange={(e) => set("lang", e.target.value)}
              >
                <option value="vi">Tiếng Việt</option>
                <option value="en">English</option>
              </select>
            </label>
          </div>
          <label className="block">
            <span className="label">Địa điểm *</span>
            <input
              maxLength={200}
              className="input"
              value={f.venueName}
              onChange={(e) => set("venueName", e.target.value)}
            />
          </label>
          <label className="block">
            <span className="label">Địa chỉ</span>
            <input
              maxLength={300}
              className="input"
              value={f.venueAddress}
              onChange={(e) => set("venueAddress", e.target.value)}
            />
          </label>
          <label className="block">
            <span className="label">Link bản đồ</span>
            <input
              type="url"
              maxLength={500}
              className="input"
              value={f.mapUrl}
              onChange={(e) => set("mapUrl", e.target.value)}
              placeholder="https://maps.google.com/..."
            />
          </label>
          <InvitationImagePicker
            label="Ảnh bìa thiệp"
            max={1}
            images={f.coverImageUrl ? [f.coverImageUrl] : []}
            onChange={(urls) => set("coverImageUrl", urls[0] || "")}
            onUploadingChange={setCoverUploading}
            disabled={saving}
          />
          <InvitationImagePicker
            label="Album ảnh cưới"
            max={12}
            images={content.album ?? []}
            onChange={(album) => setContent((c) => ({ ...c, album }))}
            onUploadingChange={setAlbumUploading}
            disabled={saving}
          />
          <label className="block">
            <span className="label">Câu chuyện của hai bạn</span>
            <textarea
              maxLength={3000}
              className="input"
              rows={4}
              value={f.loveStory}
              onChange={(e) => set("loveStory", e.target.value)}
            />
          </label>
          <button
            className="btn btn-ghost w-full justify-between"
            aria-expanded={advanced}
            onClick={() => setAdvanced(!advanced)}
          >
            Tùy chỉnh nâng cao <span>{advanced ? "−" : "+"}</span>
          </button>
          {advanced && (
            <div className="space-y-4 border-t pt-4 border-[var(--line)]">
              <label className="block">
                <span className="label">Lời mời mở đầu</span>
                <textarea
                  className="input"
                  maxLength={1000}
                  value={content.welcome || ""}
                  onChange={(e) =>
                    setContent((c) => ({ ...c, welcome: e.target.value }))
                  }
                />
              </label>
              <label className="block">
                <span className="label">Dress code</span>
                <input
                  className="input"
                  maxLength={500}
                  value={content.dressCode || ""}
                  onChange={(e) =>
                    setContent((c) => ({ ...c, dressCode: e.target.value }))
                  }
                />
              </label>
              <label className="block">
                <span className="label">Font</span>
                <select
                  className="input"
                  value={content.font || "Playfair"}
                  onChange={(e) =>
                    setContent((c) => ({ ...c, font: e.target.value }))
                  }
                >
                  <option value="Playfair">Playfair Display</option>
                  <option value="Inter">Inter</option>
                </select>
              </label>
              <label className="block">
                <span className="label">
                  Nhạc nền (URL audio, không tự phát)
                </span>
                <input
                  type="url"
                  maxLength={500}
                  className="input"
                  value={f.musicUrl}
                  onChange={(e) => set("musicUrl", e.target.value)}
                />
              </label>
              <label className="block">
                <span className="label">Thông tin mừng cưới</span>
                <textarea
                  maxLength={300}
                  className="input"
                  rows={3}
                  value={f.bankInfo}
                  onChange={(e) => set("bankInfo", e.target.value)}
                  placeholder="Ngân hàng · Số tài khoản · Chủ tài khoản"
                />
              </label>
              <div>
                <h3 className="font-bold text-sm mb-2">Các sự kiện thêm</h3>
                {content.events?.map((event, n) => (
                  <div
                    key={n}
                    className="border border-[var(--line)] rounded-xl p-3 space-y-2 mb-3"
                  >
                    {(
                      [
                        ["name", "Tên sự kiện"],
                        ["date", "Ngày giờ Việt Nam"],
                        ["venue", "Địa điểm"],
                        ["address", "Địa chỉ"],
                      ] as const
                    ).map(([key, name]) => (
                      <label key={key} className="block">
                        <span className="label">{name}</span>
                        <input
                          className="input"
                          type={key === "date" ? "datetime-local" : "text"}
                          maxLength={300}
                          value={event[key]}
                          onChange={(e) =>
                            setContent((c) => ({
                              ...c,
                              events: c.events?.map((x, i) =>
                                i === n ? { ...x, [key]: e.target.value } : x,
                              ),
                            }))
                          }
                        />
                      </label>
                    ))}
                    <button
                      className="btn btn-ghost"
                      onClick={() =>
                        setContent((c) => ({
                          ...c,
                          events: c.events?.filter((_, i) => i !== n),
                        }))
                      }
                    >
                      <Trash2 size={13} />
                      Bỏ sự kiện
                    </button>
                  </div>
                ))}
                <button
                  className="btn btn-ghost"
                  disabled={(content.events?.length ?? 0) >= 10}
                  onClick={() =>
                    setContent((c) => ({
                      ...c,
                      events: [
                        ...(c.events ?? []),
                        { name: "", date: "", venue: "", address: "" },
                      ],
                    }))
                  }
                >
                  <Plus size={13} />
                  Thêm sự kiện
                </button>
              </div>
              <div className="flex flex-wrap gap-3">
                {(
                  [
                    ["story", "Câu chuyện"],
                    ["gallery", "Album"],
                    ["wishes", "Lời chúc"],
                  ] as const
                ).map(([key, name]) => (
                  <label key={key} className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={content.sections?.[key] !== false}
                      onChange={(e) =>
                        setContent((c) => ({
                          ...c,
                          sections: { ...c.sections, [key]: e.target.checked },
                        }))
                      }
                    />
                    Hiện {name}
                  </label>
                ))}
              </div>
              <label className="block">
                <span className="label">Tiêu đề chia sẻ</span>
                <input
                  className="input"
                  maxLength={160}
                  value={content.ogTitle || ""}
                  onChange={(e) =>
                    setContent((c) => ({ ...c, ogTitle: e.target.value }))
                  }
                />
              </label>
              <label className="block">
                <span className="label">Mô tả chia sẻ</span>
                <textarea
                  className="input"
                  maxLength={300}
                  value={content.ogDescription || ""}
                  onChange={(e) =>
                    setContent((c) => ({ ...c, ogDescription: e.target.value }))
                  }
                />
              </label>
            </div>
          )}
        </div>
        <div className="lg:sticky lg:top-24">
          <p className="text-xs muted uppercase tracking-wider text-center mb-3">
            Xem trước · chưa lưu
          </p>
          <InvitationView
            compact
            template={template}
            invitation={{
              ...f,
              contentJson: serialize(),
              eventDate: f.eventDate ? `${f.eventDate}:00+07:00` : "",
            }}
          />
        </div>
      </div>
      <div className="card p-5">
        <h3 className="serif text-2xl mb-3">Phản hồi & lời chúc</h3>
        {rsvpError ? (
          <p role="alert" className="text-red-700">
            Không tải được phản hồi. Hãy thử lại.
          </p>
        ) : (
          <>
            <p className="text-sm muted mb-3">
              {rsvps.length} phản hồi ·{" "}
              {rsvps
                .filter((r) => r.status === "Attending")
                .reduce((s, r) => s + 1 + r.companionCount, 0)}{" "}
              người sẽ tham dự
            </p>
            <div className="overflow-x-auto">
              <table className="w-full text-sm text-left">
                <thead>
                  <tr>
                    <th className="p-2">Khách</th>
                    <th className="p-2">Phản hồi</th>
                    <th className="p-2">Lời chúc</th>
                  </tr>
                </thead>
                <tbody>
                  {rsvps.map((r) => (
                    <tr key={r.id} className="border-t border-[var(--line)]">
                      <td className="p-2 font-bold">
                        {r.guestName}
                        <small className="block font-normal muted">
                          {r.phoneNumber}
                        </small>
                      </td>
                      <td className="p-2">
                        {r.status === "Attending"
                          ? `Tham dự · ${1 + r.companionCount} người`
                          : r.status === "Tentative"
                            ? "Chưa chắc"
                            : "Không dự"}
                        <small className="block muted">
                          {r.dietaryPreference}
                        </small>
                      </td>
                      <td className="p-2">{r.wishes || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
export default InvitationBuilder;
