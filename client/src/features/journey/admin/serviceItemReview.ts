export function serviceItemReviewPayload(
  item: { moderationStatus: string; reviewVersion?: string | null },
  approve: boolean,
  note: string,
) {
  if (item.moderationStatus !== "Pending") throw new Error("Dịch vụ không còn chờ duyệt. Hãy tải lại danh sách.");
  if (typeof item.reviewVersion !== "string" || !/^[a-f0-9]{64}$/i.test(item.reviewVersion)) {
    throw new Error("Chưa xác định được phiên bản dịch vụ. Hãy tải lại danh sách trước khi duyệt.");
  }
  const trimmed = note.trim();
  if ((!approve && !trimmed) || trimmed.length > 2000) throw new Error("Lý do từ chối cần từ 1 đến 2.000 ký tự; ghi chú tối đa 2.000 ký tự.");
  return { approve, note: trimmed || null, reviewVersion: item.reviewVersion };
}
