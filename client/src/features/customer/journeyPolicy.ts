export const JOURNEY_STAGES = [
  { id: 'overview', label: 'Tổng quan', path: '/customer/dashboard', hint: 'Xem tình trạng và việc nên làm tiếp theo.' },
  { id: 'checklist', label: 'Công việc', path: '/customer/dashboard?tab=checklist', hint: 'Theo dõi các mốc chuẩn bị, đánh dấu việc đã xong.' },
  { id: 'budget', label: 'Ngân sách', path: '/customer/dashboard?tab=budget', hint: 'Rà soát dự trù và số tiền đã chi trước khi chọn dịch vụ.' },
  { id: 'discovery', label: 'Khám phá', path: '/combo', hint: 'Chọn dịch vụ lẻ hoặc combo của một nhà cung cấp; trao đổi với NCC nếu muốn đổi gói.' },
  { id: 'combos', label: 'Lựa chọn', path: '/customer/dashboard?tab=combos', hint: 'Mở bản nháp, kiểm tra gói rồi gửi yêu cầu cho NCC.' },
  { id: 'contracts', label: 'Tư vấn & hợp đồng', path: '/customer/dashboard?tab=contracts', hint: 'Trao đổi, xem báo giá và xác nhận hợp đồng ở bước riêng.' },
] as const;

export function journeyNext(active: string, counts?: { leadCount: number; contractCount: number }) {
  if (active === 'overview' && counts && (counts.leadCount > 0 || counts.contractCount > 0))
    return { path: '/customer/dashboard?tab=contracts', label: 'Theo dõi tư vấn & hợp đồng', note: 'Bạn đã có yêu cầu hoặc hợp đồng. Kiểm tra phản hồi mới từ nhà cung cấp.' };
  const next = JOURNEY_STAGES[JOURNEY_STAGES.findIndex(step => step.id === active) + 1];
  if (!next || active === 'contracts' || !JOURNEY_STAGES.some(step => step.id === active)) return null;
  return { path: next.path, label: `Tiếp tục: ${next.label.toLowerCase()}`, note: next.hint };
}
