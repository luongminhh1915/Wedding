import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import type { Contract } from "../../../types/contract.types";
import { apiError } from "../../journey/api";

export function ContractRevisionActions({
  contract,
  onSuccess,
}: {
  contract: Contract;
  onSuccess: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [value, setValue] = useState(contract.contractValue);
  const [deposit, setDeposit] = useState(contract.depositAmount);
  const [date, setDate] = useState(contract.weddingDate?.slice(0, 10) ?? "");
  const [policy, setPolicy] = useState(
    contract.cancellationPolicySnapshot ?? "",
  );
  const revise = useMutation({
    mutationFn: () =>
      axiosClient.post(`/api/contracts/${contract.id}/revise`, {
        contractValue: value,
        depositAmount: deposit,
        weddingDate: date ? `${date}T00:00:00Z` : null,
        cancellationPolicy: policy,
        contractImageUrl: contract.contractImageUrl,
      }),
    onSuccess: () => {
      setOpen(false);
      onSuccess();
    },
  });
  const complete = useMutation({
    mutationFn: () =>
      axiosClient.post(`/api/contracts/${contract.id}/complete`),
    onSuccess,
  });
  return (
    <>
      {contract.status === "Draft" && (
        <button
          className="px-3 py-2 rounded-xl bg-slate-900 text-white text-xs font-semibold"
          onClick={() => setOpen(true)}
        >
          Sửa & gửi lại HĐ
        </button>
      )}
      {contract.status === "Confirmed" && (
        <button
          className="px-3 py-2 rounded-xl border text-xs font-semibold"
          disabled={complete.isPending}
          onClick={() => {
            if (
              confirm(
                "Dịch vụ đã thực hiện sau ngày cưới? K2 vẫn khóa đủ thời hạn khiếu nại 3 ngày.",
              )
            )
              complete.mutate();
          }}
        >
          Hoàn tất dịch vụ
        </button>
      )}
      {complete.error && (
        <span className="text-xs text-red-700">{apiError(complete.error)}</span>
      )}
      {open && (
        <div
          className="fixed inset-0 z-50 bg-slate-950/50 p-4 flex items-center justify-center"
          role="dialog"
          aria-modal="true"
          aria-label="Chỉnh sửa hợp đồng bị trả về"
        >
          <form
            className="bg-white p-6 rounded-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto space-y-3"
            onSubmit={(e) => {
              e.preventDefault();
              revise.mutate();
            }}
          >
            <h3 className="font-bold">
              Sửa {contract.contractCode} · lần{" "}
              {(contract.revisionCount ?? 0) + 1}
            </h3>
            <p className="text-xs text-red-700">
              Phản hồi khách:{" "}
              {contract.rejectReason ?? contract.cancellationReason}
            </p>
            <label className="block text-xs">
              Tổng giá trị
              <input
                required
                className="w-full border rounded-lg p-2"
                type="number"
                min={1}
                value={value}
                onChange={(e) => setValue(+e.target.value)}
              />
            </label>
            <label className="block text-xs">
              Tổng cọc (đã gồm cọc lịch hẹn)
              <input
                required
                className="w-full border rounded-lg p-2"
                type="number"
                min={0}
                max={value}
                value={deposit}
                onChange={(e) => setDeposit(+e.target.value)}
              />
            </label>
            <label className="block text-xs">
              Ngày cưới
              <input
                required
                className="w-full border rounded-lg p-2"
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
              />
            </label>
            <label className="block text-xs">
              Chính sách hủy / hoàn cọc
              <textarea
                required
                rows={4}
                maxLength={4000}
                className="w-full border rounded-lg p-2"
                value={policy}
                onChange={(e) => setPolicy(e.target.value)}
              />
            </label>
            {revise.error && (
              <p className="text-xs text-red-700">{apiError(revise.error)}</p>
            )}
            <div className="flex gap-3">
              <button
                type="button"
                className="border rounded-lg px-4 py-2 text-sm"
                onClick={() => setOpen(false)}
              >
                Đóng
              </button>
              <button
                className="bg-slate-900 text-white rounded-lg px-4 py-2 text-sm"
                disabled={revise.isPending}
              >
                Gửi khách duyệt lại (72 giờ)
              </button>
            </div>
          </form>
        </div>
      )}
    </>
  );
}
