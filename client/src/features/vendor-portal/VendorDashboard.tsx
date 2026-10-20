import { useSearchParams } from "react-router-dom";
import {
  Building2,
  MessageSquare,
  HeartHandshake,
  ShieldCheck,
  LayoutList,
  CalendarClock,
  FileText,
  BadgeCheck,
  RefreshCw,
  Package,
  LayoutDashboard,
  BookOpen,
} from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { useAuthStore } from "../../store/authStore";
import axiosClient from "../../services/axiosClient";
import ListingTable from "./components/ListingTable";
import { LeadInbox } from "./components/LeadInbox";
import { ContractList } from "./components/ContractList";
import { SettlementDashboard } from "./components/SettlementDashboard";
import { VendorAppointments } from "../journey/vendor/VendorAppointments";
import { VendorQuotes } from "../journey/vendor/VendorQuotes";
import { VendorVerificationPanel } from "../journey/vendor/VendorVerificationPanel";
import { OrderRequestInbox } from "../journey/customerOrders/OrderRequestInbox";
import { VendorPartnershipPolicy } from "./components/VendorPartnershipPolicy";
import { VendorCombosPanel } from "../journey/vendor/VendorCombosPanel";
import { VendorJourneyContext, VendorJourneyGuide, VendorJourneyOverview } from "./VendorJourney";
import { useVendorJourney } from "./hooks/useVendorJourney";
import { VENDOR_JOURNEY_TABS, vendorDashboardTab } from "./vendorJourneyModel";

const tabIcons = {
  overview: LayoutDashboard, guide: BookOpen,
  listings: LayoutList, combos: Package, leads: MessageSquare, quotes: FileText,
  "change-requests": RefreshCw, appointments: CalendarClock,
  "contracts-couple": HeartHandshake, "contracts-admin": ShieldCheck,
  "partnership-policy": FileText, verification: BadgeCheck,
};
const tabs = VENDOR_JOURNEY_TABS.map(tab => ({ ...tab, icon: tabIcons[tab.id] }));

export default function VendorDashboard() {
  const { user } = useAuthStore();
  const [params, setParams] = useSearchParams();
  const activeTab = vendorDashboardTab(params);
  const setActiveTab = (tab: (typeof tabs)[number]["id"]) => setParams(tab === "overview" ? {} : { tab });
  const journey = useVendorJourney(activeTab === "overview" || activeTab === "guide");
  const { data: categories = [] } = useQuery<{ id: string; name: string; requiresCommissionBasisApproval?: boolean }[]>({
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
        {activeTab === "overview" && <VendorJourneyOverview query={journey} />}
        {activeTab === "guide" && <VendorJourneyGuide query={journey} requestedStep={params.get("guideStep")} />}
        <VendorJourneyContext active={activeTab} />
        {activeTab === "listings" && <ListingTable categories={categories} />}
        {activeTab === "combos" && <div className="wd"><VendorCombosPanel /></div>}
        {activeTab === "leads" && <LeadInbox />}
        {activeTab === "quotes" && <VendorQuotes />}
        {activeTab === "change-requests" && <OrderRequestInbox audience="vendor" />}
        {activeTab === "appointments" && <VendorAppointments />}
        {activeTab === "verification" && <VendorVerificationPanel />}
        {activeTab === "contracts-couple" && <ContractList />}
        {activeTab === "contracts-admin" && <SettlementDashboard />}
        {activeTab === "partnership-policy" && <VendorPartnershipPolicy />}
      </div>
    </main>
  );
}
