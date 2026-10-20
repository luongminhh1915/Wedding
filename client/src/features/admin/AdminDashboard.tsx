import { useSearchParams } from "react-router-dom";
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
  RefreshCw,
} from "lucide-react";
import {
  AdminKpiPanel,
  DisputeDesk,
  VerificationQueue,
} from "../journey/admin/AdminJourneyPanels";
import { SettlementsPanel } from "../journey/admin/FinancialLedgerPanel";
import { ArticleCms, EmergencyMatchingPanel } from "../journey/admin/OperationsPanels";
import { ComboAdministration } from "../journey/admin/ComboAdministration";
import { useAuthStore } from "../../store/authStore";
import ModerationQueue from "./ModerationQueue";
import UserManagement from "./UserManagement";
import AllListingsManager from "./AllListingsManager";
import FinancialDashboard from "./FinancialDashboard";
import { useModerationQueue } from "./useModerationQueue";
import { OrderRequestInbox } from "../journey/customerOrders/OrderRequestInbox";

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
    description: "Hàng chờ chung cho bài giới thiệu, dịch vụ thành phần và combo của nhà cung cấp.",
    roles: ["SuperAdmin", "Moderator"],
  },
  {
    id: "published-listings",
    label: "Gói dịch vụ",
    icon: Layers,
    description: "Theo dõi và quản lý các gói dịch vụ trên nền tảng.",
    roles: ["SuperAdmin", "Moderator"],
  },
  {
    id: "users",
    label: "Người dùng & đối tác",
    icon: Users,
    description: "Kiểm tra thông tin và quản lý trạng thái tài khoản.",
    roles: ["SuperAdmin"],
  },
  {
    id: "finance",
    label: "Tài chính & đối soát",
    icon: DollarSign,
    description: "Tổng hợp hợp đồng, tiền cọc và hoa hồng của nhà cung cấp.",
    roles: ["SuperAdmin", "Finance"],
  },
  {
    id: "settlements",
    label: "Bảng kê tháng",
    icon: Receipt,
    description: "Bảng kê, giao dịch ngân hàng, hàng chờ đối soát và hóa đơn có chứng từ thật.",
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
    roles: ["SuperAdmin", "CustomerCare", "Finance"],
  },
  { id: "change-requests", label: "Yêu cầu thay đổi / hủy", icon: RefreshCw, description: "Tiếp nhận đề nghị sau khi NCC phản hồi; không tự thay hợp đồng hoặc hoàn tiền.", roles: ["SuperAdmin", "Finance"] },
  { id: "cms", label: "Cẩm nang CMS", icon: FileCheck, description: "Tạo, chỉnh sửa, xuất bản và ghim dịch vụ vào bài viết.", roles: ["SuperAdmin", "Moderator"] },
  { id: "combos", label: "Combo & ưu đãi", icon: Layers, description: "Mẫu combo, duyệt dịch vụ và cấu hình giảm giá.", roles: ["SuperAdmin", "Moderator"] },
  { id: "emergency", label: "Hỗ trợ khẩn cấp", icon: ShieldCheck, description: "Tạo yêu cầu NCC thay thế cho hợp đồng đang tranh chấp.", roles: ["SuperAdmin", "CustomerCare"] },
] as const;

export default function AdminDashboard() {
  const { user } = useAuthStore();
  const [params, setParams] = useSearchParams();
  const visibleTabs = tabs.filter(
    (tab) => !("roles" in tab) || (tab.roles as readonly string[]).includes(user?.role ?? ""),
  );
  const activeTab = visibleTabs.find(tab => tab.id === params.get("tab"))?.id ?? "overview";
  const setActiveTab = (tab: (typeof tabs)[number]["id"]) => setParams(tab === "overview" ? {} : { tab });
  const { data: moderation } = useModerationQueue({ type: "all", status: "Pending", search: "", page: 1, pageSize: 1 });
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
            {id === "moderation" && (moderation?.pendingCount ?? 0) > 0 && (
              <span className="bg-amber-100 text-amber-700 text-[9px] rounded-full px-2 py-0.5">
                {moderation?.pendingCount}
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
        {activeTab === "change-requests" && <OrderRequestInbox audience="finance" />}
        {activeTab === "cms" && <ArticleCms />}
        {activeTab === "emergency" && <EmergencyMatchingPanel />}
        {activeTab === "combos" && <ComboAdministration />}
      </div>
    </main>
  );
}
