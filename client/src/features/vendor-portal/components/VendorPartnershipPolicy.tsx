import { ShieldCheck } from "lucide-react";
import { useBusinessGroups, useVendorBusinessGroupPolicy } from "../hooks/useBusinessGroups";
import { commissionPercent, policyAcceptedTime } from "../businessGroupPolicy";
import { Empty, Panel } from "../../journey/ui";

export function VendorPartnershipPolicy() {
  const policy = useVendorBusinessGroupPolicy();
  const references = useBusinessGroups();
  const data = !policy.isPending && !policy.isError ? policy.data : undefined;
  return <div className="wd">
    <Panel title="Chính sách hợp tác" eyebrow="Nhóm hoa hồng theo loại dịch vụ">
      <p className="text-sm muted leading-relaxed mb-4">Bạn không cần chọn nhóm khi đăng ký hoặc chờ Admin cấp nhóm. Khi tạo dịch vụ, hãy tự chọn loại dịch vụ đang hoạt động; form sẽ hiển thị nhóm hoa hồng và mức mặc định tương ứng từ nền tảng. Đây là hoa hồng nền tảng, không phải giảm giá dành cho khách hàng.</p>
      {policy.isPending && <p role="status" className="mb-5 text-sm muted">Đang kiểm tra chính sách riêng và thông tin xác nhận đã lưu của shop...</p>}
      {(policy.isError || (!policy.isPending && !policy.data)) && <div role="alert" className="mb-5 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
        <p>Chưa tải được chính sách riêng của shop. Các mức tham khảo bên dưới không tự thay thế tỷ lệ cũ hoặc thỏa thuận riêng.</p>
        <button type="button" className="btn btn-outline mt-3" disabled={policy.isFetching} onClick={() => void policy.refetch()}>{policy.isFetching ? "Đang thử lại..." : "Thử lại chính sách của shop"}</button>
      </div>}
      {data?.usesLegacyCommissionRate && <section className="mb-5 rounded-xl border border-amber-200 bg-amber-50 p-4" aria-label="Chính sách tài khoản cũ">
        <h3 className="font-semibold text-sm text-amber-900">Chính sách cũ được giữ nguyên</h3>
        <p className="text-sm text-amber-900 mt-2">{data.legacyCommissionRate != null ? `Tỷ lệ cũ đang được lưu: ${commissionPercent(data.legacyCommissionRate)}.` : "Chưa có thông tin tỷ lệ cũ để hiển thị."}</p>
        <p className="text-xs leading-relaxed text-amber-800 mt-2">Chọn loại dịch vụ không tự chuyển tỷ lệ này sang mức mặc định. Chỉ thay đổi khi có xác nhận chuyển đổi hợp lệ; hợp đồng và giao dịch đã chốt không bị tính lại.</p>
      </section>}
      <h3 className="mb-3 text-sm font-semibold">Các nhóm hoa hồng tham khảo đang hoạt động</h3>
      {references.isPending ? <p role="status" className="text-sm muted">Đang tải các nhóm hoa hồng tham khảo...</p> : references.isError || !references.data ? <div role="alert" className="rounded-xl border border-red-200 p-4 text-sm">
        <p>Chưa tải được nhóm hoa hồng tham khảo. Hệ thống không tự tạo nhóm hoặc tỷ lệ thay thế.</p>
        <button type="button" className="btn btn-outline mt-3" disabled={references.isFetching} onClick={() => void references.refetch()}>{references.isFetching ? "Đang thử lại..." : "Thử lại nhóm tham khảo"}</button>
      </div> : references.data.length === 0 ? <Empty>Chưa có nhóm hoa hồng đang hoạt động được cấu hình. Không có mức hoa hồng nào được tự giả định.</Empty> : <div className="grid gap-3 sm:grid-cols-2">{references.data.map(group => <section key={group.id} className="rounded-2xl border border-[var(--line)] bg-white p-4">
        <div className="flex items-start justify-between gap-3"><h3 className="text-sm font-semibold leading-relaxed">{group.name}</h3><ShieldCheck size={18} className="shrink-0 text-slate-500" aria-hidden="true" /></div>
        <p className="text-xs leading-relaxed muted mt-2">{group.description}</p>
        <dl className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-[#fcf6ea] p-3 text-sm"><dt>Mức mặc định tham khảo của nhóm</dt><dd className="font-semibold text-[#233a42]">{commissionPercent(group.defaultCommissionRate)}</dd></dl>
        {group.requiresCommissionBasisApproval && <p className="mt-3 text-xs leading-relaxed text-slate-600">Dịch vụ A–Z cần cơ sở tính phí tổ chức/quản lý/điều phối được xác định và phê duyệt. Không mặc định tính trên toàn bộ ngân sách cưới, khoản chi hộ hoặc chi phí của bên thứ ba.</p>}
        <p className="mt-3 text-xs muted">Phiên bản chính sách nhóm: {group.policyVersion}</p>
      </section>)}</div>}
      <p className="mt-3 text-xs leading-relaxed muted">Mỗi dịch vụ có danh mục riêng; xem mức của danh mục đã chọn ngay trên form tạo dịch vụ. Danh sách này không phải nhóm shop đã đăng ký hoặc bằng chứng đã duyệt một thỏa thuận hoa hồng riêng.</p>
      <div className="mt-5 border-t border-[var(--line)] pt-4 text-xs leading-relaxed muted">
        {data && (data.policyAcceptedAt ? <p>Thông tin xác nhận đã lưu{data.policyVersion ? ` · phiên bản ${data.policyVersion}` : ""}: {policyAcceptedTime(data.policyAcceptedAt)}. Xác nhận điều khoản không đồng nghĩa đã ký hợp đồng, chấp thuận mọi nhóm hoặc kích hoạt một chính sách riêng.</p> : <p>Chưa có thời điểm xác nhận điều khoản được ghi nhận. Điều này không yêu cầu Admin bổ sung nhóm trước khi shop tạo dịch vụ.</p>)}
        <p className="mt-2">Đây là khu vực chỉ xem. Những thỏa thuận riêng và thay đổi có hiệu lực cần được quản trị viên phê duyệt; không thể tự chỉnh tỷ lệ tại đây. Tỷ lệ đã lưu trên giao dịch cũ không bị thay đổi.</p>
      </div>
    </Panel>
  </div>;
}
