import { useState } from "react";
import { FileCheck2, MessageCircle, ReceiptText } from "lucide-react";
import { CustomerLeads } from "./CustomerLeads";
import { MyContracts } from "./MyContracts";
import { MyQuotes } from "../../journey/quotes/MyQuotes";
import { CustomerDisputes } from "../../journey/quotes/CustomerDisputes";
import { OrderFilterBar } from "../../journey/customerOrders/OrderUi";

/** One workspace; keep proposals separate from signed financial commitments. */
export function ContractWorkspace() {
  const [section, setSection] = useState("consultation");
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
