import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../services/axiosClient";
import { useBusinessGroups } from "../vendor-portal/hooks/useBusinessGroups";
import { commissionPercent, readVendorBusinessGroupPolicy } from "../vendor-portal/businessGroupPolicy";
import { apiError } from "../journey/api";
import { vendorMembershipPayload } from "./vendorMembershipModel";

export function VendorBusinessGroupsEditor({ vendorId }: { vendorId: string }) {
  const qc = useQueryClient();
  const groups = useBusinessGroups();
  const query = useQuery({
    queryKey: ["admin-vendor-business-groups", vendorId],
    queryFn: async () => readVendorBusinessGroupPolicy((await axiosClient.get(`/api/business-groups/vendors/${vendorId}`)).data),
    staleTime: 0,
  });
  const [selected, setSelected] = useState<string[]>([]);
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const save = useMutation({
    mutationFn: (body: { businessGroupIds: string[]; reason: string }) => axiosClient.post(`/api/business-groups/vendors/${vendorId}`, body),
    onSuccess: () => {
      setSelected([]); setReason("");
      void qc.invalidateQueries({ queryKey: ["admin-vendor-business-groups", vendorId] });
      void qc.invalidateQueries({ queryKey: ["vendor-service-categories"] });
      void qc.invalidateQueries({ queryKey: ["vendor-business-group-policy"] });
      void qc.invalidateQueries({ queryKey: ["vendor-journey"] });
    },
  });
  if (groups.isPending || query.isPending) return <p role="status" className="text-xs text-slate-500">Đang tải nhóm kinh doanh của shop...</p>;
  if (groups.isError || query.isError || !groups.data || !query.data) return <div role="alert" className="rounded-xl border border-red-200 p-3 text-xs"><p>Chưa kiểm tra được nhóm kinh doanh. Không có thông tin nào được thay đổi.</p><button type="button" className="mt-2 underline" onClick={() => { void groups.refetch(); void query.refetch(); }}>Thử lại</button></div>;
  const policy = query.data;
  const existing = policy.groups.map(group => group.id);
  return <section className="rounded-xl border border-[#ded8c6] bg-[#fcf6ea] p-4" aria-labelledby="vendor-membership-title">
    <h4 id="vendor-membership-title" className="text-sm font-semibold text-[#233a42]">Nhóm shop đã ghi nhận (thông tin tham khảo)</h4>
    <p className="mt-2 text-xs leading-relaxed text-slate-600">Shop được chọn mọi loại dịch vụ đang hoạt động khi tạo bài; không cần Admin cấp nhóm. Các nhóm cũ được giữ làm thông tin tham khảo. Bổ sung thông tin không đổi tỷ lệ hoa hồng, xác nhận chính sách hoặc hợp đồng đã chốt.</p>
    {policy.usesLegacyCommissionRate && <p className="mt-2 text-xs leading-relaxed text-slate-600">Chính sách cũ tiếp tục được giữ{policy.legacyCommissionRate != null ? `: ${commissionPercent(policy.legacyCommissionRate)}` : ''}. Bổ sung nhóm không phải chuyển đổi chính sách tài chính.</p>}
    {policy.groups.length > 0 && <p className="mt-3 text-xs leading-relaxed"><b>Đã ghi nhận:</b> {policy.groups.map(group => group.name).join("; ")}</p>}
    <form className="mt-4" onSubmit={event => {
      event.preventDefault(); setError("");
      try { save.mutate(vendorMembershipPayload(selected, groups.data!.map(group => group.id), existing, reason)); }
      catch (failure) { setError(failure instanceof Error ? failure.message : "Chưa thể gửi xác nhận."); }
    }}>
      <fieldset disabled={save.isPending} className="min-w-0 space-y-3"><legend className="mb-2 text-xs font-semibold">Bổ sung thông tin nhóm (không cấp quyền hoặc đổi hoa hồng)</legend>
        {groups.data.map(group => <label key={group.id} className="flex items-start gap-2 text-xs leading-relaxed"><input type="checkbox" className="mt-1 shrink-0" checked={existing.includes(group.id) || selected.includes(group.id)} disabled={existing.includes(group.id)} onChange={event => { setSelected(current => event.target.checked ? [...current, group.id] : current.filter(id => id !== group.id)); setError(""); save.reset(); }} /><span>{group.name}{existing.includes(group.id) && " · đã ghi nhận"}</span></label>)}
        <label className="block"><span className="text-xs font-semibold">Lý do xác nhận bổ sung (bắt buộc)</span><textarea value={reason} onChange={event => { setReason(event.target.value); setError(""); save.reset(); }} maxLength={2000} rows={3} className="mt-2 w-full rounded-xl border border-slate-200 bg-white p-3 text-xs" placeholder="Đã đối chiếu phạm vi dịch vụ và hồ sơ của shop..." /></label>
        {(error || save.isError) && <p role="alert" className="text-xs text-red-700">{error || apiError(save.error)}</p>}
        {save.isSuccess && <p role="status" className="text-xs text-green-800">Đã bổ sung nhóm và lưu lịch sử. Hoa hồng và hợp đồng cũ giữ nguyên.</p>}
        <button type="submit" disabled={!selected.length || !reason.trim() || save.isPending} className="rounded-xl bg-[#e38c82] px-4 py-2.5 text-xs font-semibold text-[#233a42] disabled:opacity-50">{save.isPending ? "Đang ghi nhận..." : "Xác nhận bổ sung nhóm"}</button>
      </fieldset>
    </form>
  </section>;
}
