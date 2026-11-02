import type { Contract } from '../../types/contract.types';

type SnapshotContract = Pick<Contract, 'status' | 'confirmedAt' | 'commissionRateSnapshot' | 'categorySnapshot'>;

/** Only immutable backend metadata may be labelled historical; current shop/listing data is not a fallback. */
export function contractCategoryPresentation(contract: SnapshotContract) {
  const snapshot = contract.categorySnapshot;
  const rate = typeof contract.commissionRateSnapshot === 'number' && Number.isFinite(contract.commissionRateSnapshot) &&
    contract.commissionRateSnapshot >= .03 && contract.commissionRateSnapshot <= .12
    ? new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 2 }).format(contract.commissionRateSnapshot) : null;
  if (snapshot) {
    return {
      title: snapshot.categoryName,
      group: snapshot.businessGroupName || null,
      note: 'Danh mục được lưu tại thời điểm xác nhận, không đổi theo danh mục hiện tại.',
      rate,
      source: snapshot.rateSource === 'LegacyVendor' ? 'Tỷ lệ riêng của shop được giữ nguyên' :
        snapshot.rateSource === 'CategoryDefault' ? 'Tỷ lệ mặc định theo danh mục' : 'Nguồn tỷ lệ chưa xác định',
      policy: snapshot.policyVersion ? `Phiên bản ${snapshot.policyVersion}` :
        'Chưa có thông tin phiên bản chính sách cũ; không suy diễn từ chính sách hiện tại.',
    };
  }
  const historical = !!contract.confirmedAt || ['Confirmed', 'Completed', 'Disputed'].includes(contract.status);
  return {
    title: historical ? 'Hợp đồng cũ chưa lưu danh mục lúc xác nhận' : 'Danh mục sẽ được chốt khi khách xác nhận',
    group: null,
    note: historical ? 'Không lấy danh mục hiện tại để gán ngược cho hợp đồng cũ.' : 'Hệ thống lấy danh mục từ dịch vụ gốc; khách không tự chọn tỷ lệ hoa hồng.',
    rate,
    source: rate ? 'Tỷ lệ đã lưu trên hợp đồng' : null,
    policy: historical && !rate ? 'Chưa đủ snapshot hoa hồng; cần Admin/Tài chính đối chiếu trước khi hoàn tất.' : null,
  };
}
