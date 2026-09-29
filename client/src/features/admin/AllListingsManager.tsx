import { useState, useMemo } from 'react';
import {
  FileText, Search, Eye, EyeOff, Building2, MapPin,
  CheckCircle2, AlertCircle, Loader2, Tag
} from 'lucide-react';
import { useAdminListings, useUpdateListingStatus } from '../vendor-portal/hooks/useListings';
import type { ListingDto, ListingStatus } from '../../types/listing.types';

export default function AllListingsManager() {
  const [selectedStatus, setSelectedStatus] = useState<string>('Active'); // Mặc định hiển thị bài đã đăng (Active)
  const [search, setSearch] = useState('');
  const [selectedListing, setSelectedListing] = useState<ListingDto | null>(null);
  const [confirmToggleModal, setConfirmToggleModal] = useState<{ listing: ListingDto; newStatus: ListingStatus } | null>(null);

  const { data: listings = [], isLoading, error } = useAdminListings(selectedStatus === 'ALL' ? undefined : undefined);
  const updateStatusMutation = useUpdateListingStatus();

  // Thống kê nhanh
  const stats = useMemo(() => {
    return {
      total: listings.length,
      active: listings.filter(l => l.status === 'Active').length,
      hidden: listings.filter(l => l.status === 'Hidden').length,
      pending: listings.filter(l => l.status === 'PendingApproval').length,
      totalViews: listings.reduce((sum, l) => sum + (l.viewCount || 0), 0),
    };
  }, [listings]);

  // Bộ lọc
  const filteredListings = useMemo(() => {
    return listings.filter(item => {
      const matchSearch =
        item.title.toLowerCase().includes(search.toLowerCase()) ||
        item.vendorBrandName.toLowerCase().includes(search.toLowerCase()) ||
        item.categoryName.toLowerCase().includes(search.toLowerCase()) ||
        item.location.toLowerCase().includes(search.toLowerCase());

      const matchStatus = selectedStatus === 'ALL' || item.status === selectedStatus;

      return matchSearch && matchStatus;
    });
  }, [listings, search, selectedStatus]);

  const handleToggleStatus = async () => {
    if (!confirmToggleModal) return;
    try {
      await updateStatusMutation.mutateAsync({
        id: confirmToggleModal.listing.id,
        status: confirmToggleModal.newStatus,
      });
      setConfirmToggleModal(null);
      if (selectedListing?.id === confirmToggleModal.listing.id) {
        setSelectedListing({ ...selectedListing, status: confirmToggleModal.newStatus });
      }
    } catch {
      alert('Có lỗi xảy ra khi cập nhật trạng thái bài đăng.');
    }
  };

  const getStatusBadge = (status: ListingStatus) => {
    switch (status) {
      case 'Active':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
            Đang hiển thị
          </span>
        );
      case 'Hidden':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200">
            <EyeOff className="w-3 h-3 text-slate-500" />
            Đã ẩn
          </span>
        );
      case 'PendingApproval':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-50 text-amber-700 border border-amber-200">
            Chờ duyệt
          </span>
        );
      case 'Rejected':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-200">
            Từ chối
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-600 border border-slate-200">
            Bản nháp
          </span>
        );
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center py-20 bg-white rounded-2xl border border-slate-200">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-medium text-slate-600">Đang tải danh sách bài đăng...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 bg-red-50 rounded-2xl border border-red-200 text-center">
        <AlertCircle className="w-8 h-8 text-red-500 mx-auto mb-2" />
        <p className="font-semibold text-red-800">Không thể tải danh sách bài đăng</p>
        <p className="text-sm text-red-600 mt-1">Vui lòng kiểm tra quyền truy cập hoặc kết nối mạng.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Top Banner & Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Bài đang hiển thị</p>
            <p className="text-2xl font-bold text-emerald-600 mt-1">{stats.active}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-emerald-50 flex items-center justify-center text-emerald-600">
            <CheckCircle2 className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Bài đang ẩn (Tạm khóa)</p>
            <p className="text-2xl font-bold text-slate-700 mt-1">{stats.hidden}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-slate-100 flex items-center justify-center text-slate-600">
            <EyeOff className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Tổng bài trên hệ thống</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">{stats.total}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-rose-50 flex items-center justify-center text-rose-600">
            <FileText className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Lượt xem bài đăng</p>
            <p className="text-2xl font-bold text-sky-600 mt-1">{stats.totalViews.toLocaleString('vi-VN')}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-sky-50 flex items-center justify-center text-sky-600">
            <Eye className="w-5 h-5" />
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col md:flex-row items-center gap-3">
        <div className="relative flex-1 w-full">
          <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            placeholder="Tìm theo tên bài đăng, tên NCC, ngành hàng, khu vực..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-9 pr-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
          />
        </div>

        <div className="flex items-center gap-2 w-full md:w-auto">
          <select
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            className="px-3 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 text-slate-700"
          >
            <option value="Active">Đang hiển thị (Active)</option>
            <option value="Hidden">Đang ẩn (Hidden)</option>
            <option value="ALL">Tất cả trạng thái</option>
            <option value="PendingApproval">Chờ duyệt (Pending)</option>
            <option value="Draft">Bản nháp (Draft)</option>
            <option value="Rejected">Bị từ chối (Rejected)</option>
          </select>
        </div>
      </div>

      {/* Main Table */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 border-b border-slate-200 text-xs font-semibold text-slate-500 uppercase tracking-wider">
              <tr>
                <th className="px-6 py-3.5">Gói dịch vụ</th>
                <th className="px-6 py-3.5">Nhà cung cấp</th>
                <th className="px-6 py-3.5">Khoảng giá</th>
                <th className="px-6 py-3.5">Địa điểm</th>
                <th className="px-6 py-3.5">Trạng thái</th>
                <th className="px-6 py-3.5 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredListings.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-slate-500">
                    <FileText className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                    Không tìm thấy bài đăng nào phù hợp bộ lọc.
                  </td>
                </tr>
              ) : (
                filteredListings.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50/70 transition">
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 rounded-lg bg-slate-100 overflow-hidden flex-shrink-0 border border-slate-200 flex items-center justify-center">
                          {item.media && item.media.length > 0 ? (
                            <img
                              src={item.media[0].thumbnailUrl || item.media[0].mediaUrl}
                              alt={item.title}
                              className="w-full h-full object-cover"
                            />
                          ) : (
                            <Tag className="w-5 h-5 text-slate-400" />
                          )}
                        </div>
                        <div className="min-w-0 max-w-xs">
                          <p className="font-semibold text-slate-900 truncate" title={item.title}>
                            {item.title}
                          </p>
                          <span className="inline-block text-[11px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 font-medium mt-1">
                            {item.categoryName}
                          </span>
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-1.5 text-slate-700 font-medium">
                        <Building2 className="w-3.5 h-3.5 text-slate-400" />
                        <span className="truncate max-w-[150px]">{item.vendorBrandName}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      <p className="text-xs font-semibold text-slate-800">
                        {item.minPrice.toLocaleString('vi-VN')} đ
                      </p>
                      <p className="text-[11px] text-slate-400">
                        đến {item.maxPrice.toLocaleString('vi-VN')} đ
                      </p>
                    </td>
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-1 text-xs text-slate-600">
                        <MapPin className="w-3.5 h-3.5 text-slate-400" />
                        <span className="truncate max-w-[120px]">{item.location}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      {getStatusBadge(item.status)}
                    </td>
                    <td className="px-6 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          onClick={() => setSelectedListing(item)}
                          className="px-3 py-1.5 text-xs font-medium text-slate-700 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
                        >
                          Chi tiết
                        </button>

                        {item.status === 'Active' && (
                          <button
                            onClick={() => setConfirmToggleModal({ listing: item, newStatus: 'Hidden' })}
                            className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-medium text-amber-700 hover:text-amber-800 bg-amber-50 hover:bg-amber-100 border border-amber-200 rounded-lg transition"
                            title="Tạm ẩn bài đăng này khỏi sàn"
                          >
                            <EyeOff className="w-3.5 h-3.5" />
                            Ẩn bài
                          </button>
                        )}

                        {item.status === 'Hidden' && (
                          <button
                            onClick={() => setConfirmToggleModal({ listing: item, newStatus: 'Active' })}
                            className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-medium text-emerald-700 hover:text-emerald-800 bg-emerald-50 hover:bg-emerald-100 border border-emerald-200 rounded-lg transition"
                            title="Hiển thị lại bài đăng trên sàn"
                          >
                            <Eye className="w-3.5 h-3.5" />
                            Hiện bài
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Confirmation Modal */}
      {confirmToggleModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-xl border border-slate-100 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center gap-3 mb-4">
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
                confirmToggleModal.newStatus === 'Active' ? 'bg-emerald-100 text-emerald-600' : 'bg-amber-100 text-amber-600'
              }`}>
                {confirmToggleModal.newStatus === 'Active' ? <Eye className="w-5 h-5" /> : <EyeOff className="w-5 h-5" />}
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  {confirmToggleModal.newStatus === 'Active' ? 'Hiển thị bài đăng công khai' : 'Tạm ẩn bài đăng'}
                </h3>
                <p className="text-xs text-slate-500">Hành động của Quản trị viên</p>
              </div>
            </div>

            <p className="text-sm text-slate-600 mb-6">
              Bạn có chắc muốn {confirmToggleModal.newStatus === 'Active' ? 'hiển thị lại' : 'ẩn'} bài đăng{' '}
              <strong className="text-slate-900">"{confirmToggleModal.listing.title}"</strong> của{' '}
              <strong className="text-slate-900">{confirmToggleModal.listing.vendorBrandName}</strong>?
              {confirmToggleModal.newStatus === 'Hidden' && ' Khi bị ẩn, bài đăng sẽ không còn xuất hiện trong kết quả tìm kiếm của khách hàng.'}
            </p>

            <div className="flex items-center justify-end gap-3">
              <button
                onClick={() => setConfirmToggleModal(null)}
                className="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded-xl transition"
              >
                Hủy
              </button>
              <button
                onClick={handleToggleStatus}
                disabled={updateStatusMutation.isPending}
                className={`px-4 py-2 text-sm font-semibold text-white rounded-xl transition flex items-center gap-2 ${
                  confirmToggleModal.newStatus === 'Active'
                    ? 'bg-emerald-600 hover:bg-emerald-700'
                    : 'bg-amber-600 hover:bg-amber-700'
                }`}
              >
                {updateStatusMutation.isPending && <Loader2 className="w-4 h-4 animate-spin" />}
                {confirmToggleModal.newStatus === 'Active' ? 'Xác nhận Hiển thị' : 'Xác nhận Ẩn bài'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Listing Detail Modal */}
      {selectedListing && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-xl border border-slate-100 max-h-[90vh] overflow-y-auto animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-start justify-between mb-4 pb-4 border-b border-slate-100">
              <div>
                <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-rose-50 text-rose-600 border border-rose-100 mb-2 inline-block">
                  {selectedListing.categoryName}
                </span>
                <h3 className="text-xl font-bold text-slate-900 leading-snug">{selectedListing.title}</h3>
                <div className="flex items-center gap-3 mt-1.5 text-xs text-slate-500">
                  <span className="flex items-center gap-1 font-medium text-slate-700">
                    <Building2 className="w-3.5 h-3.5 text-slate-400" />
                    {selectedListing.vendorBrandName}
                  </span>
                  <span>•</span>
                  <span className="flex items-center gap-1">
                    <MapPin className="w-3.5 h-3.5 text-slate-400" />
                    {selectedListing.location}
                  </span>
                </div>
              </div>
              <button
                onClick={() => setSelectedListing(null)}
                className="text-slate-400 hover:text-slate-600 p-1 rounded-lg hover:bg-slate-100 transition"
              >
                ✕
              </button>
            </div>

            {/* Media Gallery */}
            {selectedListing.media && selectedListing.media.length > 0 && (
              <div className="mb-5">
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">Hình ảnh dịch vụ ({selectedListing.media.length})</p>
                <div className="grid grid-cols-3 gap-2">
                  {selectedListing.media.map((m, idx) => (
                    <div key={m.id || idx} className="h-28 rounded-lg overflow-hidden border border-slate-200 bg-slate-100">
                      <img src={m.mediaUrl} alt="" className="w-full h-full object-cover" />
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Info Cards */}
            <div className="grid grid-cols-3 gap-3 mb-5">
              <div className="bg-slate-50 p-3 rounded-xl border border-slate-100">
                <p className="text-xs text-slate-500 font-medium">Khoảng giá</p>
                <p className="text-sm font-bold text-rose-600 mt-0.5">
                  {selectedListing.minPrice.toLocaleString('vi-VN')} đ - {selectedListing.maxPrice.toLocaleString('vi-VN')} đ
                </p>
              </div>
              <div className="bg-slate-50 p-3 rounded-xl border border-slate-100">
                <p className="text-xs text-slate-500 font-medium">Trạng thái</p>
                <div className="mt-1">{getStatusBadge(selectedListing.status)}</div>
              </div>
              <div className="bg-slate-50 p-3 rounded-xl border border-slate-100">
                <p className="text-xs text-slate-500 font-medium">Lượt xem</p>
                <p className="text-sm font-bold text-slate-800 mt-0.5">
                  {selectedListing.viewCount || 0} lượt
                </p>
              </div>
            </div>

            {/* Description */}
            <div className="mb-5">
              <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">Mô tả chi tiết</p>
              <div className="bg-slate-50 p-4 rounded-xl text-sm text-slate-700 whitespace-pre-line border border-slate-100 leading-relaxed">
                {selectedListing.description || 'Chưa có mô tả.'}
              </div>
            </div>

            {/* Meta */}
            <div className="bg-slate-50 p-3 rounded-xl text-xs text-slate-500 space-y-1 mb-5">
              <p><strong>Mã bài đăng:</strong> <span className="font-mono">{selectedListing.id}</span></p>
              <p><strong>Ngày tạo:</strong> {new Date(selectedListing.createdAt).toLocaleString('vi-VN')}</p>
              {selectedListing.moderatedAt && (
                <p><strong>Thời gian kiểm duyệt:</strong> {new Date(selectedListing.moderatedAt).toLocaleString('vi-VN')}</p>
              )}
            </div>

            <div className="flex justify-end gap-2">
              <button
                onClick={() => setSelectedListing(null)}
                className="px-4 py-2 text-sm font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-xl transition"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
