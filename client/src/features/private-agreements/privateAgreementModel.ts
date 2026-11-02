export type PrivateAgreementKind = "combo" | "contract";
export interface PrivateServiceLine { name: string; quantity: number; details: string }
export interface PrivateServiceAgreement {
  id: string; sequence: number; status: "PendingCustomer" | "Accepted" | "Rejected" | "Withdrawn" | "Superseded";
  items: PrivateServiceLine[]; note: string; terms: string; createdAt: string; respondedAt?: string | null; response?: string | null;
}
export interface PrivateAgreementLeadOption { leadId: string; listingTitle: string }
export interface PrivateAgreementWorkspace {
  kind: PrivateAgreementKind; targetId: string; currentTotal: number; currentVersion: string;
  originalContent: PrivateServiceLine[]; effectiveContent: PrivateServiceLine[]; history: PrivateServiceAgreement[];
  canPropose: boolean; canRespond: boolean; canWithdraw?: boolean; blockedReason?: string | null; leadId?: string | null;
  leadOptions?: PrivateAgreementLeadOption[] | null;
  sourceComboId?: string | null;
  sourceComboContent?: PrivateServiceLine[] | null;
}
export interface PrivateAgreementDraft { expectedVersion: string; items: PrivateServiceLine[]; note: string; terms: string }

function version(value: string) {
  if (!/^[a-f\d]{64}$/i.test(value)) throw new Error("Chưa tải được phiên bản giao dịch hợp lệ. Hãy tải lại trước khi tiếp tục.");
  return value;
}
export function privateAgreementDraft(workspace: PrivateAgreementWorkspace): PrivateAgreementDraft {
  return { expectedVersion: workspace.currentVersion, items: workspace.effectiveContent.map(item => ({ name: item.name, quantity: item.quantity, details: item.details ?? "" })), note: "", terms: "" };
}
export function privateAgreementPayload(draft: PrivateAgreementDraft) {
  version(draft.expectedVersion);
  if (draft.items.length < 1 || draft.items.length > 30) throw new Error("Đề xuất cần từ 1 đến 30 hạng mục.");
  if (draft.items.some(item => !item.name.trim() || item.name.length > 200 || !Number.isInteger(item.quantity) || item.quantity < 1 || item.quantity > 10000 || item.details.length > 10000)) throw new Error("Kiểm tra tên (tối đa 200 ký tự), số lượng (1–10.000) và chi tiết (tối đa 10.000 ký tự) từng hạng mục.");
  if (!draft.note.trim() || draft.note.trim().length > 2000 || draft.terms.length > 4000) throw new Error("Nhập lý do đề xuất, tối đa 2000 ký tự; điều kiện tối đa 4000 ký tự.");
  const items = draft.items.map(item => ({ name: item.name.trim(), quantity: item.quantity, details: item.details.trim() }));
  if (JSON.stringify(items).length > 40000) throw new Error("Tổng nội dung đề xuất quá dài (tối đa 40.000 ký tự JSON). Hãy viết gọn phạm vi đã thống nhất.");
  return { expectedVersion: draft.expectedVersion, items, note: draft.note.trim(), terms: draft.terms.trim() };
}
export function privateAgreementResponse(expectedVersion: string, accept: boolean, note: string) {
  version(expectedVersion);
  if (note.length > 2000) throw new Error("Phản hồi tối đa 2000 ký tự.");
  return { expectedVersion, accept, note: note.trim() || null };
}
export function privateAgreementActions(workspace: PrivateAgreementWorkspace, role?: string | null) {
  const pending = workspace.history.find(item => item.status === "PendingCustomer");
  return { pending, propose: role === "VendorOwner" && workspace.canPropose && !workspace.blockedReason,
    respond: role === "Customer" && workspace.canRespond && !workspace.blockedReason && !!pending,
    withdraw: role === "VendorOwner" && (workspace.canWithdraw ?? !workspace.blockedReason) && !!pending };
}
export function privateAgreementEffective(workspace: PrivateAgreementWorkspace) {
  return [...workspace.history].sort((left, right) => right.sequence - left.sequence).find(item => item.status === "Accepted");
}
export function privateAgreementChatLead(workspace: PrivateAgreementWorkspace, contextualLeadId?: string, selectedLeadId?: string | null) {
  const options = workspace.leadOptions ?? [];
  if (contextualLeadId) return options.some(option => option.leadId === contextualLeadId) ? contextualLeadId : null;
  if (workspace.leadId) return options.some(option => option.leadId === workspace.leadId) ? workspace.leadId : null;
  return options.some(option => option.leadId === selectedLeadId) ? selectedLeadId ?? null : null;
}
