import { type ReactNode } from "react";
import { LoaderCircle } from "lucide-react";
import { ComboPhoto } from "../combo/ComboPhoto";
import { ComboDialog } from "../combo/ComboDialog";
import "../../../styles/customer-orders.css";

export type OrderPhotoSources = (string | null | undefined)[];
export function OrderPhoto({
  sources,
  alt,
  className = "",
}: {
  sources?: OrderPhotoSources;
  alt: string;
  className?: string;
}) {
  return (
    <ComboPhoto
      sources={sources ?? []}
      alt={alt}
      className={`co-photo ${className}`}
    />
  );
}
const STATUSES: Record<string, { label: string; tone: string }> = {
  Draft: { label: "Bản nháp", tone: "neutral" },
  Submitted: { label: "Chờ NCC tiếp nhận", tone: "waiting" },
  InProgress: { label: "Đang trao đổi", tone: "waiting" },
  PartiallyQuoted: { label: "Đã có báo giá", tone: "waiting" },
  Quoted: { label: "Chờ bạn duyệt báo giá", tone: "waiting" },
  PartiallyContracted: { label: "Đang xác nhận hợp đồng", tone: "active" },
  Contracted: { label: "Đã ký hợp đồng", tone: "active" },
  Completed: { label: "Hoàn tất", tone: "success" },
  Cancelled: { label: "Đã rút / hủy", tone: "muted" },
  Expired: { label: "Hết hạn", tone: "muted" },
  Archived: { label: "Đã lưu trữ", tone: "muted" },
  New: { label: "Chờ tiếp nhận", tone: "waiting" },
  Accepted: { label: "Đã tiếp nhận", tone: "active" },
  Contacted: { label: "Đang tư vấn", tone: "active" },
  Sent: { label: "Chờ phản hồi", tone: "waiting" },
  Confirmed: { label: "Đã xác nhận", tone: "success" },
  PendingVerification: { label: "Chờ bạn xác nhận", tone: "waiting" },
  Rejected: { label: "Đã từ chối", tone: "muted" },
  Disputed: { label: "Đang xử lý khiếu nại", tone: "waiting" },
  Superseded: { label: "Đã thay thế", tone: "muted" },
  PendingVendor: { label: "Chờ NCC phản hồi", tone: "waiting" },
  PendingFinance: { label: "Chờ đối chiếu tài chính", tone: "waiting" },
  Acknowledged: { label: "Đã ghi nhận", tone: "active" },
};
export function OrderStatus({
  status,
  label,
}: {
  status: string;
  label?: string;
}) {
  const value = STATUSES[status];
  return (
    <span className={`co-status is-${value?.tone ?? "neutral"}`}>
      {label ?? value?.label ?? status}
    </span>
  );
}
export function OrderCard({
  imageSources,
  title,
  eyebrow,
  status,
  subtitle,
  meta,
  children,
  footer,
  onImageClick,
  className = "",
}: {
  imageSources?: OrderPhotoSources;
  title: ReactNode;
  eyebrow?: ReactNode;
  status?: ReactNode;
  subtitle?: ReactNode;
  meta?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  onImageClick?: () => void;
  className?: string;
}) {
  return (
    <article className={`co-card ${className}`}>
      <header className="co-card-header">
        {imageSources &&
          (onImageClick ? (
            <button
              className="co-card-cover"
              type="button"
              onClick={onImageClick}
              aria-label="Xem hình ảnh chi tiết"
            >
              <OrderPhoto
                sources={imageSources}
                alt={typeof title === "string" ? title : "Ảnh nhà cung cấp"}
              />
            </button>
          ) : (
            <div className="co-card-cover">
              <OrderPhoto
                sources={imageSources}
                alt={typeof title === "string" ? title : "Ảnh nhà cung cấp"}
              />
            </div>
          ))}
        <div className="co-card-identity">
          {eyebrow && <p className="co-eyebrow">{eyebrow}</p>}
          <h3>{title}</h3>
          {subtitle && <div className="co-card-subtitle">{subtitle}</div>}
          {meta && <div className="co-card-meta">{meta}</div>}
        </div>
        {status && (
          <div className="co-card-status">
            {typeof status === "string" ? (
              <OrderStatus status={status} />
            ) : (
              status
            )}
          </div>
        )}
      </header>
      <div className="co-card-body">{children}</div>
      {footer && <footer className="co-card-footer">{footer}</footer>}
    </article>
  );
}
export function OrderServiceRow({
  photoSources,
  title,
  subtitle,
  amount,
  children,
  onPhotoClick,
  className = "",
}: {
  photoSources?: OrderPhotoSources;
  title: ReactNode;
  subtitle?: ReactNode;
  amount?: ReactNode;
  children?: ReactNode;
  onPhotoClick?: () => void;
  className?: string;
}) {
  const photo = (
    <OrderPhoto
      sources={photoSources}
      alt={typeof title === "string" ? title : "Hình dịch vụ"}
    />
  );
  return (
    <div className={`co-service-row ${className}`}>
      <div className="co-service-main">
        {onPhotoClick ? (
          <button
            type="button"
            className="co-service-photo"
            onClick={onPhotoClick}
            aria-label="Mở ảnh dịch vụ"
          >
            {photo}
          </button>
        ) : (
          <div className="co-service-photo">{photo}</div>
        )}
        <div className="co-service-info">
          <h4>{title}</h4>
          {subtitle && <div className="co-service-subtitle">{subtitle}</div>}
        </div>
        {amount != null && <div className="co-service-amount">{amount}</div>}
      </div>
      {children && <div className="co-service-content">{children}</div>}
    </div>
  );
}
export interface OrderFilterOption {
  id: string;
  label: string;
  count?: number;
}
export function OrderFilterBar({
  value,
  onChange,
  options,
  label = "Lọc danh sách",
}: {
  value: string;
  onChange: (value: string) => void;
  options: OrderFilterOption[];
  label?: string;
}) {
  return (
    <nav className="co-filter-bar" aria-label={label}>
      {options.map((option) => (
        <button
          type="button"
          key={option.id}
          onClick={() => onChange(option.id)}
          className={value === option.id ? "is-active" : ""}
          aria-pressed={value === option.id}
        >
          {option.label}
          {option.count != null && <span>{option.count}</span>}
        </button>
      ))}
    </nav>
  );
}
export function OrderActionDialog({
  title,
  description,
  confirmLabel,
  onConfirm,
  onClose,
  busy = false,
  confirmDisabled = false,
  danger = false,
  error,
  children,
}: {
  title: string;
  description?: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  onClose: () => void;
  busy?: boolean;
  confirmDisabled?: boolean;
  danger?: boolean;
  error?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <ComboDialog
      title={title}
      onClose={() => {
        if (!busy) onClose();
      }}
    >
      <div className="co-action-dialog">
        {description && <div className="co-dialog-copy">{description}</div>}
        {children}
        {error && (
          <p className="co-error" role="alert">
            {error}
          </p>
        )}
        <div className="co-dialog-actions">
          <button
            type="button"
            className="co-button secondary"
            disabled={busy}
            onClick={onClose}
          >
            Quay lại
          </button>
          <button
            type="button"
            className={`co-button ${danger ? "danger" : "primary"}`}
            disabled={busy || confirmDisabled}
            onClick={onConfirm}
          >
            {busy && <LoaderCircle size={14} className="animate-spin" />}
            {busy ? "Đang xử lý..." : confirmLabel}
          </button>
        </div>
      </div>
    </ComboDialog>
  );
}
