import { useLayoutEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Plus, Send, Trash2 } from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import type { Lead } from "../../../types/lead.types";
import { apiError, type QuoteLine } from "../api";
import {
  useFinancialQuotes as useQuotes,
  useSendFinancialQuote as useSendQuote,
} from "../financeApi";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDate } from "../format";
import { useNotificationFocus } from "../../notifications/useNotificationFocus";
import { MoneyInput } from "../../../components/MoneyInput";

/** P4 – NCC gửi báo giá có cấu trúc cho các yêu cầu đã tiếp nhận (gồm cả Lead đến từ combo). */
export function VendorQuotes() {
  const leadQuery = useQuery({
    queryKey: ["vendor-leads", "quotable"],
    queryFn: async () =>
      (await axiosClient.get<Lead[]>("/api/leads/vendor-inbox")).data,
  });
  const leads = leadQuery.data ?? [];
  const leadsReady = !leadQuery.isPending && !leadQuery.isError && !leadQuery.isFetching;
  const { data: quotes = [], isPending: loadingQuotes, isError: quotesError, refetch: reloadQuotes } = useQuotes();
  const { focusId, missing } = useNotificationFocus("quote", quotes.map(quote => quote.id), !loadingQuotes && !quotesError);
  const send = useSendQuote();
  const quotable = leads.filter(
    (l) => l.status === "Accepted" || l.status === "Contacted",
  );
  const [leadId, setLeadId] = useState<string>("");
  const [items, setItems] = useState<QuoteLine[]>([
    { name: "", quantity: 1, unitPrice: 0 },
  ]);
  const [deposit, setDeposit] = useState(0);
  const [vatRate, setVatRate] = useState(0);
  const [terms, setTerms] = useState(
    "Cọc 30% khi ký, thanh toán phần còn lại trước ngày cưới 7 ngày.",
  );
  const subtotal = items.reduce((s, i) => s + i.quantity * i.unitPrice, 0);
  const total = subtotal + Math.round(subtotal * vatRate);
  const selectedLead = quotable.find(lead => lead.id === leadId);
  const sendLock = useRef(false);
  const latestSelection = useRef({ ready: leadsReady, leadId, selectedLeadId: selectedLead?.id });
  useLayoutEffect(() => {
    latestSelection.current = { ready: leadsReady, leadId, selectedLeadId: selectedLead?.id };
  }, [leadsReady, leadId, selectedLead?.id]);
  const pick = (id: string) => {
    if (!leadsReady || send.isPending || sendLock.current) return;
    setLeadId(id);
    const l = leads.find((x) => x.id === id);
    setItems([
      {
        name: l?.listingTitle ?? "Gói dịch vụ",
        quantity: 1,
        unitPrice: l?.estimatedBudget ?? 0,
      },
    ]);
    setDeposit(Math.round(((l?.estimatedBudget ?? 0) * 0.3) / 100000) * 100000);
  };
  const submit = () => {
    const current = latestSelection.current;
    if (sendLock.current || send.isPending || !current.ready ||
      !current.selectedLeadId || current.leadId !== leadId || current.selectedLeadId !== leadId || total <= 0) return;
    sendLock.current = true;
    send.mutate({ leadId, items, depositAmount: deposit, terms, validDays: 7, vatRate }, {
      onSuccess: () => setLeadId(""),
      onSettled: () => { sendLock.current = false; },
    });
  };

  return (
    <div className="wd">
      <Panel eyebrow="Báo giá" title="Gửi báo giá cho cặp đôi">
        {leadQuery.isError ? (
          <div role="alert" className="mb-3 space-y-2">
            <p>Chưa tải được yêu cầu tư vấn. Bản nháp đang nhập được giữ nguyên; hãy tải lại trước khi gửi báo giá.</p>
            <button type="button" className="btn btn-outline" disabled={leadQuery.isFetching} onClick={() => void leadQuery.refetch()}>Tải lại yêu cầu</button>
          </div>
        ) : leadQuery.isPending ? <p role="status">Đang tải yêu cầu tư vấn...</p>
          : leadQuery.isFetching ? <p role="status" className="mb-3">Đang cập nhật yêu cầu tư vấn. Vui lòng chờ trước khi gửi báo giá.</p> : null}
        {leadsReady && quotable.length === 0 && !leadId ? (
          <Empty>
            Chưa có yêu cầu nào đã tiếp nhận. Tiếp nhận Lead ở tab “Yêu cầu tư
            vấn” trước.
          </Empty>
        ) : quotable.length > 0 || leadId ? (
          <div className="space-y-3">
            <label className="block">
              <span className="label">Yêu cầu tư vấn</span>
              <select
                className="input"
                value={leadId}
                disabled={!leadsReady || send.isPending}
                onChange={(e) => pick(e.target.value)}
              >
                <option value="">— Chọn yêu cầu —</option>
                {leadId && !selectedLead && <option value={leadId} disabled>Yêu cầu đã chọn không còn nhận báo giá</option>}
                {quotable.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.customerName} · {l.listingTitle ?? "Dịch vụ"} ·{" "}
                    {l.weddingDate ? vnDate(l.weddingDate) : "chưa có ngày"}
                    {l.notes?.startsWith("[Combo") ? " · COMBO" : ""}
                  </option>
                ))}
              </select>
            </label>
            {leadsReady && leadId && !selectedLead && <p role="alert">Yêu cầu đã chọn không còn nhận báo giá. Bản nháp được giữ để đối chiếu; hãy chọn một yêu cầu đang tiếp nhận trước khi gửi.</p>}
            {leadId && (
              <fieldset className="min-w-0 space-y-3 border-0 p-0" disabled={!leadsReady || !selectedLead || send.isPending}>
                {items.map((it, k) => (
                  <div
                    key={k}
                    className="grid grid-cols-[1fr_80px_150px_40px] gap-2"
                  >
                    <input
                      className="input"
                      placeholder="Hạng mục"
                      value={it.name}
                      onChange={(e) =>
                        setItems(
                          items.map((x, j) =>
                            j === k ? { ...x, name: e.target.value } : x,
                          ),
                        )
                      }
                    />
                    <input
                      className="input"
                      type="number"
                      min={1}
                      value={it.quantity}
                      onChange={(e) =>
                        setItems(
                          items.map((x, j) =>
                            j === k ? { ...x, quantity: +e.target.value } : x,
                          ),
                        )
                      }
                      aria-label="Số lượng"
                    />
                    <MoneyInput
                      className="input"
                      min={0}
                      value={it.unitPrice}
                      onValueChange={(value) =>
                        setItems(
                          items.map((x, j) =>
                            j === k ? { ...x, unitPrice: value ?? 0 } : x,
                          ),
                        )
                      }
                      aria-label="Đơn giá"
                    />
                    <button
                      className="btn btn-ghost !px-2"
                      onClick={() => setItems(items.filter((_, j) => j !== k))}
                      aria-label="Xóa dòng"
                      disabled={items.length === 1}
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                ))}
                <button
                  className="btn btn-outline"
                  onClick={() =>
                    setItems([
                      ...items,
                      { name: "", quantity: 1, unitPrice: 0 },
                    ])
                  }
                >
                  <Plus size={14} /> Thêm hạng mục
                </button>
                <div className="grid md:grid-cols-[200px_1fr] gap-3">
                  <label>
                    <span className="label">Thuế suất VAT</span>
                    <select
                      className="input"
                      value={vatRate}
                      onChange={(e) => setVatRate(+e.target.value)}
                    >
                      <option value={0}>Không tính VAT</option>
                      <option value={0.08}>8%</option>
                      <option value={0.1}>10%</option>
                    </select>
                  </label>
                  <label>
                    <span className="label">Tiền cọc</span>
                    <MoneyInput
                      className="input"
                      value={deposit}
                      onValueChange={(value) => setDeposit(value ?? 0)}
                      aria-label="Tiền cọc (VNĐ)"
                    />
                  </label>
                  <label>
                    <span className="label">Điều khoản</span>
                    <input
                      className="input"
                      value={terms}
                      onChange={(e) => setTerms(e.target.value)}
                    />
                  </label>
                </div>
                <ErrorText>
                  {send.isError ? apiError(send.error) : ""}
                </ErrorText>
                <div className="flex justify-between items-center">
                  <p>
                    Tổng: <b>{vnd(total)}</b>
                  </p>
                  <button
                    className="btn btn-primary"
                    disabled={!leadsReady || !selectedLead || send.isPending || total <= 0}
                    onClick={submit}
                  >
                    <Send size={14} /> Gửi báo giá
                  </button>
                </div>
              </fieldset>
            )}
          </div>
        ) : null}
      </Panel>
      <Panel title="Báo giá đã gửi">
        {missing && <p role="status" className="text-sm muted">Báo giá trong thông báo không còn khả dụng trong tài khoản của bạn.</p>}
        {quotesError ? <div role="alert"><p>Chưa tải được báo giá.</p><button className="btn btn-outline" onClick={() => reloadQuotes()}>Thử lại</button></div> : loadingQuotes ? <p role="status">Đang tải báo giá...</p> : quotes.length === 0 ? (
          <Empty>Chưa gửi báo giá nào.</Empty>
        ) : (
          <table className="wd-table">
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tổng</th>
                <th>Cọc</th>
                <th>Hiệu lực</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {quotes.map((q) => (
                <tr key={q.id} id={`notification-quote-${q.id}`} tabIndex={-1} data-notification-focus={focusId === q.id}>
                  <td>
                    {q.code} <span className="muted text-xs">v{q.version}</span>
                  </td>
                  <td>{vnd(q.totalAmount)}</td>
                  <td>{vnd(q.depositAmount)}</td>
                  <td>{vnDate(q.validUntil)}</td>
                  <td>
                    <StatusPill status={q.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Panel>
    </div>
  );
}
