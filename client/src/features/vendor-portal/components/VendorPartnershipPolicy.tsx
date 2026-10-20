import { ShieldCheck } from "lucide-react";
import { useVendorBusinessGroupPolicy } from "../hooks/useBusinessGroups";
import { commissionPercent, policyAcceptedTime } from "../businessGroupPolicy";
import { Empty, Panel } from "../../journey/ui";

export function VendorPartnershipPolicy() {
  const policy = useVendorBusinessGroupPolicy();
  if (policy.isPending) return <Panel title="Chính sách hợp tác"><p role="status" className="text-sm muted">Đang tải nhóm kinh doanh và chính sách của shop...</p></Panel>;
  if (policy.isError || !policy.data) return <Panel title="Chính sách hợp tác"><div role="alert" className="text-sm text-slate-600"><p>Chưa tải được chính sách hợp tác. Thông tin cũ của shop vẫn được giữ nguyên.</p><button type="button" className="btn btn-outline mt-3" onClick={() => void policy.refetch()}>Thử lại</button></div></Panel>;
  const data = policy.data;
  return <div className="wd">
    <Panel title="Chính sách hợp tác" eyebrow="Nhóm kinh doanh của shop">
      <p className="text-sm muted leading-relaxed mb-4">Thông tin dưới đây lấy từ chính sách và nhóm kinh doanh đã lưu. Danh mục của từng dịch vụ xác định chính sách tương ứng. Các tỷ lệ không phải là giảm giá dành cho khách hàng.</p>
      {data.usesLegacyCommissionRate && <section className="mb-5 rounded-xl border border-amber-200 bg-amber-50 p-4" aria-label="Chính sách tài khoản cũ">
        <h3 className="font-semibold text-sm text-amber-900">Chính sách cũ được giữ nguyên</h3>
        <p className="text-sm text-amber-900 mt-2">Shop được tạo trước cơ chế đăng ký nhiều nhóm. {data.legacyCommissionRate != null ? `Tỷ lệ cũ đang được lưu: ${commissionPercent(data.legacyCommissionRate)}.` : "Chưa có thông tin tỷ lệ cũ để hiển thị."}</p>
        <p className="text-xs leading-relaxed text-amber-800 mt-2">Chưa xác nhận lại theo chính sách nhóm mới. Hệ thống không tự áp lại mức mặc định, không sửa hợp đồng hay giao dịch đã chốt. Hãy liên hệ quản trị viên để rà soát nhóm kinh doanh phù hợp.</p>
      </section>}
      {data.groups.length === 0 ? <Empty>Chưa có nhóm kinh doanh được ghi nhận. Không có nhóm hoặc mức hoa hồng nào được tự suy đoán từ dữ liệu cũ.</Empty> : <div className="grid gap-3 sm:grid-cols-2">{data.groups.map(group => <section key={group.id} className="rounded-2xl border border-[var(--line)] bg-white p-4">
        <div className="flex items-start justify-between gap-3"><h3 className="text-sm font-semibold leading-relaxed">{group.name}</h3><ShieldCheck size={18} className="shrink-0 text-slate-500" aria-hidden="true" /></div>
        <p className="text-xs leading-relaxed muted mt-2">{group.description}</p>
        <dl className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-[#f5f2e8] p-3 text-sm"><dt>Mặc định hiện hành theo nhóm</dt><dd className="font-semibold text-[#25334c]">{commissionPercent(group.defaultCommissionRate)}</dd></dl>
        {group.requiresCommissionBasisApproval && <p className="mt-3 text-xs leading-relaxed text-slate-600">Dịch vụ A–Z cần cơ sở tính phí tổ chức/quản lý/điều phối được xác định và phê duyệt. Không mặc định tính trên toàn bộ ngân sách cưới, khoản chi hộ hoặc chi phí của bên thứ ba.</p>}
        <p className="mt-3 text-xs muted">Phiên bản chính sách nhóm: {group.policyVersion}</p>
      </section>)}</div>}
      <div className="mt-5 border-t border-[var(--line)] pt-4 text-xs leading-relaxed muted">
        {data.policyAcceptedAt ? <p>Đã ghi nhận xác nhận chính sách{data.policyVersion ? ` ${data.policyVersion}` : ""} lúc {policyAcceptedTime(data.policyAcceptedAt)}.</p> : <p>Chưa ghi nhận thời điểm xác nhận chính sách nhóm mới.</p>}
        <p className="mt-2">Đây là khu vực chỉ xem. Những thỏa thuận riêng và thay đổi có hiệu lực cần được quản trị viên phê duyệt; không thể tự chỉnh tỷ lệ tại đây. Tỷ lệ đã lưu trên giao dịch cũ không bị thay đổi.</p>
      </div>
    </Panel>
  </div>;
}
