import type { ReactNode } from "react";


const STATUS: Record<string, [string, string]> = {
  AwaitingDeposit: ["Chờ đặt cọc", "warn"], Pending: ["Chờ NCC duyệt", "warn"], Confirmed: ["Đã xác nhận", "ok"],
  Rejected: ["Bị từ chối", "bad"], Cancelled: ["Đã hủy", "bad"], Completed: ["Hoàn tất", "ok"], NoShow: ["Vắng mặt", "bad"], Expired: ["Hết hạn", "bad"],
  Submitted: ["Đã gửi", "info"], InProgress: ["Đang xử lý", "warn"], Sent: ["Đã gửi NCC", "info"], Accepted: ["NCC đã nhận", "warn"],
  Quoted: ["Đã báo giá", "warn"], Contracted: ["Đã ký HĐ", "ok"], Superseded: ["Đã thay thế", "info"],
  Open: ["Đang mở", "warn"], Closed: ["Đã chốt", "warn"], Paid: ["Đã thanh toán", "ok"], Overdue: ["Quá hạn", "bad"],
  Approved: ["Đã duyệt", "ok"], InReview: ["Đang xử lý", "warn"], ResolvedCustomer: ["Xử lý – có lợi cho khách", "ok"], ResolvedVendor: ["Xử lý – có lợi cho NCC", "ok"],
};
export function StatusPill({ status }: { status: string }) {
  const [label, tone] = STATUS[status] ?? [status, "info"];
  return <span className={`pill ${tone}`}>{label}</span>;
}

export function Panel({ title, eyebrow, action, children }: { title: string; eyebrow?: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="wd card p-5 md:p-6 mb-6">
      <div className="flex flex-wrap items-end justify-between gap-3 mb-4">
        <div>
          {eyebrow && <p className="eyebrow text-[var(--gold)]">{eyebrow}</p>}
          <h2 className="serif text-xl font-bold">{title}</h2>
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="text-sm muted py-6 text-center border border-dashed border-[var(--line)] rounded-2xl">{children}</p>;
}

export function ErrorText({ children }: { children?: ReactNode }) {
  return children ? <p role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-xl px-3 py-2">{children}</p> : null;
}
