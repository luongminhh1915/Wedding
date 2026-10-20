import { useState } from "react";
import { useSearchParams, Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, Eye, Loader2, RefreshCw } from "lucide-react";
import { useAuthStore } from "../../store/authStore";
import axiosClient from "../../services/axiosClient";
import { Pagination } from "../../components/Pagination";
import { ServiceDetails } from "../customer/components/ServiceDetails";
import { listingImagePreview } from "../vendor-portal/components/listingEditModel";
import { apiError } from "../journey/api";
import { comboDate, comboPreviewPath } from "../journey/combo/comboTemplateEditorModel";
import { useModerationQueue } from "./useModerationQueue";
import { MODERATION_TYPES, MODERATION_STATUSES, moderationFilters, moderationSelection, updateModerationFilters, selectModerationRow,
  moderationStatus, moderationDecision, type ModerationDetail, type ModerationFilters } from "./moderationQueueModel";

const button = "rounded-xl border border-[#dcd8ca] bg-white px-4 py-2 text-sm font-semibold text-[#25334c] disabled:opacity-50";
const input = "min-w-0 w-full rounded-xl border border-[#dcd8ca] bg-white px-3 py-2 text-sm text-[#25334c]";
const money = (value: number) => `${value.toLocaleString("vi-VN")} ₫`;
const label = (status: string) => MODERATION_STATUSES.find(([value]) => value === status)?.[1] ?? status;
const kindLabel = (type: string) => MODERATION_TYPES.find(([value]) => value === type)?.[1] ?? type;
function Gallery({ urls }: { urls: string[] }) {
  return urls.length > 0 ? <div className="my-5 grid grid-cols-2 gap-3 sm:grid-cols-3">{[...new Set(urls)].map((url, index) => <a key={url} href={listingImagePreview(url, import.meta.env.VITE_API_URL || "")} target="_blank" rel="noreferrer"><img src={listingImagePreview(url, import.meta.env.VITE_API_URL || "")} alt={`Ảnh nội dung đang xem ${index + 1}`} loading="lazy" className="aspect-square w-full rounded-xl object-cover" /></a>)}</div> : <p className="my-4 text-xs text-slate-500">Nội dung này chưa có ảnh.</p>;
}
function Content({ detail }: { detail: ModerationDetail }) {
  const listing = detail.listing; const item = detail.serviceItem; const combo = detail.combo;
  if (listing) return <>
    <div className="flex flex-wrap items-start justify-between gap-3"><div><h3 className="text-xl font-semibold">{listing.title}</h3><p className="mt-2 text-xs text-slate-500">{listing.categoryName} · {listing.vendorBrandName}</p></div><Link className={button} to={`/listings/${listing.id}?preview=vendor${listing.latestRevision ? `&revision=${listing.latestRevision.id}` : ""}`} target="_blank" rel="noreferrer">Xem trước đầy đủ</Link></div>
    <dl className="mt-5 grid gap-3 text-sm sm:grid-cols-2"><div><dt className="text-xs text-slate-500">Khoảng giá niêm yết</dt><dd>{money(listing.minPrice)} – {money(listing.maxPrice)}</dd></div><div><dt className="text-xs text-slate-500">Khu vực</dt><dd>{listing.location}</dd></div></dl>
    <h4 className="mt-5 font-semibold">Mô tả</h4><p className="my-3 whitespace-pre-wrap text-sm leading-relaxed text-slate-600">{listing.description}</p>
    <Gallery urls={listing.media.map(image => image.mediaUrl)} /><ServiceDetails details={listing.serviceDetails} cancellationPolicy={listing.cancellationPolicy} />
    {listing.latestRevision?.reviewNote && <p className="mt-3 whitespace-pre-wrap text-sm">Phản hồi: {listing.latestRevision.reviewNote}</p>}
  </>;
  if (item) return <>
    <h3 className="text-xl font-semibold">{item.name}</h3><p className="mt-2 text-xs text-slate-500">{item.vendorName} · {item.isActive ? "Đang bật kinh doanh" : "Đang tắt kinh doanh"}</p>
    <dl className="mt-5 grid gap-3 text-sm sm:grid-cols-2"><div><dt className="text-xs text-slate-500">Giá / đơn vị</dt><dd>{money(item.basePrice)} / {item.unit}</dd></div><div><dt className="text-xs text-slate-500">Khu vực / sức chứa</dt><dd>{item.regions} · {item.minGuests}–{item.maxGuests} khách</dd></div><div><dt className="text-xs text-slate-500">Số đơn tối đa mỗi ngày</dt><dd>{item.maxBookingsPerDay}</dd></div></dl>
    <Link className={button + " mt-4 inline-block"} to={`/listings/${item.listingId}?preview=vendor`} target="_blank" rel="noreferrer">Xem ảnh & bài giới thiệu liên quan</Link>
    <h4 className="mt-5 font-semibold">Biến thể & món kèm</h4><ul className="mt-3 space-y-2 text-sm">{item.variants.map(variant => <li key={variant.id ?? variant.name}>{variant.name} · chênh lệch {money(variant.priceDelta)}</li>)}{item.addOns.map(addOn => <li key={addOn.id ?? addOn.name}>{addOn.name} · {money(addOn.price)}{addOn.perUnit ? "/đơn vị" : "/gói"}</li>)}</ul>
    {!item.variants.length && !item.addOns.length && <p className="mt-2 text-xs text-slate-500">Không có biến thể hoặc món kèm.</p>}
    <h4 className="mt-5 font-semibold">Điều kiện hủy</h4><p className="mt-2 whitespace-pre-wrap text-sm">{item.cancellationPolicy}</p>{item.reviewNote && <p className="mt-3 whitespace-pre-wrap text-sm">Phản hồi: {item.reviewNote}</p>}
  </>;
  if (combo) return <>
    <div className="flex flex-wrap items-start justify-between gap-3"><div><h3 className="text-xl font-semibold">{combo.name}</h3><p className="mt-2 text-xs text-slate-500">{combo.ownerVendorName || "Chưa gắn NCC"} · {combo.style} · {combo.region || "Chưa ghi khu vực"}</p></div><Link className={button} to={comboPreviewPath(combo.id, combo.latestRevision?.id)} target="_blank" rel="noreferrer">Xem trước đầy đủ</Link></div>
    <Gallery urls={[combo.imageUrl, ...(combo.content?.galleryUrls ?? [])].filter((url): url is string => !!url)} />
    <p className="whitespace-pre-wrap text-sm leading-relaxed">{combo.description}</p>
    <p className="mt-3 text-xs text-slate-500">Thời hạn: {combo.content?.validFromUtc ? comboDate(combo.content.validFromUtc) : "Không đặt ngày bắt đầu"} → {combo.content?.validUntilUtc ? comboDate(combo.content.validUntilUtc) : "Không đặt ngày kết thúc"}</p>
    <h4 className="mt-5 font-semibold">Các hạng mục ({combo.items.length})</h4><ul className="mt-3 space-y-3">{combo.items.map(slot => { const option = combo.previewOptions?.find(option => option.serviceItemId === slot.serviceItemId); return <li key={slot.categoryId} className="rounded-xl border border-[#e4dfcf] p-3 text-sm"><b>{option?.title || "Dịch vụ không còn khả dụng"}</b><p className="mt-1 text-xs text-slate-500">{slot.isOptional ? "Tùy chọn" : "Bao gồm"} · {option ? `${money(option.price)}/${option.unit}` : slot.serviceItemId} · {slot.allowedItemIds?.length ?? 0} lựa chọn thay thế</p></li>; })}</ul>
    <h4 className="mt-5 font-semibold">Điều khoản</h4><p className="mt-2 whitespace-pre-wrap text-sm">{combo.content?.terms || "Chưa ghi điều khoản bổ sung."}</p>{combo.latestRevision?.reviewNote && <p className="mt-3 whitespace-pre-wrap text-sm">Phản hồi: {combo.latestRevision.reviewNote}</p>}
  </>;
  return <p role="alert">Không đọc được nội dung đang chọn. Hãy tải lại trước khi duyệt.</p>;
}
function ReviewDetail({ detail, onRefresh, onDone }: { detail: ModerationDetail; onRefresh: () => void; onDone: () => void }) {
  const qc = useQueryClient(); const [note, setNote] = useState(""); const [validation, setValidation] = useState("");
  const review = useMutation({ mutationFn: async (approve: boolean) => { const request = moderationDecision(detail, approve, note); return axiosClient.post(request.url, request.body, { params: request.params }); },
    onSuccess: async () => { onDone(); await qc.invalidateQueries(); } });
  const pending = moderationStatus(detail) === "Pending";
  const decide = (approve: boolean) => { setValidation(""); try { moderationDecision(detail, approve, note); review.mutate(approve); } catch (error) { setValidation(error instanceof Error ? error.message : "Nội dung kiểm duyệt không hợp lệ."); } };
  return <article className="min-w-0 rounded-2xl border border-[#e4dfcf] bg-white p-4 text-[#25334c] [overflow-wrap:anywhere] sm:p-6" aria-label="Nội dung phiên bản được chọn">
    <p className="mb-4 text-xs text-slate-500">{kindLabel(detail.type)} · {label(moderationStatus(detail))}</p><Content detail={detail} />
    <p role="note" className="mt-5 rounded-xl bg-[#faf8f0] p-3 text-xs leading-relaxed">Đang xem nội dung riêng của phiên bản được chọn. Duyệt nội dung không thay đổi hoa hồng, giao dịch đã chốt hoặc tự bật lại trạng thái kinh doanh.</p>
    {pending && <fieldset disabled={review.isPending} className="mt-5 min-w-0 border-t border-slate-100 pt-4"><legend className="sr-only">Quyết định kiểm duyệt</legend>
      <label className="block text-sm font-semibold" htmlFor="moderation-review-note">Ghi chú / lý do từ chối (bắt buộc khi từ chối)</label><textarea id="moderation-review-note" className={input + " mt-2"} rows={3} maxLength={detail.type === "listing" ? 1000 : 2000} value={note} onChange={event => setNote(event.target.value)} />
      <p className="mt-2 text-xs text-slate-500">Chỉ áp dụng cho đúng phiên bản đang xem. Nếu NCC đã chỉnh sửa, máy chủ sẽ yêu cầu tải lại.</p>
      <div className="mt-4 flex flex-wrap justify-end gap-3"><button className={button} disabled={!note.trim()} onClick={() => decide(false)}>Từ chối kèm lý do</button><button className={button + " !bg-[#25334c] !text-white"} onClick={() => decide(true)}>{review.isPending ? "Đang gửi..." : "Phê duyệt phiên bản"}</button></div>
    </fieldset>}
    {(validation || review.isError) && <div role="alert" className="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-700"><p>{validation || apiError(review.error)}</p><button className={button + " mt-3"} disabled={review.isPending} onClick={onRefresh}>Tải lại đúng phiên bản</button></div>}
  </article>;
}
export default function ModerationQueue() {
  const [params, setParams] = useSearchParams(); const { user, token } = useAuthStore();
  const filters = moderationFilters(params); const selected = moderationSelection(params); const query = useModerationQueue(filters);
  const [notice, setNotice] = useState("");
  const detail = useQuery({ queryKey: ["moderation-detail", user?.id, user?.role, selected?.type, selected?.id, selected?.revisionId], enabled: !!token && !!selected && ["Moderator", "SuperAdmin"].includes(user?.role ?? ""),
    retry: false, refetchOnWindowFocus: false, refetchOnReconnect: false,
    queryFn: async () => (await axiosClient.get<ModerationDetail>(`/api/moderation-queue/${selected!.type}/${selected!.id}`, { params: { revisionId: selected!.revisionId } })).data });
  const update = (patch: Partial<ModerationFilters>) => { setNotice(""); setParams(updateModerationFilters(params, patch)); };
  const refreshDetail = () => { if (!selected) return; const next = new URLSearchParams(params); next.delete("reviewRevision"); setParams(next, { replace: true }); void detail.refetch(); void query.refetch(); };
  const done = () => { const next = new URLSearchParams(params); for (const key of ["reviewType", "reviewId", "reviewRevision"]) next.delete(key); setParams(next, { replace: true }); setNotice("Đã lưu quyết định và thông báo tới đúng NCC. Các dữ liệu tài chính được giữ nguyên."); };
  const version = detail.data?.serviceItem?.reviewVersion ?? detail.data?.listing?.latestRevision?.id ?? detail.data?.combo?.latestRevision?.id ?? "legacy";
  return <section aria-label="Hàng chờ kiểm duyệt thống nhất" className="min-w-0">
    <div className="mb-5 flex flex-wrap items-center justify-between gap-3"><div><h2 className="text-xl font-semibold text-[#25334c]">Hàng chờ nội dung</h2><p className="mt-1 text-xs text-slate-500">Bài giới thiệu, dịch vụ thành phần và combo · {query.data?.pendingCount ?? "…"} nội dung chờ duyệt.</p></div><button className={button} onClick={() => { void query.refetch(); if (selected) void detail.refetch(); }}>Tải lại hàng chờ</button></div>
    <div className="mb-5 grid min-w-0 gap-3 rounded-2xl border border-[#e4dfcf] bg-white p-4 sm:grid-cols-2 lg:grid-cols-[1fr_1fr_2fr_auto]">
      <label className="min-w-0 text-xs font-semibold text-slate-600">Loại nội dung<select aria-label="Loại nội dung kiểm duyệt" className={input + " mt-2"} value={filters.type} onChange={event => update({ type: event.target.value })}>{MODERATION_TYPES.map(([value, text]) => <option key={value} value={value}>{text}</option>)}</select></label>
      <label className="min-w-0 text-xs font-semibold text-slate-600">Trạng thái<select aria-label="Trạng thái kiểm duyệt" className={input + " mt-2"} value={filters.status} onChange={event => update({ status: event.target.value })}>{MODERATION_STATUSES.map(([value, text]) => <option key={value} value={value}>{text}</option>)}</select></label>
      <form key={filters.search} className="flex min-w-0 items-end gap-2" onSubmit={event => { event.preventDefault(); update({ search: String(new FormData(event.currentTarget).get("search") ?? "") }); }}><label className="min-w-0 flex-1 text-xs font-semibold text-slate-600">Tên đề xuất / nhà cung cấp<input name="search" className={input + " mt-2"} defaultValue={filters.search} maxLength={150} placeholder="Tìm trong nội dung đề xuất mới nhất" /></label><button className={button}>Tìm</button></form>
      <button className={button + " self-end"} onClick={() => update({ type: "all", status: "Pending", search: "", page: 1, pageSize: 10 })}>Xóa bộ lọc</button>
    </div>
    {notice && <p role="status" className="mb-4 rounded-xl border border-green-200 bg-green-50 p-3 text-sm text-green-800">{notice}</p>}
    {query.isPending ? <p role="status" className="flex items-center gap-2 p-8"><Loader2 className="animate-spin" size={18} />Đang tải hàng chờ...</p> : query.isError ? <div role="alert" className="rounded-xl bg-red-50 p-4"><p>{apiError(query.error)}</p><button className={button + " mt-3"} onClick={() => void query.refetch()}><RefreshCw size={16} className="mr-2 inline" />Thử lại</button></div> : <>
      <div className="grid min-w-0 gap-5 lg:grid-cols-[300px_minmax(0,1fr)]"><div className="min-w-0 space-y-3">
        {!query.data?.items.length && <div className="rounded-2xl border border-[#e4dfcf] bg-white p-6 text-center text-slate-500"><CheckCircle2 className="mx-auto mb-3" /><p>Không có nội dung phù hợp bộ lọc.</p></div>}
        {query.data?.items.map(row => <button key={`${row.type}:${row.id}:${row.revisionId ?? row.changedAt}`} className={`w-full rounded-2xl border p-4 text-left [overflow-wrap:anywhere] ${selected?.id === row.id && selected.type === row.type ? "border-[#c9a740] bg-[#faf8f0]" : "border-[#e4dfcf] bg-white"}`} onClick={() => { setNotice(""); setParams(selectModerationRow(params, row)); }}><span className="block text-xs text-slate-500">{kindLabel(row.type)} · {label(row.status)}</span><b className="mt-2 block text-sm text-[#25334c]">{row.title}</b><span className="mt-2 block text-xs text-slate-500">{row.vendorName}</span><span className="mt-2 block text-xs text-slate-500">{row.revisionSequence ? `Phiên bản #${row.revisionSequence}` : "Nội dung hiện tại"} · {comboDate(row.changedAt)}</span></button>)}
      </div><div className="min-w-0">{!selected ? <div className="rounded-2xl border border-dashed border-[#dcd8ca] p-10 text-center text-slate-500"><Eye className="mx-auto mb-3" /><p>Chọn nội dung để xem đúng đề xuất, ảnh và điều khoản trước khi duyệt.</p></div> : detail.isPending ? <p role="status" className="p-8">Đang tải phiên bản được chọn...</p> : detail.isError ? <div role="alert" className="rounded-xl bg-red-50 p-4"><p>{apiError(detail.error)}</p><button className={button + " mt-3"} onClick={refreshDetail}>Tải lại đúng phiên bản</button></div> : detail.data && <ReviewDetail key={`${detail.data.type}:${detail.data.id}:${version}`} detail={detail.data} onRefresh={refreshDetail} onDone={done} />}</div></div>
      {query.data && <Pagination page={query.data.page} pageSize={query.data.pageSize} totalCount={query.data.totalCount} isLoading={query.isFetching} onPageChange={page => update({ page })} label="Phân trang hàng chờ kiểm duyệt" />}
    </>}
  </section>;
}
