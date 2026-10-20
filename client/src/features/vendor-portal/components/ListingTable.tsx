import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Image, Clock, Search, Plus, RefreshCw } from "lucide-react";
import { useMyListingsPage } from "../hooks/useListings";
import { changeVendorListingFilters, listingUpdatedTime, vendorListingFilters, VENDOR_LISTING_PAGE_SIZES, VENDOR_LISTING_STATUSES } from "../vendorListingFilters";
import { Pagination } from "../../../components/Pagination";
import { VendorListingActions } from "./VendorListingActions";
import ListingStatusBadge from "./ListingStatusBadge";
import ListingForm from "./ListingForm";
import type { ListingStatus, ListingSummaryDto } from "../../../types/listing.types";

interface Props { categories: { id: string; name: string; requiresCommissionBasisApproval?: boolean }[]; }
const statusNames: Record<ListingStatus, string> = { Draft: "Nháp", PendingApproval: "Chờ duyệt", Active: "Đã duyệt", Rejected: "Bị từ chối", Hidden: "Đã ẩn" };
const outline = "inline-flex items-center justify-center gap-2 rounded-xl border border-[#dcd8ca] bg-white px-3 py-2 text-xs font-semibold text-[#25334c] hover:bg-[#f5f2e8] focus-visible:outline-2 focus-visible:outline-[#c9a740] disabled:opacity-50";
const primary = "inline-flex items-center justify-center gap-2 rounded-xl bg-[#25334c] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#354762] disabled:opacity-50";

export default function ListingTable({ categories }: Props) {
  const [params, setParams] = useSearchParams();
  const filters = vendorListingFilters(params);
  const query = useMyListingsPage(filters);
  const [editingListing, setEditingListing] = useState<ListingSummaryDto | null>(null);
  const [showForm, setShowForm] = useState(false);
  const listings = query.data?.items ?? [];
  const filtered = !!filters.keyword || !!filters.status || !!filters.categoryId;
  const change = (values: Parameters<typeof changeVendorListingFilters>[1]) => setParams(changeVendorListingFilters(params, values));
  const reset = () => change({ keyword: "", categoryId: undefined, status: undefined, page: 1 });
  const create = () => { setEditingListing(null); setShowForm(true); };

  return <section className="min-w-0" aria-labelledby="vendor-services-title">
    <div className="flex flex-wrap items-start justify-between gap-4 mb-5">
      <div><p className="text-xs uppercase tracking-wider text-slate-500 mb-2">Danh mục của shop</p><h2 id="vendor-services-title" className="text-xl font-semibold text-[#25334c]">Dịch vụ của tôi</h2><p className="mt-2 text-xs leading-relaxed text-slate-500 flex items-start gap-2"><Clock size={15} className="shrink-0" />Lưu nháp, xem trước và gửi kiểm duyệt trước khi xuất bản.</p></div>
      <button type="button" className={primary} onClick={create}><Plus size={16} />Tạo dịch vụ</button>
    </div>
    <form onSubmit={event => { event.preventDefault(); change({ keyword: String(new FormData(event.currentTarget).get("keyword") ?? "").trim().slice(0, 200) }); }} className="mb-5 flex min-w-0 flex-wrap items-end gap-3 rounded-2xl border border-[#e4dfcf] bg-[#faf8f0] p-4" aria-label="Lọc dịch vụ của shop">
      <div className="min-w-0 flex-[1_1_220px]"><label htmlFor="vendor-services-keyword" className="mb-1.5 block text-xs font-semibold text-[#25334c]">Tìm dịch vụ</label><div className="flex gap-2"><input id="vendor-services-keyword" key={filters.keyword} name="keyword" type="search" maxLength={200} defaultValue={filters.keyword} placeholder="Tên, ngành hoặc khu vực..." className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm focus:outline-2 focus:outline-[#c9a740]" /><button type="submit" className={outline} aria-label="Tìm dịch vụ của shop"><Search size={16} /><span>Tìm</span></button></div></div>
      <div className="min-w-0 flex-[1_1_150px]"><label htmlFor="vendor-services-status" className="mb-1.5 block text-xs font-semibold text-[#25334c]">Trạng thái</label><select id="vendor-services-status" value={filters.status ?? ""} onChange={event => change({ status: event.target.value ? event.target.value as ListingStatus : undefined })} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm"><option value="">Tất cả trạng thái</option>{VENDOR_LISTING_STATUSES.map(status => <option key={status} value={status}>{statusNames[status]}</option>)}</select></div>
      <div><label htmlFor="vendor-services-size" className="mb-1.5 block text-xs font-semibold text-[#25334c]">Mỗi trang</label><select id="vendor-services-size" value={filters.pageSize} onChange={event => change({ pageSize: Number(event.target.value) })} className="rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm">{VENDOR_LISTING_PAGE_SIZES.map(size => <option key={size} value={size}>{size} dịch vụ</option>)}</select></div>
      <div className="min-w-0 flex-[1_1_160px]"><label htmlFor="vendor-services-category" className="mb-1.5 block text-xs font-semibold text-[#25334c]">Danh mục</label><select id="vendor-services-category" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm" value={filters.categoryId ?? ""} onChange={event => change({ categoryId: event.target.value || undefined })}><option value="">Tất cả danh mục</option>{categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select></div>
      {filtered && <button type="button" className={outline} onClick={reset}>Xóa bộ lọc</button>}
    </form>
    <div aria-busy={query.isFetching}>
      {query.isPending ? <p role="status" className="py-12 text-center text-sm text-slate-500">Đang tải dịch vụ của shop...</p> : query.isError ? <div role="alert" className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center"><p className="text-sm text-red-700">Không tải được dịch vụ. Bộ lọc và dữ liệu đã lưu vẫn được giữ nguyên.</p><button type="button" className={`${outline} mt-4`} onClick={() => void query.refetch()}><RefreshCw size={15} />Thử lại</button></div> : <>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2"><p role="status" aria-live="polite" className="text-xs text-slate-500">{query.data?.totalCount ?? 0} dịch vụ{filtered ? " phù hợp bộ lọc" : " trong shop"}{query.isFetching ? " · Đang cập nhật..." : ""}</p><p className="text-xs text-slate-500">Mới cập nhật trước</p></div>
        {listings.length === 0 ? <div className="rounded-2xl border border-dashed border-[#dcd8ca] bg-white px-4 py-12 text-center"><Image size={30} className="mx-auto mb-3 text-slate-400" aria-hidden="true" /><h3 className="font-semibold text-[#25334c]">{(query.data?.totalCount ?? 0) > 0 ? "Trang này chưa có dịch vụ" : filtered ? "Chưa có dịch vụ phù hợp" : "Shop chưa có dịch vụ nào"}</h3><p className="mt-2 text-sm text-slate-500">{filtered ? "Thử từ khóa khác hoặc xóa bộ lọc để xem lại danh sách." : "Tạo dịch vụ đầu tiên, lưu nháp và gửi duyệt khi đã sẵn sàng."}</p><div className="mt-4 flex flex-wrap justify-center gap-2">{(query.data?.totalCount ?? 0) > 0 && <button type="button" className={outline} onClick={() => change({ page: Math.max(1, query.data?.totalPages ?? 1) })}>Về trang cuối có dữ liệu</button>}{filtered ? <button type="button" className={outline} onClick={reset}>Xóa bộ lọc</button> : <button type="button" className={primary} onClick={create}>Tạo dịch vụ đầu tiên</button>}</div></div> : <ul className="grid min-w-0 grid-cols-1 gap-3 list-none p-0 m-0">{listings.map(listing => <li key={listing.id} className="min-w-0 rounded-2xl border border-[#e4dfcf] bg-white p-4 sm:p-5">
          <div className="flex min-w-0 items-start gap-3 sm:gap-4"><div className="h-16 w-16 sm:h-20 sm:w-20 shrink-0 overflow-hidden rounded-xl bg-[#f5f2e8]">{listing.primaryImageUrl ? <img src={listing.primaryImageUrl} alt="" loading="lazy" className="h-full w-full object-cover" /> : <Image className="h-full w-full p-5 text-slate-400" aria-hidden="true" />}</div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-start justify-between gap-2"><h3 className="min-w-0 break-words text-sm sm:text-base font-semibold text-[#25334c]">{listing.title}</h3><ListingStatusBadge status={listing.status} /></div><p className="mt-1 break-words text-xs leading-relaxed text-slate-500">{listing.categoryName} · {listing.location}</p><p className="mt-2 text-sm font-semibold text-[#25334c]">{listing.minPrice.toLocaleString("vi-VN")} ₫{listing.maxPrice !== listing.minPrice && <> – {listing.maxPrice.toLocaleString("vi-VN")} ₫</>}</p></div></div>
          {['Active','Hidden'].includes(listing.status) && <p className="mt-3 text-xs text-slate-600">{listing.isActive === false ? 'Tạm ngừng kinh doanh' : 'Đang kinh doanh'}{listing.status === 'Hidden' ? ' · Bị ẩn bởi kiểm duyệt; bật kinh doanh không gỡ lệnh ẩn' : ''}</p>}
          {['Draft','Pending','Rejected'].includes(listing.revisionStatus ?? '') && <div className="mt-3 rounded-xl border border-[#e4dfcf] bg-[#faf8f0] p-3 text-xs"><p>Bản sửa riêng: {listing.revisionStatus === 'Draft' ? 'Nháp, chưa gửi' : listing.revisionStatus === 'Pending' ? 'Đang chờ duyệt' : 'Cần chỉnh sửa'}. Bản đã công khai được giữ nguyên.</p>{listing.revisionReviewNote && <p className="mt-1 whitespace-pre-wrap text-red-700">{listing.revisionReviewNote}</p>}</div>}
          {listing.status === "Rejected" && listing.rejectionReason && <p className="mt-3 break-words rounded-xl border border-red-100 bg-red-50 p-3 text-xs leading-relaxed text-red-700"><b>Lý do từ chối:</b> {listing.rejectionReason}</p>}
          <div className="mt-4 flex min-w-0 flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-3"><div className="text-xs leading-relaxed text-slate-500"><p>Cập nhật: {listingUpdatedTime(listing.updatedAt, listing.createdAt)}</p><p>{listing.viewCount.toLocaleString("vi-VN")} lượt xem</p></div><VendorListingActions listing={listing} edit={() => { setEditingListing(listing); setShowForm(true); }} /></div>
        </li>)}</ul>}
        {listings.length > 0 && query.data && <Pagination page={query.data.page} pageSize={query.data.pageSize} totalCount={query.data.totalCount} isLoading={query.isFetching} onPageChange={page => change({ page })} label="Phân trang dịch vụ của shop" />}
      </>}
    </div>
    {showForm && <ListingForm editing={editingListing ?? undefined} categories={categories} onClose={() => { setShowForm(false); setEditingListing(null); }} />}
  </section>;
}
