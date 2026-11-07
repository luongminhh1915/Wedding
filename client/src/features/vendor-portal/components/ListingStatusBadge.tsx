import { Clock, CheckCircle2, XCircle, EyeOff, FileEdit } from 'lucide-react';
import type { ListingStatus } from '../../../types/listing.types';

const statusConfig: Record<ListingStatus, { label: string; className: string; icon: React.ReactNode }> = {
  Draft: {
    label: 'Bản nháp',
    className: 'bg-slate-100 text-slate-700 border border-slate-300 font-medium',
    icon: <FileEdit size={12} className="mr-1 text-slate-500 shrink-0" />,
  },
  PendingApproval: {
    label: 'Đang chờ duyệt',
    className: 'bg-amber-50 text-amber-800 border border-amber-300 font-medium',
    icon: <Clock size={12} className="mr-1 text-amber-600 shrink-0" />,
  },
  Active: {
    label: 'Đã duyệt',
    className: 'bg-emerald-50 text-emerald-700 border border-emerald-200 font-medium',
    icon: <CheckCircle2 size={12} className="mr-1 text-emerald-600 shrink-0" />,
  },
  Rejected: {
    label: 'Bị từ chối',
    className: 'bg-red-50 text-red-700 border border-red-200 font-medium',
    icon: <XCircle size={12} className="mr-1 text-red-600 shrink-0" />,
  },
  Hidden: {
    label: 'Đã ẩn',
    className: 'bg-gray-100 text-gray-500 border border-gray-200 font-medium',
    icon: <EyeOff size={12} className="mr-1 text-gray-400 shrink-0" />,
  },
};

interface Props {
  status: ListingStatus;
}

export default function ListingStatusBadge({ status }: Props) {
  const config = statusConfig[status] ?? {
    label: status,
    className: 'bg-gray-100 text-gray-500',
    icon: null,
  };
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ${config.className}`}>
      {config.icon}
      {config.label}
    </span>
  );
}
