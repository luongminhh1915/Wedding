import { useState } from "react";
import { Link } from "react-router-dom";
import { Eye, Pencil, Send, Pause, Play } from "lucide-react";
import { useSetListingActivity, useSubmitListing } from "../hooks/useListings";
import { listingCanEdit, listingCanSubmit, listingPreviewUrl } from "../listingLifecycle";
import { apiError } from "../../journey/api";
import type { ListingSummaryDto } from "../../../types/listing.types";

const button = "inline-flex items-center justify-center gap-2 rounded-xl border border-[#dcd8ca] bg-white px-3 py-2 text-xs font-semibold text-[#25334c] hover:bg-[#f5f2e8] disabled:opacity-50";
export function VendorListingActions({ listing, edit }: { listing: ListingSummaryDto; edit: () => void }) {
  const submit = useSubmitListing(); const activity = useSetListingActivity(); const [message, setMessage] = useState('');
  const pending = submit.isPending || activity.isPending;
  const send = async () => {
    if (!confirm(`Gửi "${listing.title}" để kiểm duyệt? Bản công khai hiện tại và các giao dịch đã chốt được giữ nguyên.`)) return;
    setMessage('');
    try { await submit.mutateAsync(listing.id); setMessage('Đã gửi phiên bản để kiểm duyệt.'); }
    catch { /* Render the API error next to the action. */ }
  };
  return <div className="min-w-0 max-w-full">
    <div className="flex flex-wrap gap-2">
      <Link className={button} to={listingPreviewUrl(listing)} aria-label={`Xem trước dịch vụ ${listing.title}`}><Eye size={15} />Xem trước</Link>
      {listingCanEdit(listing) && <button type="button" className={button} onClick={edit} disabled={pending} aria-label={`Chỉnh sửa dịch vụ ${listing.title}`}><Pencil size={15} />{['Active','Hidden'].includes(listing.status) ? 'Sửa phiên bản' : 'Chỉnh sửa'}</button>}
      {listingCanSubmit(listing) && <button type="button" className={button} disabled={pending} onClick={() => void send()} aria-label={`Gửi duyệt dịch vụ ${listing.title}`}><Send size={15} />{submit.isPending ? 'Đang gửi...' : 'Gửi duyệt'}</button>}
      {typeof listing.isActive === 'boolean' && ['Active','Hidden'].includes(listing.status) && <button type="button" className={button} disabled={pending} onClick={() => activity.mutate({ id: listing.id, isActive: !listing.isActive })} aria-label={`${listing.isActive ? 'Tạm ngừng' : 'Kinh doanh lại'} dịch vụ ${listing.title}`}>{listing.isActive ? <Pause size={15} /> : <Play size={15} />}{listing.isActive ? 'Tạm ngừng' : 'Kinh doanh lại'}</button>}
    </div>
    {(submit.isError || activity.isError) && <p role="alert" className="mt-2 text-xs text-red-700">{apiError(submit.error ?? activity.error)}</p>}
    {message && <p role="status" className="mt-2 text-xs text-slate-600">{message}</p>}
  </div>;
}
