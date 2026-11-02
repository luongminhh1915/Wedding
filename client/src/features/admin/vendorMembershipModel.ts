/** Administrative scope assignment must never carry rate/contract/acknowledgment fields. */
export function vendorMembershipPayload(selected: readonly string[], allowed: readonly string[], existing: readonly string[], reason: string) {
  const ids = [...new Set(selected)];
  if (!ids.length || ids.length > 20 || ids.some(id => !allowed.includes(id) || existing.includes(id)))
    throw new Error("Hãy chọn nhóm đang hoạt động chưa có trong shop.");
  const note = reason.trim();
  if (!note || note.length > 2000) throw new Error("Cần lý do xác nhận, tối đa 2.000 ký tự.");
  return { businessGroupIds: ids, reason: note };
}
