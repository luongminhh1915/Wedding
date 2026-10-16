import { useState } from "react";
import {
  Building2,
  MessageSquare,
  HeartHandshake,
  ShieldCheck,
  LayoutList,
} from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { useAuthStore } from "../../store/authStore";
import axiosClient from "../../services/axiosClient";
import ListingTable from "./components/ListingTable";
import { LeadInbox } from "./components/LeadInbox";
import { ContractList } from "./components/ContractList";
import { SettlementDashboard } from "./components/SettlementDashboard";

const tabs = [
  { id: "listings", label: "Gói dịch vụ", icon: LayoutList },
  { id: "leads", label: "Yêu cầu tư vấn", icon: MessageSquare },
  {
    id: "contracts-couple",
    label: "Hợp đồng với cặp đôi",
    icon: HeartHandshake,
  },
  { id: "contracts-admin", label: "Hoa hồng & đối soát", icon: ShieldCheck },
] as const;

export default function VendorDashboard() {
  const { user } = useAuthStore();
  const [activeTab, setActiveTab] =
    useState<(typeof tabs)[number]["id"]>("listings");
  const { data: categories = [] } = useQuery<{ id: string; name: string }[]>({
    queryKey: ["categories"],
    queryFn: async () => (await axiosClient.get("/api/categories")).data,
    staleTime: 5 * 60 * 1000,
  });

  return (
    <main className="portal-shell">
      <div className="portal-heading">
        <div>
          <Building2 size={24} strokeWidth={1.5} />
        </div>
        <div>
          <h1>{user?.vendorBrandName || "Không gian đối tác"}</h1>
          <p>
            Xin chào {user?.fullName}. Cùng tạo nên những ngày cưới đáng nhớ.
          </p>
        </div>
        <span>Đối tác Wedding Match</span>
      </div>
      <nav className="planner-tabs" aria-label="Quản lý nhà cung cấp">
        {tabs.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            className={activeTab === id ? "active" : ""}
            onClick={() => setActiveTab(id)}
            aria-current={activeTab === id ? "page" : undefined}
          >
            <Icon size={17} />
            {label}
          </button>
        ))}
      </nav>
      <div className="feature-content">
        {activeTab === "listings" && <ListingTable categories={categories} />}
        {activeTab === "leads" && <LeadInbox />}
        {activeTab === "contracts-couple" && <ContractList />}
        {activeTab === "contracts-admin" && <SettlementDashboard />}
      </div>
    </main>
  );
}
