import { useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import type { OrderJourneyItem } from "../../journey/customerOrders/customerOrderApi";
import { useAuthStore } from "../../../store/authStore";
import { FileCheck2, MessageCircle, ReceiptText } from "lucide-react";
import { CustomerLeads } from "./CustomerLeads";
import { MyContracts } from "./MyContracts";
import { MyQuotes } from "../../journey/quotes/MyQuotes";
import { CustomerDisputes } from "../../journey/quotes/CustomerDisputes";
import { OrderFilterBar } from "../../journey/customerOrders/OrderUi";

/** One workspace; keep proposals separate from signed financial commitments. */
export function ContractWorkspace() {
  const [params, setParams] = useSearchParams();
  const requested = params.get("section");
  const focusKind = params.get("focusKind");
  const section = requested === "quotes" || (!requested && focusKind === "quote") ? "quotes" :
    requested === "contracts" || (!requested && focusKind === "contract") ? "contracts" : requested === "support" ? "support" : "consultation";
  const setSection = (next: string) => setParams(previous => { const updated = new URLSearchParams(previous); updated.set("section", next === "consultation" ? "leads" : next); updated.delete("focus"); updated.delete("focusKind"); return updated; });
  const requestFocus = focusKind === "order-request" ? params.get("focus") : null;
  const userId = useAuthStore(state => state.user?.id);
  const requests = useQuery({ queryKey: ["customer-order-request-focus", userId, requestFocus], enabled: !!requestFocus && !!userId, queryFn: async () => (await axiosClient.get<OrderJourneyItem[]>("/api/customer-orders")).data });
  useEffect(() => {
    if (!requestFocus || !requests.data) return;
    const target = requests.data.find(item => item.requests.some(request => request.id === requestFocus));
    if (!target) return;
    setParams(previous => { const next = new URLSearchParams(previous); next.set("focus", target.id); next.set("focusKind", target.kind); next.set("section", target.kind === "contract" ? "contracts" : target.kind === "quote" ? "quotes" : "leads"); if (target.kind === "combo") next.set("tab", "combos"); return next; }, { replace: true });
  }, [requestFocus, requests.data, setParams]);
  return (
    <div className="wd customer-order-workspace">
      <header className="customer-order-workspace-heading">
        <p className="eyebrow">CÙNG CHỌN ĐIỀU PHÙ HỢP</p>
        <h2 className="serif text-2xl">Dịch vụ & những thỏa thuận của bạn</h2>
        <p className="muted text-sm mt-2">
          Theo dõi theo nhà cung cấp. Bạn có thể đề nghị thay đổi, dừng yêu cầu
          hoặc cất vào lưu trữ mà không mất lịch sử.
        </p>
      </header>
      <OrderFilterBar
        value={section}
        onChange={setSection}
        label="Loại hồ sơ"
        options={[
          { id: "consultation", label: "Tư vấn" },
          { id: "quotes", label: "Báo giá" },
          { id: "contracts", label: "Hợp đồng" },
          { id: "support", label: "Hỗ trợ" },
        ]}
      />
      <p className="customer-order-policy">
        <FileCheck2 size={16} /> Dừng tư vấn khác với hủy hợp đồng. Yêu cầu hủy
        không tự hoàn cọc hoặc thực hiện thanh toán.
      </p>
      {requestFocus && <p role={requests.isError ? "alert" : "status"} className="co-summary-note">{requests.isPending ? "Đang tìm hồ sơ liên quan đến phản hồi..." : requests.isError ? "Chưa tải được hồ sơ. Vui lòng thử lại từ trang Thông báo." : "Hồ sơ này không còn khả dụng trong tài khoản của bạn."}</p>}
      {section === "consultation" && <CustomerLeads />}
      {section === "quotes" && (
        <MyQuotes onOpenContracts={() => setSection("contracts")} />
      )}
      {section === "contracts" && <MyContracts />}
      {section === "support" && <CustomerDisputes />}
      <nav className="customer-order-shortcuts" aria-label="Chuyển bước xử lý">
        {section !== "consultation" && (
          <button
            className="btn btn-ghost"
            onClick={() => setSection("consultation")}
          >
            <MessageCircle size={14} /> Xem yêu cầu tư vấn
          </button>
        )}
        {section !== "quotes" && (
          <button
            className="btn btn-ghost"
            onClick={() => setSection("quotes")}
          >
            <ReceiptText size={14} /> Xem báo giá
          </button>
        )}
        {section !== "contracts" && (
          <button
            className="btn btn-ghost"
            onClick={() => setSection("contracts")}
          >
            <FileCheck2 size={14} /> Xem hợp đồng
          </button>
        )}
      </nav>
    </div>
  );
}
