import { useState } from "react";
import {
  Activity,
  DollarSign,
  FileCheck,
  Layers,
  ShieldCheck,
  Users,
  BadgeCheck,
  Gavel,
  Receipt,
} from "lucide-react";
import {
  AdminKpiPanel,
  DisputeDesk,
  SettlementsPanel,
  VerificationQueue,
} from "../journey/admin/AdminJourneyPanels";
import { useAuthStore } from "../../store/authStore";
import ModerationQueue from "./ModerationQueue";
import UserManagement from "./UserManagement";
import AllListingsManager from "./AllListingsManager";
import FinancialDashboard from "./FinancialDashboard";
import { usePendingListings } from "../vendor-portal/hooks/useListings";

const tabs = [
  {
    id: "overview",
    label: "Tổng quan",
    icon: Activity,
    description: "KPI vận hành: GMV, chuyển đổi Lead → HĐ, SLA, tranh chấp, nhật ký thao tác.",
    roles: ["SuperAdmin", "Finance", "Moderator", "CustomerCare"],
  },
  {
    id: "moderation",
    label: "Kiểm duyệt",
    icon: FileCheck,
    description: "Xem xét bài đăng và đánh giá từ cộng đồng.",
  },
  {
    id: "published-listings",
    label: "Gói dịch vụ",
    icon: Layers,
    description: "Theo dõi và quản lý các gói dịch vụ trên nền tảng.",
  },
  {
    id: "users",
    label: "Người dùng & đối tác",
    icon: Users,
    description: "Kiểm tra thông tin và quản lý trạng thái tài khoản.",
  },
  {
    id: "finance",
    label: "Tài chính & đối soát",
    icon: DollarSign,
    description: "Tổng hợp hợp đồng, tiền cọc và hoa hồng của nhà cung cấp.",
  },
  {
    id: "settlements",
    label: "Bảng kê tháng",
    icon: Receipt,
    description: "Chốt bảng kê hoa hồng theo kỳ (hạn ngày 25), thu qua VietQR, hóa đơn điện tử (giả lập).",
    roles: ["SuperAdmin", "Finance"],
  },
  {
    id: "verifications",
    label: "Xác minh NCC",
    icon: BadgeCheck,
    description: "Duyệt hồ sơ pháp lý để cấp huy hiệu “Đã xác minh”.",
    roles: ["SuperAdmin", "Moderator"],
  },
  {
    id: "disputes",
    label: "Tranh chấp",
    icon: Gavel,
    description: "Xử lý sự cố hợp đồng giữa cặp đôi và nhà cung cấp trong 72 giờ.",
    roles: ["SuperAdmin", "CustomerCare"],
  },
] as const;

export default function AdminDashboard() {
  const { user } = useAuthStore();
  const [activeTab, setActiveTab] =
    useState<(typeof tabs)[number]["id"]>("overview");
  const visibleTabs = tabs.filter(
    (tab) => !("roles" in tab) || (tab.roles as readonly string[]).includes(user?.role ?? ""),
  );
  const { data: pendingListings = [] } = usePendingListings();
  const current = tabs.find((tab) => tab.id === activeTab)!;
  return (
    <main className="portal-shell">
      <div className="portal-heading">
        <div>
          <ShieldCheck size={24} strokeWidth={1.5} />
        </div>
        <div>
          <h1>Không gian quản trị</h1>
          <p>{user?.fullName} · Cùng vận hành một trải nghiệm cưới tốt hơn.</p>
        </div>
        <span>{user?.role}</span>
      </div>
      <nav className="planner-tabs" aria-label="Quản trị hệ thống">
        {visibleTabs.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            className={activeTab === id ? "active" : ""}
            onClick={() => setActiveTab(id)}
            aria-current={activeTab === id ? "page" : undefined}
          >
            <Icon size={17} />
            {label}
            {id === "moderation" && pendingListings.length > 0 && (
              <span className="bg-amber-100 text-amber-700 text-[9px] rounded-full px-2 py-0.5">
                {pendingListings.length}
              </span>
            )}
          </button>
        ))}
      </nav>
      <div className="feature-content">
        <div className="mb-6">
          <h2 className="font-semibold text-slate-800">{current.label}</h2>
          <p className="text-xs text-slate-400 mt-2">{current.description}</p>
        </div>
        {activeTab === "moderation" && <ModerationQueue />}
        {activeTab === "published-listings" && <AllListingsManager />}
        {activeTab === "users" && <UserManagement />}
        {activeTab === "finance" && <FinancialDashboard />}
        {activeTab === "overview" && <AdminKpiPanel />}
        {activeTab === "settlements" && <SettlementsPanel />}
        {activeTab === "verifications" && <VerificationQueue />}
        {activeTab === "disputes" && <DisputeDesk />}
      </div>
    </main>
  );
}
