import type { CategoryDto } from "../../../types/category.types";
import type { VendorBusinessGroupPolicy } from "../businessGroupPolicy";
import { commissionPercent } from "../businessGroupPolicy";

/** Read-only server policy information, never a client-provided rate for saving. */
export function ServiceCategoryPolicySummary({ category, policy, pending, failed }: {
  category: CategoryDto;
  policy?: VendorBusinessGroupPolicy;
  pending: boolean;
  failed: boolean;
}) {
  const rate = category.defaultCommissionRate;
  const validRate = typeof rate === "number" && Number.isFinite(rate) && rate > 0 && rate <= 1;
  return <section aria-label="Nhóm hoa hồng của loại dịch vụ" className="rounded-xl border border-[#dedccf] bg-[#fcf6ea] p-4 text-xs leading-relaxed text-slate-600">
    <p className="font-semibold text-[#233a42]">Chính sách theo loại dịch vụ đã chọn</p>
    <dl className="mt-3 grid gap-2 sm:grid-cols-2">
      <div><dt>Loại dịch vụ</dt><dd className="font-semibold text-[#233a42]">{category.name}</dd></div>
      <div><dt>Nhóm hoa hồng tương ứng</dt><dd className="font-semibold text-[#233a42]">{category.businessGroupName || "Chưa có thông tin nhóm"}</dd></div>
      <div><dt>Hoa hồng mặc định theo danh mục</dt><dd className="font-semibold text-[#233a42]">{validRate ? commissionPercent(rate) : "Chưa có tỷ lệ hợp lệ"}</dd></div>
    </dl>
    <p className="mt-3">Thông tin do nền tảng cấu hình, không phải giảm giá cho khách. Bạn không cần đăng ký nhóm trước và không tự nhập tỷ lệ.</p>
    {policy?.usesLegacyCommissionRate && <p className="mt-2 rounded-lg bg-amber-50 p-2 text-amber-900">Shop đang giữ chính sách cũ{policy.legacyCommissionRate != null ? ` (${commissionPercent(policy.legacyCommissionRate)})` : ""}. Mức mặc định ở trên không tự thay thế tỷ lệ này; chỉ chuyển khi có xác nhận hợp lệ của Admin.</p>}
    {(pending || failed || !policy) && <p className="mt-2">Chưa xác nhận được chính sách riêng của shop. Mức theo danh mục chỉ là tham khảo, không tự thay thế chính sách cũ hoặc thỏa thuận riêng.</p>}
    <p className="mt-2">Chính sách thực tế được ghi nhận khi xác nhận hợp đồng; hợp đồng đã chốt giữ snapshot ban đầu.</p>
    {category.requiresCommissionBasisApproval && <p className="mt-2 text-amber-900">A–Z: mặc định chỉ tính trên phí quản lý/tổ chức được Admin duyệt, không trên toàn bộ ngân sách cưới. Chọn loại dịch vụ hoặc nhập mô tả không thay thế phê duyệt cơ sở tính.</p>}
  </section>;
}
