import { useState, useMemo } from 'react';
import {
  FileText, Search, Eye, EyeOff, Building2, MapPin,
  CheckCircle2, AlertCircle, Loader2, Tag, Zap, Pencil
} from 'lucide-react';
import { useAdminListings, useUpdateListingStatus } from '../vendor-portal/hooks/useListings';
import ListingForm from '../vendor-portal/components/ListingForm';
import { usePublicCategories } from '../customer/hooks/useBrowse';
import { useAutoApproveAll } from './useModerationQueue';
import { useAuthStore } from '../../store/authStore';
import { moderationActor, sameModerationActor, type BulkModerationProposal } from './bulkModerationModel';
import { BulkModerationConfirmationDialog } from './BulkModerationConfirmationDialog';
import type { ListingDto, ListingStatus } from '../../types/listing.types';
import { Pagination } from '../../components/Pagination';
import { paginationWindow } from '../../components/paginationModel';

export default function AllListingsManager() {
  const [selectedStatus, setSelectedStatus] = useState<string>('Active'); // Mặc định hiển thị bài đã đăng (Active)
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedListing, setSelectedListing] = useState<ListingDto | null>(null);
  const [confirmToggleModal, setConfirmToggleModal] = useState<{ listing: ListingDto; newStatus: ListingStatus } | null>(null);
  const [editingListing, setEditingListing] = useState<ListingDto | null>(null);
  const { data: publicCategories = [] } = usePublicCategories();

  const { data: listings = [], isLoading, error } = useAdminListings();
  const updateStatusMutation = useUpdateListingStatus();
  const autoApproveMutation = useAutoApproveAll();
  const [bulkNotice, setBulkNotice] = useState<string | null>(null);
  const [bulkApproval, setBulkApproval] = useState<BulkModerationProposal | null>(null);
  const [bulkError, setBulkError] = useState('');

  const handleAutoApproveListings = async () => {
    if (!bulkApproval || autoApproveMutation.isPending) return;
    const proposal = bulkApproval;
    if (!sameModerationActor(proposal.actor, useAuthStore.getState())) { setBulkError('Phiên kiểm duyệt đã thay đổi. Hủy và xác nhận lại.'); return; }
    try {
      const res = await autoApproveMutation.mutateAsync({ type: 'listing', actor: proposal.actor });
      if (!sameModerationActor(proposal.actor, useAuthStore.getState())) return;
      setBulkApproval(null);
      setBulkNotice(`Đã duyệt ${res.approvedListings}/${res.attempted} bài đăng trong lượt này, bỏ qua ${res.skipped}.${res.hasMore ? ' Còn bài chờ duyệt; bạn có thể xác nhận lượt tiếp theo.' : ''}`);
    } catch (error) {
      if (sameModerationActor(proposal.actor, useAuthStore.getState())) setBulkError('Lượt duyệt đã dừng; một số bài có thể đã được duyệt. Tải lại danh sách trước khi thử lại. ' + (error instanceof Error ? error.message : ''));
    }
  };

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

  const { current: currentPage } = paginationWindow(page, pageSize, filteredListings.length);
  const pagedListings = filteredListings.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  // A status change or refreshed list may remove the final page.
  if (page !== currentPage) setPage(currentPage);

  const listingActions = (item: ListingDto) => (
    <div className="flex min-w-0 flex-wrap items-center gap-2 @min-[1000px]:justify-end">
      <button type="button" onClick={() => setEditingListing(item)}
        className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-semibold text-rose-700 transition hover:bg-rose-100 hover:text-rose-800"
        title="Chỉnh sửa thông tin bài đăng">
        <Pencil size={12} /> Sửa bài
      </button>
      <button type="button" onClick={() => setSelectedListing(item)}
        className="shrink-0 whitespace-nowrap rounded-lg bg-slate-100 px-3 py-2 text-xs font-medium text-slate-700 transition hover:bg-slate-200 hover:text-slate-900">
        Chi tiết
      </button>
      {item.status === 'Active' && (
        <button type="button" onClick={() => setConfirmToggleModal({ listing: item, newStatus: 'Hidden' })}
          className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-medium text-amber-700 transition hover:bg-amber-100 hover:text-amber-800"
          title="Tạm ẩn bài đăng này khỏi sàn">
          <EyeOff className="h-3.5 w-3.5" /> Ẩn bài
        </button>
      )}
      {item.status === 'Hidden' && (
        <button type="button" onClick={() => setConfirmToggleModal({ listing: item, newStatus: 'Active' })}
          className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-medium text-emerald-700 transition hover:bg-emerald-100 hover:text-emerald-800"
          title="Hiển thị lại bài đăng trên sàn">
          <Eye className="h-3.5 w-3.5" /> Hiện bài
        </button>
      )}
    </div>
  );

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
          <span className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
            Đang hiển thị
          </span>
        );
      case 'Hidden':
        return (
          <span className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200">
            <EyeOff className="w-3 h-3 text-slate-500" />
            Đã ẩn
          </span>
        );
      case 'PendingApproval':
        return (
          <span className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-50 text-amber-700 border border-amber-200">
            Đang chờ duyệt
          </span>
        );
      case 'Rejected':
        return (
          <span className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-200">
            Từ chối
          </span>
        );
      default:
        return (
          <span className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-600 border border-slate-200">
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
    <div className="@container w-full min-w-0 max-w-full space-y-6">
      {/* Top Banner & Stats */}
      <div className="grid min-w-0 grid-cols-1 gap-3 @min-[420px]:grid-cols-2 @min-[900px]:grid-cols-4">
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

      {bulkNotice && (
        <div className="mb-4 flex items-center gap-2 p-3.5 rounded-xl border border-emerald-200 bg-emerald-50 text-emerald-800 text-sm font-medium">
          <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
          <span>{bulkNotice}</span>
        </div>
      )}
      {stats.pending > 0 && (
        <div className="mb-4 flex min-w-0 flex-wrap items-center justify-between gap-3 p-3.5 rounded-xl border border-amber-200 bg-amber-50">
          <div className="flex items-center gap-2">
            <AlertCircle className="w-5 h-5 text-amber-600 shrink-0" />
            <span className="text-sm font-medium text-amber-900">
              Có <b>{stats.pending}</b> bài đăng dịch vụ đang chờ duyệt trên hệ thống.
            </span>
          </div>
          <button
            type="button"
            disabled={autoApproveMutation.isPending}
            onClick={() => {
              if (autoApproveMutation.isPending) return;
              const actor = moderationActor(useAuthStore.getState());
              if (actor) { setBulkError(''); setBulkApproval({ mode: 'once', type: 'listing', actor }); }
            }}
            className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-amber-500 text-white text-xs font-semibold hover:bg-amber-600 transition disabled:opacity-50 shadow-sm"
          >
            <Zap size={14} className="fill-white" />
            {autoApproveMutation.isPending ? "Đang duyệt..." : `Duyệt một lượt (${stats.pending})`}
          </button>
        </div>
      )}
      {bulkApproval && <BulkModerationConfirmationDialog proposal={bulkApproval} busy={autoApproveMutation.isPending} error={bulkError}
        onCancel={() => { if (!autoApproveMutation.isPending) { setBulkApproval(null); setBulkError(''); } }} onConfirm={() => { void handleAutoApproveListings(); }} />}
      {/* Filter and Search Bar */}
      <div className="flex min-w-0 flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="relative w-full min-w-0 flex-1 basis-64">
          <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            placeholder="Tìm theo tên bài đăng, tên NCC, ngành hàng, khu vực..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full pl-9 pr-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
          />
        </div>

        <div className="w-full min-w-0 @min-[600px]:w-auto">
          <select
            value={selectedStatus}
            onChange={(e) => { setSelectedStatus(e.target.value); setPage(1); }}
            aria-label="Lọc trạng thái dịch vụ"
            className="w-full max-w-full px-3 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 text-slate-700"
          >
            <option value="Active">Đang hiển thị (Active)</option>
            <option value="Hidden">Đang ẩn (Hidden)</option>
            <option value="ALL">Tất cả trạng thái</option>
            <option value="PendingApproval">Đang chờ duyệt (Pending)</option>
            <option value="Draft">Bản nháp (Draft)</option>
            <option value="Rejected">Bị từ chối (Rejected)</option>
          </select>
        </div>
      </div>

      {/* Rows become cards when the actual admin panel is narrow. */}
      <section className="min-w-0 max-w-full rounded-xl border border-slate-200 bg-white shadow-sm" aria-label="Danh sách dịch vụ">
        <div className="flex min-w-0 flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
          <p className="text-sm text-slate-600" role="status">{filteredListings.length} dịch vụ phù hợp</p>
          <label className="flex items-center gap-2 text-xs font-medium text-slate-600">
            Mỗi trang
            <select value={pageSize} onChange={event => { setPageSize(Number(event.target.value)); setPage(1); }}
              className="rounded-lg border border-slate-200 bg-slate-50 px-2 py-1.5 text-sm" aria-label="Số dịch vụ mỗi trang">
              <option value={10}>10</option><option value={20}>20</option><option value={50}>50</option>
            </select>
          </label>
        </div>
        <table className="block w-full max-w-full text-left text-sm @min-[1000px]:table @min-[1000px]:table-fixed">
          <colgroup className="hidden @min-[1000px]:table-column-group">
            <col className="w-[43%]" /><col className="w-[18%]" /><col className="w-[16%]" /><col className="w-[23%]" />
          </colgroup>
          <thead className="hidden border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500 @min-[1000px]:table-header-group">
            <tr><th className="px-4 py-3">Dịch vụ / Nhà cung cấp</th><th className="px-4 py-3">Khoảng giá</th><th className="px-4 py-3">Trạng thái</th><th className="px-4 py-3 text-right">Thao tác</th></tr>
          </thead>
          <tbody className="block divide-y divide-slate-100 @min-[1000px]:table-row-group">
            {pagedListings.length === 0 ? (
              <tr className="block @min-[1000px]:table-row"><td colSpan={4} className="block px-4 py-12 text-center text-slate-500 @min-[1000px]:table-cell">
                <FileText className="mx-auto mb-2 h-8 w-8 text-slate-300" />Không tìm thấy bài đăng nào phù hợp bộ lọc.
              </td></tr>
            ) : pagedListings.map(item => (
              <tr key={item.id} className="grid min-w-0 grid-cols-1 gap-3 p-4 transition hover:bg-slate-50/70 @min-[520px]:grid-cols-2 @min-[1000px]:table-row @min-[1000px]:p-0">
                <td className="block min-w-0 @min-[520px]:col-span-2 @min-[1000px]:table-cell @min-[1000px]:px-4 @min-[1000px]:py-4 @min-[1000px]:align-top">
                  <div className="flex min-w-0 items-start gap-3">
                    <div className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-slate-200 bg-slate-100">
                      {item.media?.length ? <img src={item.media[0].thumbnailUrl || item.media[0].mediaUrl} alt="" className="h-full w-full object-cover" loading="lazy" /> : <Tag className="h-5 w-5 text-slate-400" />}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="break-words font-semibold text-slate-900 [overflow-wrap:anywhere]">{item.title}</p>
                      <span className="mt-1 inline-block max-w-full break-words rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600">{item.categoryName}</span>
                      <p className="mt-1 flex min-w-0 items-start gap-1 text-xs text-slate-700"><Building2 className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" /><span className="min-w-0 break-words [overflow-wrap:anywhere]">{item.vendorBrandName}</span></p>
                      <p className="mt-1 flex min-w-0 items-start gap-1 text-xs text-slate-500"><MapPin className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" /><span className="min-w-0 break-words [overflow-wrap:anywhere]">{item.location}</span></p>
                    </div>
                  </div>
                </td>
                <td className="block min-w-0 @min-[1000px]:table-cell @min-[1000px]:px-4 @min-[1000px]:py-4 @min-[1000px]:align-top">
                  <p className="mb-1 text-[11px] font-medium text-slate-500 @min-[1000px]:hidden">Khoảng giá</p>
                  <p className="break-words text-xs font-semibold text-slate-800">{item.minPrice.toLocaleString('vi-VN')} đ</p>
                  <p className="break-words text-[11px] text-slate-500">đến {item.maxPrice.toLocaleString('vi-VN')} đ</p>
                </td>
                <td className="block min-w-0 @min-[1000px]:table-cell @min-[1000px]:px-4 @min-[1000px]:py-4 @min-[1000px]:align-top">
                  <p className="mb-1 text-[11px] font-medium text-slate-500 @min-[1000px]:hidden">Trạng thái</p>
                  {getStatusBadge(item.status)}
                </td>
                <td className="block min-w-0 @min-[520px]:col-span-2 @min-[1000px]:table-cell @min-[1000px]:px-4 @min-[1000px]:py-4 @min-[1000px]:align-top">{listingActions(item)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="min-w-0 border-t border-slate-100 px-4 py-3">
          <Pagination page={currentPage} pageSize={pageSize} totalCount={filteredListings.length} onPageChange={setPage} label="Phân trang dịch vụ quản trị" />
        </div>
      </section>

      {/* Confirmation Modal */}
      {confirmToggleModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/50 p-3 sm:p-4">
          <div className="min-w-0 w-full max-w-md max-h-[calc(100dvh-1.5rem)] overflow-y-auto rounded-2xl border border-slate-100 bg-white p-4 shadow-xl sm:p-6 animate-in fade-in zoom-in-95 duration-150">
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

            <div className="flex flex-wrap items-center justify-end gap-3">
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

      {editingListing && (
        <ListingForm
          editing={editingListing}
          categories={publicCategories}
          onClose={() => setEditingListing(null)}
        />
      )}

      {/* Listing Detail Modal */}
      {selectedListing && (
        <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/50 p-3 sm:p-4">
          <div className="min-w-0 w-full max-w-2xl max-h-[calc(100dvh-1.5rem)] overflow-y-auto rounded-2xl border border-slate-100 bg-white p-4 shadow-xl sm:p-6 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex min-w-0 items-start justify-between gap-3 mb-4 pb-4 border-b border-slate-100">
              <div className="min-w-0 flex-1">
                <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-rose-50 text-rose-600 border border-rose-100 mb-2 inline-block">
                  {selectedListing.categoryName}
                </span>
                <h3 className="break-words text-xl font-bold text-slate-900 leading-snug [overflow-wrap:anywhere]">{selectedListing.title}</h3>
                <div className="flex min-w-0 flex-wrap items-center gap-3 mt-1.5 text-xs text-slate-500 [overflow-wrap:anywhere]">
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
                type="button"
                aria-label="Đóng chi tiết dịch vụ"
                onClick={() => setSelectedListing(null)}
                className="shrink-0 text-slate-400 hover:text-slate-600 p-1 rounded-lg hover:bg-slate-100 transition"
              >
                ✕
              </button>
            </div>

            {/* Media Gallery */}
            {selectedListing.media && selectedListing.media.length > 0 && (
              <div className="mb-5">
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">Hình ảnh dịch vụ ({selectedListing.media.length})</p>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                  {selectedListing.media.map((m, idx) => (
                    <div key={m.id || idx} className="h-28 rounded-lg overflow-hidden border border-slate-200 bg-slate-100">
                      <img src={m.mediaUrl} alt="" className="w-full h-full object-cover" />
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Info Cards */}
            <div className="grid min-w-0 grid-cols-1 gap-3 mb-5 sm:grid-cols-3">
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
              <div className="break-words bg-slate-50 p-4 rounded-xl text-sm text-slate-700 whitespace-pre-line border border-slate-100 leading-relaxed [overflow-wrap:anywhere]">
                {selectedListing.description || 'Chưa có mô tả.'}
              </div>
            </div>

            {/* Meta */}
            <div className="break-words bg-slate-50 p-3 rounded-xl text-xs text-slate-500 space-y-1 mb-5 [overflow-wrap:anywhere]">
              <p><strong>Mã bài đăng:</strong> <span className="font-mono">{selectedListing.id}</span></p>
              <p><strong>Ngày tạo:</strong> {new Date(selectedListing.createdAt).toLocaleString('vi-VN')}</p>
              {selectedListing.moderatedAt && (
                <p><strong>Thời gian kiểm duyệt:</strong> {new Date(selectedListing.moderatedAt).toLocaleString('vi-VN')}</p>
              )}
            </div>

            <div className="flex flex-wrap justify-end gap-2">
              <button
                onClick={() => {
                  const toEdit = selectedListing;
                  setSelectedListing(null);
                  setEditingListing(toEdit);
                }}
                className="px-4 py-2 text-sm font-semibold text-white bg-rose-600 hover:bg-rose-700 rounded-xl transition inline-flex items-center gap-1.5 shadow-sm"
              >
                <Pencil size={14} />
                Sửa bài đăng
              </button>
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
