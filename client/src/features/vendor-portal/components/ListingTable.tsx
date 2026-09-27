import { useState } from 'react';
import { Pencil, Send, Image, Eye, Clock } from 'lucide-react';
import { useMyListings, useSubmitListing } from '../hooks/useListings';
import ListingStatusBadge from './ListingStatusBadge';
import ListingForm from './ListingForm';
import type { ListingSummaryDto } from '../../../types/listing.types';

interface Props {
  categories: { id: string; name: string }[];
}

export default function ListingTable({ categories }: Props) {
  const { data: listings = [], isLoading, isError } = useMyListings();
  const submitMutation = useSubmitListing();
  const [editingListing, setEditingListing] = useState<ListingSummaryDto | null>(null);
  const [showForm, setShowForm] = useState(false);

  const canSubmit = (l: ListingSummaryDto) =>
    l.status === 'Draft' || l.status === 'Rejected';

  const canEdit = (l: ListingSummaryDto) =>
    l.status === 'Draft' || l.status === 'Rejected';

  const handleSubmit = async (id: string) => {
    if (!confirm('Bạn có chắc muốn nộp bài đăng này lên hàng chờ kiểm duyệt (SLA 24h)?')) return;
    await submitMutation.mutateAsync(id);
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-slate-500">
        <div className="w-6 h-6 border-2 border-rose-400 border-t-transparent rounded-full animate-spin mr-3" />
        Đang tải danh sách bài đăng...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="text-center py-12 text-red-500">
        <p className="font-semibold">Không thể tải dữ liệu. Vui lòng thử lại.</p>
      </div>
    );
  }

  return (
    <>
      {/* Header actions */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-xl font-bold text-slate-900">Danh Sách Gói Dịch Vụ</h2>
          <p className="text-sm text-slate-500 mt-0.5 flex items-center gap-1">
            <Clock className="w-3.5 h-3.5 text-amber-500" />
            SLA Kiểm duyệt tối đa 24h sau khi nộp (BR-008)
          </p>
        </div>
        <button
          onClick={() => { setEditingListing(null); setShowForm(true); }}
          className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-rose-500 hover:bg-rose-600 text-white text-sm font-semibold shadow-sm shadow-rose-200 transition"
        >
          + Đăng Gói Mới
        </button>
      </div>

      {/* Empty state */}
      {listings.length === 0 && (
        <div className="text-center py-16 bg-slate-50 rounded-2xl border border-dashed border-slate-300">
          <div className="w-14 h-14 bg-rose-50 rounded-2xl flex items-center justify-center mx-auto mb-4">
            <Image className="w-7 h-7 text-rose-400" />
          </div>
          <h3 className="text-base font-semibold text-slate-700">Chưa có gói dịch vụ nào</h3>
          <p className="text-sm text-slate-500 mt-1 mb-5">Tạo bài đăng đầu tiên để bắt đầu tiếp nhận khách hàng.</p>
          <button
            onClick={() => { setEditingListing(null); setShowForm(true); }}
            className="px-5 py-2 rounded-xl bg-rose-500 text-white text-sm font-semibold hover:bg-rose-600 transition"
          >
            + Tạo Bài Đăng Ngay
          </button>
        </div>
      )}

      {/* Table */}
      {listings.length > 0 && (
        <div className="overflow-x-auto rounded-2xl border border-slate-200">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="px-4 py-3 text-left text-xs font-bold text-slate-500 uppercase tracking-wider">Gói Dịch Vụ</th>
                <th className="px-4 py-3 text-left text-xs font-bold text-slate-500 uppercase tracking-wider">Ngành</th>
                <th className="px-4 py-3 text-left text-xs font-bold text-slate-500 uppercase tracking-wider">Khoảng Giá</th>
                <th className="px-4 py-3 text-left text-xs font-bold text-slate-500 uppercase tracking-wider">Trạng Thái</th>
                <th className="px-4 py-3 text-left text-xs font-bold text-slate-500 uppercase tracking-wider">Lượt Xem</th>
                <th className="px-4 py-3 text-right text-xs font-bold text-slate-500 uppercase tracking-wider">Hành Động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {listings.map((listing) => (
                <tr key={listing.id} className="bg-white hover:bg-slate-50/70 transition">
                  <td className="px-4 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-rose-50 flex items-center justify-center flex-shrink-0 overflow-hidden">
                        {listing.primaryImageUrl ? (
                          <img src={listing.primaryImageUrl} alt="" className="w-full h-full object-cover" />
                        ) : (
                          <Image className="w-5 h-5 text-rose-300" />
                        )}
                      </div>
                      <div>
                        <p className="font-semibold text-slate-900 line-clamp-1">{listing.title}</p>
                        <p className="text-xs text-slate-400">{listing.location}</p>
                      </div>
                    </div>
                    {listing.status === 'Rejected' && listing.rejectionReason && (
                      <div className="mt-2 px-3 py-1.5 bg-red-50 border border-red-100 rounded-lg text-xs text-red-600">
                        <span className="font-semibold">Lý do từ chối:</span> {listing.rejectionReason}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-4 text-slate-600">{listing.categoryName}</td>
                  <td className="px-4 py-4">
                    <span className="font-medium text-slate-900">
                      {listing.minPrice.toLocaleString('vi-VN')}đ
                    </span>
                    <span className="text-slate-400 mx-1">—</span>
                    <span className="font-medium text-slate-900">
                      {listing.maxPrice.toLocaleString('vi-VN')}đ
                    </span>
                  </td>
                  <td className="px-4 py-4">
                    <ListingStatusBadge status={listing.status} />
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex items-center gap-1 text-slate-500">
                      <Eye className="w-3.5 h-3.5" />
                      <span className="text-xs">{listing.viewCount}</span>
                    </div>
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex items-center justify-end gap-2">
                      {canEdit(listing) && (
                        <button
                          onClick={() => { setEditingListing(listing); setShowForm(true); }}
                          title="Chỉnh sửa"
                          className="p-1.5 rounded-lg hover:bg-slate-100 text-slate-500 transition"
                        >
                          <Pencil className="w-4 h-4" />
                        </button>
                      )}
                      {canSubmit(listing) && (
                        <button
                          onClick={() => handleSubmit(listing.id)}
                          disabled={submitMutation.isPending}
                          title="Nộp duyệt"
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-50 hover:bg-amber-100 text-amber-700 text-xs font-semibold transition disabled:opacity-60"
                        >
                          <Send className="w-3.5 h-3.5" />
                          Nộp Duyệt
                        </button>
                      )}
                      {listing.status === 'PendingApproval' && (
                        <span className="inline-flex items-center gap-1 text-xs text-amber-600 font-medium">
                          <div className="w-1.5 h-1.5 bg-amber-400 rounded-full animate-pulse" />
                          Đang thẩm định
                        </span>
                      )}
                      {listing.status === 'Active' && (
                        <span className="inline-flex items-center gap-1 text-xs text-emerald-600 font-medium">
                          <div className="w-1.5 h-1.5 bg-emerald-400 rounded-full" />
                          Đang hiển thị
                        </span>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Modal Form */}
      {showForm && (
        <ListingForm
          editing={editingListing ?? undefined}
          categories={categories}
          onClose={() => { setShowForm(false); setEditingListing(null); }}
        />
      )}
    </>
  );
}
