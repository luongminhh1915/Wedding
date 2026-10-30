import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import { apiError, useRunSettlement } from "../api";
import {
  useCreditNotes,
  useLedgerStatements,
  useMatchReceipt,
  useRecordInvoice,
  useRecordReceipt,
} from "../financeApi";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDate } from "../format";
import { useUnmatchedReceipts } from "../financeApi";

export function SettlementsPanel() {
  const [period, setPeriod] = useState(() =>
    new Date().toISOString().slice(0, 7),
  );
  const statements = useLedgerStatements(period);
  const run = useRunSettlement();
  const record = useRecordReceipt();
  const match = useMatchReceipt();
  const invoice = useRecordInvoice();
  const unmatched = useUnmatchedReceipts();
  const credits = useCreditNotes();
  const qc = useQueryClient();
  const [payment, setPayment] = useState({
    id: "",
    transactionId: "",
    amount: 0,
    note: "",
  });
  const [matching, setMatching] = useState({
    id: "",
    settlementId: "",
    premiumOrderId: "",
    note: "",
  });
  const [invoiceForm, setInvoiceForm] = useState({
    id: "",
    provider: "",
    invoiceNumber: "",
    lookupUrl: "",
    vatRate: 0.08,
  });
  const resolve = useMutation({
    mutationFn: (id: string) =>
      axiosClient.post(`/api/settlements/${id}/resolve`),
    onSuccess: () => qc.invalidateQueries(),
  });
  const errors = [
    statements.error,
    run.error,
    record.error,
    match.error,
    invoice.error,
    unmatched.error,
    resolve.error,
  ].filter(Boolean);
  return (
    <div className="wd space-y-5">
      <Panel
        eyebrow="Sổ đối soát"
        title={`Bảng kê hoa hồng ${period}`}
        action={
          <div className="flex gap-2 flex-wrap">
            <input
              className="input !w-[155px]"
              type="month"
              value={period}
              onChange={(e) => setPeriod(e.target.value)}
              aria-label="Kỳ đối soát"
            />
            <button
              className="btn btn-primary"
              disabled={run.isPending}
              onClick={() => run.mutate(period)}
            >
              Chốt từ ngày 25
            </button>
          </div>
        }
      >
        <p className="text-xs muted mb-3">
          K1 50% khi xác nhận; K2 khóa đến sau cưới 3 ngày và không có tranh
          chấp. Chốt ngày 25, hạn trả ngày 30 (tháng 2: ngày cuối tháng). Không
          tạo hóa đơn hay thanh toán giả.
        </p>
        {errors.map((e, i) => (
          <ErrorText key={i}>{apiError(e)}</ErrorText>
        ))}
        {!statements.data?.length ? (
          <Empty>
            Chưa có bảng kê. Chỉ khoản đủ điều kiện mới được gom vào kỳ.
          </Empty>
        ) : (
          <div className="overflow-x-auto">
            <table className="wd-table">
              <thead>
                <tr>
                  <th>NCC</th>
                  <th>Phải thu</th>
                  <th>Đã thu</th>
                  <th>Hạn</th>
                  <th>Tham chiếu</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {statements.data.map((s) => (
                  <tr key={s.id}>
                    <td>{s.vendorName}</td>
                    <td>{vnd(s.totalAmount)}</td>
                    <td>{vnd(s.paidAmount)}</td>
                    <td>{vnDate(s.dueDate)}</td>
                    <td className="text-xs">
                      {s.paymentReference}
                      {s.invoiceNumber && <p>HĐ: {s.invoiceNumber}</p>}
                    </td>
                    <td>
                      <StatusPill status={s.status} />
                    </td>
                    <td>
                      {["Closed", "Overdue"].includes(s.status) && (
                        <button
                          className="btn btn-outline"
                          onClick={() =>
                            setPayment({
                              id: s.id,
                              transactionId: "",
                              amount: s.totalAmount - s.paidAmount,
                              note: "",
                            })
                          }
                        >
                          Ghi giao dịch thật
                        </button>
                      )}
                      {s.status === "InDispute" && (
                        <button
                          className="btn btn-outline"
                          onClick={() => {
                            if (
                              confirm(
                                "Đã kiểm tra và giải quyết tranh chấp bảng kê?",
                              )
                            )
                              resolve.mutate(s.id);
                          }}
                        >
                          Gỡ tranh chấp
                        </button>
                      )}
                      {s.status === "Paid" && !s.invoiceNumber && (
                        <button
                          className="btn btn-outline"
                          onClick={() =>
                            setInvoiceForm({ ...invoiceForm, id: s.id })
                          }
                        >
                          Ghi HĐ đã phát hành
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {payment.id && (
          <form
            className="grid md:grid-cols-2 gap-3 mt-4 rounded-xl border p-4"
            onSubmit={(e) => {
              e.preventDefault();
              record.mutate(payment, {
                onSuccess: (r) => {
                  if (r.status === "Matched")
                    setPayment({
                      id: "",
                      transactionId: "",
                      amount: 0,
                      note: "",
                    });
                },
              });
            }}
          >
            <label>
              <span className="label">Mã giao dịch ngân hàng (duy nhất)</span>
              <input
                required
                className="input"
                value={payment.transactionId}
                onChange={(e) =>
                  setPayment({ ...payment, transactionId: e.target.value })
                }
              />
            </label>
            <label>
              <span className="label">Tiền thực nhận</span>
              <input
                required
                type="number"
                min="1"
                className="input"
                value={payment.amount}
                onChange={(e) =>
                  setPayment({ ...payment, amount: +e.target.value })
                }
              />
            </label>
            <label className="md:col-span-2">
              <span className="label">Căn cứ kiểm chứng sao kê</span>
              <input
                required
                className="input"
                value={payment.note}
                onChange={(e) =>
                  setPayment({ ...payment, note: e.target.value })
                }
              />
            </label>
            <button className="btn btn-primary" disabled={record.isPending}>
              Lưu sổ giao dịch
            </button>
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => setPayment({ ...payment, id: "" })}
            >
              Đóng
            </button>
            {record.data?.status === "Unmatched" && (
              <p className="text-sm">{record.data.unmatchedReason}</p>
            )}
          </form>
        )}
      </Panel>
      <Panel title="Giao dịch chưa khớp">
        {!unmatched.data?.length ? (
          <Empty>Không có giao dịch chưa khớp.</Empty>
        ) : (
          <ul className="space-y-3">
            {unmatched.data.map((r) => (
              <li key={r.id} className="border rounded-xl p-3">
                <b>
                  {r.transactionId} · {vnd(r.amount)}
                </b>
                <p className="text-xs break-all">{r.content}</p>
                <p className="text-xs muted">{r.unmatchedReason}</p>
                <button
                  className="btn btn-outline mt-2"
                  onClick={() =>
                    setMatching({
                      id: r.id,
                      settlementId: "",
                      premiumOrderId: "",
                      note: "",
                    })
                  }
                >
                  Đối soát thủ công
                </button>
              </li>
            ))}
          </ul>
        )}
        {matching.id && (
          <form
            className="grid gap-3 border rounded-xl p-4 mt-4"
            onSubmit={(e) => {
              e.preventDefault();
              match.mutate(
                {
                  id: matching.id,
                  settlementId: matching.settlementId || undefined,
                  premiumOrderId: matching.premiumOrderId || undefined,
                  note: matching.note,
                },
                { onSuccess: () => setMatching({ ...matching, id: "" }) },
              );
            }}
          >
            <label>
              <span className="label">Bảng kê đích (chọn một đích)</span>
              <select
                className="input"
                value={matching.settlementId}
                onChange={(e) =>
                  setMatching({
                    ...matching,
                    settlementId: e.target.value,
                    premiumOrderId: "",
                  })
                }
              >
                <option value="">— Chọn bảng kê —</option>
                {statements.data
                  ?.filter((s) => ["Closed", "Overdue"].includes(s.status))
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.vendorName} · {s.paymentReference}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              <span className="label">Hoặc ID đơn Premium đã kiểm chứng</span>
              <input
                className="input"
                value={matching.premiumOrderId}
                onChange={(e) =>
                  setMatching({
                    ...matching,
                    premiumOrderId: e.target.value,
                    settlementId: "",
                  })
                }
              />
            </label>
            <label>
              <span className="label">Lý do/căn cứ</span>
              <input
                required
                className="input"
                value={matching.note}
                onChange={(e) =>
                  setMatching({ ...matching, note: e.target.value })
                }
              />
            </label>
            <button
              className="btn btn-primary"
              disabled={
                match.isPending ||
                (!matching.settlementId && !matching.premiumOrderId)
              }
            >
              Khớp giao dịch
            </button>
          </form>
        )}
      </Panel>
      <Panel title="Credit note chuyển kỳ sau">
        {!credits.data?.length ? (
          <Empty>Chưa có khoản cấn trừ.</Empty>
        ) : (
          <ul>
            {credits.data.map((c) => (
              <li key={c.id} className="border-b py-3 text-sm">
                HĐ {c.contractId.slice(0, 8)} · {vnd(c.amount)} · đã dùng{" "}
                {vnd(c.appliedAmount)}
                <p className="muted">{c.reason}</p>
              </li>
            ))}
          </ul>
        )}
      </Panel>
      {invoiceForm.id && (
        <Panel title="Ghi nhận hóa đơn điện tử thật">
          <p className="text-xs muted mb-3">
            Phát hành tại nhà cung cấp hóa đơn đã ký hợp đồng, sau đó nhập số và
            đường dẫn tra cứu. Hệ thống không sinh số hóa đơn giả; thuế suất do
            Finance kiểm tra.
          </p>
          <form
            className="grid md:grid-cols-2 gap-3"
            onSubmit={(e) => {
              e.preventDefault();
              invoice.mutate(invoiceForm, {
                onSuccess: () => setInvoiceForm({ ...invoiceForm, id: "" }),
              });
            }}
          >
            <input
              required
              className="input"
              placeholder="Nhà cung cấp hóa đơn"
              value={invoiceForm.provider}
              onChange={(e) =>
                setInvoiceForm({ ...invoiceForm, provider: e.target.value })
              }
            />
            <input
              required
              className="input"
              placeholder="Số hóa đơn thật"
              value={invoiceForm.invoiceNumber}
              onChange={(e) =>
                setInvoiceForm({
                  ...invoiceForm,
                  invoiceNumber: e.target.value,
                })
              }
            />
            <input
              required
              type="url"
              className="input"
              placeholder="HTTPS URL tra cứu"
              value={invoiceForm.lookupUrl}
              onChange={(e) =>
                setInvoiceForm({ ...invoiceForm, lookupUrl: e.target.value })
              }
            />
            <label>
              <span className="label">Thuế suất (0.08 = 8%)</span>
              <input
                required
                type="number"
                step="0.01"
                min="0"
                max="1"
                className="input"
                value={invoiceForm.vatRate}
                onChange={(e) =>
                  setInvoiceForm({ ...invoiceForm, vatRate: +e.target.value })
                }
              />
            </label>
            <button className="btn btn-primary" disabled={invoice.isPending}>
              Lưu hóa đơn đã phát hành
            </button>
          </form>
        </Panel>
      )}
    </div>
  );
}

export function VendorSettlementLedger() {
  const { data = [], error } = useLedgerStatements();
  const qc = useQueryClient();
  const dispute = useMutation({
    mutationFn: (b: { id: string; reason: string }) =>
      axiosClient.post(`/api/settlements/${b.id}/dispute`, {
        reason: b.reason,
      }),
    onSuccess: () => qc.invalidateQueries(),
  });
  return (
    <Panel title="Bảng kê đã chốt & QR thanh toán">
      <ErrorText>
        {error ? apiError(error) : dispute.error ? apiError(dispute.error) : ""}
      </ErrorText>
      {data.length === 0 ? (
        <Empty>
          Chưa có bảng kê đã chốt. K2 đang khóa không nằm trong số cần trả.
        </Empty>
      ) : (
        <div className="grid md:grid-cols-2 gap-3">
          {data.map((s) => (
            <div className="border rounded-xl p-4" key={s.id}>
              <b>
                {s.period} · {vnd(s.totalAmount - s.paidAmount)}
              </b>{" "}
              <StatusPill status={s.status} />
              <p className="text-xs mt-2">
                Hạn {vnDate(s.dueDate)} · {s.paymentReference}
              </p>
              {s.qrUrl && (
                <img
                  className="mt-3 mx-auto"
                  src={s.qrUrl}
                  width={180}
                  height={180}
                  alt={`QR đúng bảng kê ${s.period}`}
                />
              )}
              {["Closed", "Overdue"].includes(s.status) && (
                <button
                  className="btn btn-outline mt-3"
                  disabled={dispute.isPending}
                  onClick={() => {
                    const reason = prompt(
                      "Nêu khoản sai lệch và căn cứ tranh chấp bảng kê",
                    );
                    if (reason?.trim()) dispute.mutate({ id: s.id, reason });
                  }}
                >
                  Báo sai lệch
                </button>
              )}
              <p className="text-xs muted mt-2">
                Chuyển đúng nội dung và số tiền. Trạng thái chỉ đổi sau khi ngân
                hàng/Finance đối soát.
              </p>
            </div>
          ))}
        </div>
      )}
    </Panel>
  );
}
