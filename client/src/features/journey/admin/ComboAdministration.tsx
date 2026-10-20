import { useState } from "react";
import { Link } from "react-router-dom";
import { apiError } from "../api";
import { useCatalog, useComboMutation, useComboTemplates, useRules, type CatalogItem, type CatalogTemplate } from "../comboApi";
import { useAuthStore } from "../../../store/authStore";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd } from "../format";
import { ComboLifecycleCard } from "../combo/ComboLifecycleCard";
import { ComboTemplateEditor } from "../combo/ComboTemplateEditor";
import { serviceItemReviewPayload } from "./serviceItemReview";

function ReviewItem({ item, onRefresh }: { item: CatalogItem; onRefresh: () => void }) {
  const review = useComboMutation(`/api/combos/items/${item.id}/review`);
  const [note, setNote] = useState("");
  const [validationError, setValidationError] = useState("");
  const decide = (approve: boolean) => {
    setValidationError("");
    try { review.mutate(serviceItemReviewPayload(item, approve, note)); }
    catch (error) { setValidationError(error instanceof Error ? error.message : "Chưa gửi được kết quả kiểm duyệt."); }
  };
  return <article className="min-w-0 rounded-xl border border-[var(--line)] p-4 [overflow-wrap:anywhere]">
    <div className="flex flex-wrap items-start justify-between gap-2"><h3 className="font-semibold">{item.name}</h3><StatusPill status={item.moderationStatus} label={item.moderationStatus === "Pending" ? "Chờ kiểm duyệt" : undefined} /></div>
    <p className="mt-2 text-sm">{item.vendorName} · {vnd(item.basePrice)}/{item.unit} · {item.regions} · {item.minGuests}–{item.maxGuests} khách</p>
    <p className="mt-2 whitespace-pre-wrap text-xs muted">{item.cancellationPolicy}</p>
    <Link className="mt-2 inline-block text-xs underline" to={`/listings/${item.listingId}?preview=vendor`} target="_blank" rel="noopener noreferrer">Xem bài giới thiệu dịch vụ</Link>
    {(item.variants.length > 0 || item.addOns.length > 0) && <details className="mt-3 text-xs"><summary className="cursor-pointer font-semibold">Biến thể & món kèm ({item.variants.length + item.addOns.length})</summary><ul className="mt-2 space-y-2">{item.variants.map(variant => <li key={variant.id ?? variant.name}>Biến thể: {variant.name} · {vnd(variant.priceDelta)}</li>)}{item.addOns.map(addOn => <li key={addOn.id ?? addOn.name}>Món kèm: {addOn.name} · {vnd(addOn.price)}{addOn.perUnit ? "/đơn vị" : "/gói"}</li>)}</ul></details>}
    {item.reviewNote && <p className="mt-3 whitespace-pre-wrap rounded-lg bg-[#faf8f0] p-3 text-xs"><b>Phản hồi kiểm duyệt:</b> {item.reviewNote}</p>}
    {item.moderationStatus === "Pending" && <fieldset disabled={review.isPending} className="mt-4 min-w-0 space-y-3"><legend className="sr-only">Kết quả kiểm duyệt {item.name}</legend>
      <p className="text-xs muted">Kết quả chỉ áp dụng cho đúng nội dung bạn đang xem. Nếu NCC đã chỉnh sửa và gửi lại, hãy tải lại trước khi quyết định.</p>
      <label className="block"><span className="label">Ghi chú / lý do từ chối dịch vụ (bắt buộc khi từ chối)</span><textarea className="input" rows={3} maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
      <div className="flex flex-wrap gap-2"><button type="button" className="btn btn-primary" disabled={!item.reviewVersion} onClick={() => decide(true)}>{review.isPending ? "Đang gửi..." : "Duyệt dịch vụ"}</button><button type="button" className="btn btn-outline" disabled={!item.reviewVersion || !note.trim()} onClick={() => decide(false)}>Từ chối dịch vụ</button></div>
      {!item.reviewVersion && <p role="alert" className="text-xs text-amber-800">Chưa có phiên bản kiểm duyệt hợp lệ. Tải lại dịch vụ để tiếp tục.</p>}
    </fieldset>}
    {(validationError || review.isError) && <div role="alert" className="mt-3"><ErrorText>{validationError || apiError(review.error)}</ErrorText><button type="button" className="btn btn-outline mt-2" disabled={review.isPending} onClick={onRefresh}>Tải lại dịch vụ</button></div>}
  </article>;
}
const filters = [["all", "Tất cả"], ["Draft", "Nháp"], ["Pending", "Chờ duyệt"], ["Approved", "Đã duyệt"], ["Rejected", "Bị từ chối"]];
export function ComboAdministration() {
  const catalog = useCatalog(true);
  const query = useComboTemplates();
  const items = catalog.data ?? [];
  const templates = query.data ?? [];
  const { data: rules = [] } = useRules();
  const saveRules = useComboMutation("/api/combos/bundle-rules");
  const user = useAuthStore(state => state.user);
  const [editing, setEditing] = useState<CatalogTemplate | null | undefined>(undefined);
  const [filter, setFilter] = useState("all");
  const [ruleText, setRuleText] = useState<string | null>(null);
  const visible = templates.filter(template => filter === "all" || (template.latestRevision?.status ?? template.moderationStatus) === filter);
  return <div className="wd min-w-0">
    <Panel title="Duyệt dịch vụ combo">
      {catalog.isPending ? <p role="status" className="text-sm muted">Đang tải dịch vụ do NCC gửi...</p> : catalog.isError ? <div role="alert"><ErrorText>{apiError(catalog.error)}</ErrorText><button type="button" className="btn btn-outline" onClick={() => void catalog.refetch()}>Thử lại</button></div> : <div className="grid min-w-0 gap-3">{items.length ? items.map(item => <ReviewItem key={`${item.id}:${item.reviewVersion ?? "legacy"}`} item={item} onRefresh={() => void catalog.refetch()} />) : <Empty>Chưa có dịch vụ do NCC gửi.</Empty>}</div>}
    </Panel>
    <Panel title="Combo riêng của từng nhà cung cấp" action={<button type="button" className="btn btn-outline" disabled={catalog.isPending || catalog.isError} onClick={() => setEditing(null)}>Tạo combo cho một NCC</button>}>
      <p className="mb-4 text-sm leading-relaxed muted">Một combo chỉ thuộc một NCC. Đề xuất mới không tự thay thế bản đang công bố. Cả quản trị viên cũng cần gửi và duyệt đúng phiên bản trước khi công bố.</p>
      <label className="mb-4 block max-w-sm"><span className="label">Trạng thái đề xuất mới nhất</span><select className="input" value={filter} onChange={event => setFilter(event.target.value)}>{filters.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
      {query.isPending ? <p role="status" className="text-sm muted">Đang tải combo và lịch sử kiểm duyệt...</p> : query.isError ? <div role="alert"><ErrorText>{apiError(query.error)}</ErrorText><button type="button" className="btn btn-outline" onClick={() => void query.refetch()}>Thử lại</button></div> : <><p className="mb-3 text-xs muted">{visible.length} / {templates.length} combo</p><div className="grid min-w-0 gap-4">{visible.map(template => <ComboLifecycleCard key={template.id} template={template} staff onEdit={() => setEditing(template)} />)}{!visible.length && <Empty>{templates.length ? "Chưa có combo ở trạng thái này." : "Chưa có combo do NCC tạo."}</Empty>}</div></>}
      {editing !== undefined && <ComboTemplateEditor key={editing?.id ?? "new-staff-combo"} template={editing ?? undefined} items={items} staff onClose={() => setEditing(undefined)} />}
    </Panel>
    {user?.role === "SuperAdmin" && <Panel title="Quy tắc giảm combo"><p className="text-sm muted">Một dòng: số hạng mục tối thiểu = phần trăm giảm. Áp dụng bên trong combo một NCC và không vượt trần NCC đó đồng ý.</p><textarea aria-label="Các mức giảm combo" className="input mt-3" rows={4} value={ruleText ?? rules.map(r => `${r.minItems}=${r.percent * 100}`).join("\n")} onChange={e => setRuleText(e.target.value)} /><ErrorText>{saveRules.isError ? apiError(saveRules.error) : ""}</ErrorText><button className="btn btn-primary mt-3" disabled={saveRules.isPending} onClick={() => { const text = ruleText ?? rules.map(r => `${r.minItems}=${r.percent * 100}`).join("\n"); const parsed = text.split("\n").filter(Boolean).map(line => { const [min, percent] = line.split("=").map(Number); return { minItems: min, percent: percent / 100 }; }); if (parsed.some(r => !Number.isFinite(r.minItems) || !Number.isFinite(r.percent))) { alert("Nhập mỗi dòng theo dạng 3=3 (3 hạng mục giảm 3%)."); return; } saveRules.mutate({ rules: parsed }); }}>Lưu quy tắc</button></Panel>}
  </div>;
}
