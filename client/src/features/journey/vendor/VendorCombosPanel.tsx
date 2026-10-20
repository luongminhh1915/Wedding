import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuthStore } from "../../../store/authStore";
import { Pagination } from "../../../components/Pagination";
import { useMyListings } from "../../vendor-portal/hooks/useListings";
import { apiError } from "../api";
import { useCatalog, useComboMutation, useParticipation, useVendorComboTemplates, type CatalogTemplate } from "../comboApi";
import { Empty, ErrorText, Panel } from "../ui";
import { ComboLifecycleCard } from "../combo/ComboLifecycleCard";
import { ComboTemplateEditor } from "../combo/ComboTemplateEditor";
import { isFixedComboService } from "../combo/comboTemplateEditorModel";
import { VENDOR_COMBO_STATUSES, vendorComboPage } from "./vendorComboListModel";

const labels: Record<string, string> = { Draft: "Nháp", Pending: "Chờ duyệt", Approved: "Đã duyệt", Rejected: "Bị từ chối", Inactive: "Tạm ngừng" };
export function VendorCombosPanel() {
  const query = useVendorComboTemplates();
  const catalog = useCatalog();
  const listings = useMyListings();
  const participation = useParticipation();
  const consent = useComboMutation("/api/combos/vendor/participation");
  const user = useAuthStore(state => state.user);
  const [editing, setEditing] = useState<CatalogTemplate | null | undefined>(undefined);
  const [keyword, setKeyword] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(6);
  const [discount, setDiscount] = useState<number | null>(null);
  const [allow, setAllow] = useState<boolean | null>(null);
  const items = catalog.data ?? [];
  const eligible = items.filter(item => isFixedComboService(item, user?.vendorId));
  const filtered = vendorComboPage(query.data ?? [], keyword, status, page, pageSize);
  const pending = (listings.data ?? []).some(listing => listing.status === "PendingApproval" || listing.revisionStatus === "Pending");
  const createReady = !catalog.isPending && !catalog.isError && eligible.length >= 2;
  const changeStatus = (value: string) => { setStatus(value); setPage(1); };
  return <>
    <Panel title="Combo của tôi" eyebrow="Kết hợp các dịch vụ đã có" action={<button type="button" className="btn btn-primary" disabled={!createReady} onClick={() => setEditing(null)}>Tạo combo mới</button>}>
      <p className="mb-4 text-sm leading-relaxed muted">Tạo dịch vụ một lần trong Dịch vụ của tôi, rồi chọn ít nhất hai dịch vụ đã duyệt của shop để bán theo giá combo. Combo cố định gồm mỗi dịch vụ 1 gói; giá theo bàn, khách hoặc giờ chưa được dùng khi chưa có số lượng cụ thể. Combo cần được kiểm duyệt riêng trước khi hiển thị cho khách.</p>
      {catalog.isPending ? <p role="status" className="mb-4 text-sm muted">Đang tải dịch vụ đủ điều kiện...</p> : catalog.isError ? <div role="alert" className="mb-4 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700"><p>Chưa tải được dịch vụ để tạo combo.</p><button type="button" className="btn btn-outline mt-2" onClick={() => void catalog.refetch()}>Thử lại</button></div> : eligible.length < 2 && <div className="mb-4 rounded-xl border border-[#e4dfcf] bg-[#faf8f0] p-4"><p className="text-sm font-semibold">{eligible.length === 1 ? "Cần thêm ít nhất một dịch vụ đủ điều kiện" : "Bạn chưa có dịch vụ đủ điều kiện để tạo combo"}</p><p className="mt-2 text-xs leading-relaxed muted">{eligible.length === 1 ? "Shop mới có một dịch vụ đã duyệt và đang hoạt động. Combo cần ít nhất hai dịch vụ độc lập." : pending ? "Shop có dịch vụ đang chờ Moderator duyệt. Sau khi được duyệt và đang hoạt động, dịch vụ sẽ xuất hiện ở đây." : "Hãy tạo dịch vụ mới, thiết lập giá và gửi kiểm duyệt trước khi kết hợp thành combo. Với dịch vụ cũ, mở chỉnh sửa để chọn cấu hình giá và gửi duyệt."}</p><Link className="btn btn-outline mt-3" to="/vendor/dashboard?tab=listings">{!listings.data?.length ? "Tạo dịch vụ đầu tiên" : "Đi đến Dịch vụ của tôi"}</Link></div>}
      <div className="mb-4 flex flex-wrap items-end gap-3 rounded-xl border border-[#e4dfcf] p-3" aria-label="Lọc combo của shop"><label className="min-w-0 flex-[1_1_220px]"><span className="label">Tìm combo</span><input className="input" type="search" maxLength={150} value={keyword} onChange={event => { setKeyword(event.target.value); setPage(1); }} placeholder="Tên combo, khu vực..." /></label><label><span className="label">Trạng thái</span><select className="input" value={status} onChange={event => changeStatus(event.target.value)}><option value="">Tất cả trạng thái</option>{VENDOR_COMBO_STATUSES.map(value => <option key={value} value={value}>{labels[value]}</option>)}</select></label><label><span className="label">Mỗi trang</span><select className="input" value={pageSize} onChange={event => { setPageSize(Number(event.target.value)); setPage(1); }}>{[6, 12, 24].map(size => <option key={size} value={size}>{size} combo</option>)}</select></label>{(keyword || status) && <button type="button" className="btn btn-outline" onClick={() => { setKeyword(""); changeStatus(""); }}>Xóa bộ lọc</button>}</div>
      {query.isPending ? <p role="status" className="text-sm muted">Đang tải combo của shop...</p> : query.isError ? <div role="alert"><p className="text-sm text-red-700">Chưa tải được combo của shop.</p><button type="button" className="btn btn-outline mt-2" onClick={() => void query.refetch()}>Thử lại</button></div> : <><p role="status" className="mb-3 text-xs muted">{filtered.totalCount} combo{query.isFetching ? " · Đang cập nhật..." : ""}</p>{filtered.items.length ? <div className="grid min-w-0 gap-4">{filtered.items.map(template => <ComboLifecycleCard key={template.id} template={template} onEdit={() => setEditing(template)} />)}</div> : <Empty>{keyword || status ? "Chưa có combo phù hợp bộ lọc. Thử từ khóa hoặc trạng thái khác." : "Chưa có combo nào. Chọn dịch vụ đã duyệt và đang hoạt động để tạo combo đầu tiên."}</Empty>}<Pagination page={filtered.page} pageSize={filtered.pageSize} totalCount={filtered.totalCount} onPageChange={setPage} isLoading={query.isFetching} label="Phân trang combo của shop" /></>}
      {editing !== undefined && <ComboTemplateEditor key={editing?.id ?? "new-vendor-combo"} template={editing ?? undefined} ownerVendorId={user?.vendorId} items={items} maxDiscount={participation.data?.maxComboDiscount ?? 0} onClose={() => setEditing(undefined)} />}
    </Panel>
    <Panel title="Thỏa thuận tham gia combo" eyebrow="Chính sách của shop"><p className="mb-3 text-xs leading-relaxed muted">Shop cần đồng ý tham gia để combo đủ điều kiện xuất hiện công khai. Giới hạn ưu đãi được lưu theo thỏa thuận hiện hành.</p>{participation.isPending ? <p role="status" className="text-sm muted">Đang tải thỏa thuận...</p> : participation.isError ? <div role="alert"><p className="text-sm text-red-700">Chưa tải được thỏa thuận.</p><button type="button" className="btn btn-outline mt-2" onClick={() => void participation.refetch()}>Thử lại</button></div> : <div className="flex flex-wrap items-center gap-3"><label className="text-sm"><input type="checkbox" checked={allow ?? participation.data?.allowInCombos ?? false} onChange={event => setAllow(event.target.checked)} /> Cho phép dịch vụ đã duyệt vào combo</label><label className="text-sm">Trần giảm (%) <input aria-label="Trần giảm combo" className="input !w-24" type="number" min={0} max={30} value={discount ?? (participation.data?.maxComboDiscount ?? 0) * 100} onChange={event => setDiscount(Number(event.target.value))} /></label><button type="button" className="btn btn-primary" disabled={consent.isPending} onClick={() => consent.mutate({ allowInCombos: allow ?? participation.data?.allowInCombos ?? false, maxComboDiscount: (discount ?? (participation.data?.maxComboDiscount ?? 0) * 100) / 100 })}>{consent.isPending ? "Đang lưu..." : "Lưu thỏa thuận"}</button>{consent.isSuccess && <p role="status" className="text-xs text-emerald-700">Đã lưu thỏa thuận.</p>}</div>}<ErrorText>{consent.isError ? apiError(consent.error) : ""}</ErrorText></Panel>
  </>;
}
