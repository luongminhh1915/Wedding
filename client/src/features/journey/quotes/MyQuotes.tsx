import { FileSignature } from "lucide-react";
import { apiError, useRespondQuote } from "../api";
import { useFinancialQuotes as useQuotes } from "../financeApi";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDate } from "../format";

/** P4 – Khách xem báo giá có cấu trúc; chấp nhận → hệ thống tạo hợp đồng chờ xác nhận (72h). */
export function MyQuotes() {
  const { data = [], isLoading } = useQuotes();
  const respond = useRespondQuote();
  return (
    <Panel eyebrow="Báo giá" title="Báo giá từ nhà cung cấp">
      <ErrorText>{respond.isError ? apiError(respond.error) : ""}</ErrorText>
      {isLoading ? (
        <p className="muted text-sm">Đang tải...</p>
      ) : data.length === 0 ? (
        <Empty>
          Chưa có báo giá. NCC sẽ gửi báo giá sau khi tiếp nhận yêu cầu tư vấn.
        </Empty>
      ) : (
        <ul className="grid gap-3">
          {data.map((q) => (
            <li
              key={q.id}
              className="rounded-2xl border border-[var(--line)] p-4 bg-[var(--paper)]"
            >
              <div className="flex flex-wrap justify-between gap-2">
                <p className="font-bold">
                  {q.vendorName} · {q.code}{" "}
                  <span className="text-xs muted">v{q.version}</span>{" "}
                  <StatusPill status={q.status} />
                </p>
                <p className="text-xs muted">
                  Hiệu lực đến {vnDate(q.validUntil)}
                </p>
              </div>
              <table className="wd-table mt-2">
                <tbody>
                  {q.items.map((i, k) => (
                    <tr key={k}>
                      <td>{i.name}</td>
                      <td className="text-right">
                        {i.quantity} × {vnd(i.unitPrice)}
                      </td>
                      <td className="text-right font-bold">
                        {vnd(i.quantity * i.unitPrice)}
                      </td>
                    </tr>
                  ))}
                  {!!q.vatAmount && (
                    <tr>
                      <td>VAT {q.vatRate * 100}%</td>
                      <td />
                      <td className="text-right">{vnd(q.vatAmount)}</td>
                    </tr>
                  )}
                </tbody>
              </table>
              <div className="flex flex-wrap justify-between items-center gap-3 mt-3">
                <p className="text-sm">
                  Tổng <b>{vnd(q.totalAmount)}</b> · Cọc {vnd(q.depositAmount)}
                  {q.terms ? ` · ${q.terms}` : ""}
                </p>
                {q.status === "Sent" && (
                  <div className="flex gap-2">
                    <button
                      className="btn btn-outline"
                      disabled={respond.isPending}
                      onClick={() => {
                        const note =
                          prompt("Lý do từ chối / đề nghị điều chỉnh?") ??
                          undefined;
                        respond.mutate({ id: q.id, accept: false, note });
                      }}
                    >
                      Từ chối
                    </button>
                    <button
                      className="btn btn-primary"
                      disabled={respond.isPending}
                      onClick={() => respond.mutate({ id: q.id, accept: true })}
                    >
                      <FileSignature size={14} /> Chấp nhận & tạo hợp đồng
                    </button>
                  </div>
                )}
                {q.contractId && (
                  <span className="pill ok">
                    Đã tạo hợp đồng – xác nhận ở mục Hợp đồng bên dưới
                  </span>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}
