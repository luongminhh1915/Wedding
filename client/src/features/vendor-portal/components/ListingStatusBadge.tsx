import type { ListingStatus } from '../../../types/listing.types';

const statusConfig: Record<ListingStatus, { label: string; className: string }> = {
  Draft: {
    label: 'Nháp',
    className: 'bg-slate-100 text-slate-600 border border-slate-200',
  },
  PendingApproval: {
    label: 'Chờ Duyệt',
    className: 'bg-amber-50 text-amber-700 border border-amber-200',
  },
  Active: {
    label: 'Đang Hiển Thị',
    className: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  },
  Rejected: {
    label: 'Bị Từ Chối',
    className: 'bg-red-50 text-red-700 border border-red-200',
  },
  Hidden: {
    label: 'Đã Ẩn',
    className: 'bg-gray-100 text-gray-500 border border-gray-200',
  },
};

interface Props {
  status: ListingStatus;
}

export default function ListingStatusBadge({ status }: Props) {
  const config = statusConfig[status] ?? { label: status, className: 'bg-gray-100 text-gray-500' };
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ${config.className}`}>
      {config.label}
    </span>
  );
}
