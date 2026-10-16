import { Link, useLocation, useSearchParams } from "react-router-dom";
import { useState } from "react";
import {
  ArrowRight,
  CalendarDays,
  CheckCheck,
  Circle,
  Compass,
  FileCheck2,
  Heart,
  LayoutDashboard,
  Mail,
  CalendarClock,
  PackageCheck,
  Users,
  Wallet,
} from "lucide-react";
import { useAuthStore } from "../../store/authStore";
import { MyContracts } from "./components/MyContracts";
import { CustomerLeads } from "./components/CustomerLeads";
import { InvitationBuilder } from "./components/invitation/InvitationBuilder";
import { WeddingBudgetPlanner } from "./components/tools/WeddingBudgetPlanner";
import { WeddingChecklist } from "./components/tools/WeddingChecklist";
import { useMyInvitation } from "./hooks/useInvitations";
import { useWeddingBudget, useWeddingChecklist } from "./hooks/useWeddingTools";
import { PlannerSnapshot } from "../journey/planner/PlannerSnapshot";
import { usePlannerOverview } from "../journey/api";
import { PremiumUpgrade } from "../journey/planner/PremiumUpgrade";
import { MyAppointments } from "../journey/appointments/MyAppointments";
import { MyCombos } from "../journey/combo/MyCombos";
import { MyQuotes } from "../journey/quotes/MyQuotes";
import { CustomerDisputes } from "../journey/quotes/CustomerDisputes";
import { GuestsPlanner } from "../journey/planner/GuestsPlanner";
import { WeddingMembers } from "../journey/planner/WeddingMembers";
import { useMembers } from "../journey/planner/plannerApi";
import InvitationManage from "../journey/invitations/InvitationManage";
import { ContinueIntent } from "../journey/planner/ContinueIntent";

const tabs = [
  { id: "overview", label: "Tổng quan", icon: LayoutDashboard },
  { id: "checklist", label: "Công việc", icon: CheckCheck },
  { id: "budget", label: "Ngân sách", icon: Wallet },
  { id: "guests", label: "Khách & xếp bàn", icon: Users },
  { id: "invitations", label: "Thiệp & RSVP", icon: Heart },
  { id: "appointments", label: "Lịch hẹn", icon: CalendarClock },
  { id: "combos", label: "Combo", icon: PackageCheck },
  { id: "contracts", label: "Hợp đồng & tư vấn", icon: FileCheck2 },
] as const;
type CustomerTab = (typeof tabs)[number]["id"];

export default function CustomerDashboard() {
  const { user } = useAuthStore();
  const [params, setParams] = useSearchParams();
  const location = useLocation();
  const requestedTab =
    params.get("tab") ||
    (location.pathname.endsWith("/contracts") || location.pathname === "/hop-dong" ? "contracts" : location.pathname === "/lich-hen" ? "appointments" : location.pathname.startsWith("/thiep/") ? "invitations" : "overview");
  const activeTab = tabs.some((tab) => tab.id === requestedTab)
    ? (requestedTab as CustomerTab)
    : "overview";
  const selectTab = (tab: CustomerTab) =>
    setParams(tab === "overview" ? {} : { tab });
  const { invitation } = useMyInvitation();
  const [today] = useState(() => Date.now());
  const { data: planner } = usePlannerOverview();
  const profile = planner?.profile;
  const members = useMembers();
  const viewer = members.data?.myRole === "Viewer";
  const dateIso = profile?.weddingDate || invitation?.eventDate;
  const date = dateIso ? new Date(dateIso) : null;
  const coupleName =
    profile?.groomName && profile?.brideName
      ? `${profile.groomName} & ${profile.brideName}`
      : invitation
        ? `${invitation.groomName} & ${invitation.brideName}`
        : user?.fullName && user.fullName !== "Cặp đôi mới"
          ? user.fullName
          : "Ngày cưới của chúng mình";
  const daysLeft = date
    ? Math.max(0, Math.ceil((date.getTime() - today) / 86400000))
    : null;

  return (
    <main className="planner-shell">
      <div className="planner-heading">
        <div>
          <h1>
            <em>
              {coupleName}
            </em>{" "}
            <Heart
              className="inline ml-2 text-rose-300"
              size={22}
              strokeWidth={1.3}
            />
          </h1>
          <p>Mỗi điều nhỏ hôm nay, một ngày hạnh phúc mai sau.</p>
        </div>
        <button
          onClick={() => selectTab("invitations")}
          className="wedding-date-badge"
        >
          <CalendarDays size={17} />
          {date ? (
            <>
              <span>{date.toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })}</span>
              <span>· Còn {daysLeft} ngày</span>
            </>
          ) : (
            "Thêm ngày cưới của hai bạn"
          )}
        </button>
      </div>
      <nav className="planner-tabs" aria-label="Công cụ của cặp đôi">
        {tabs.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            className={activeTab === id ? "active" : ""}
            onClick={() => selectTab(id)}
            aria-current={activeTab === id ? "page" : undefined}
          >
            <Icon size={17} />
            {label}
          </button>
        ))}
        <Link to="/browse">
          <Compass size={17} />
          Tìm dịch vụ
        </Link>
      </nav>
      <div className="feature-content">
        {activeTab === "overview" && (
          <>
            <ContinueIntent />
            <WeddingMembers />
            <PlannerSnapshot />
            <PlannerOverview onSelect={selectTab} />
          </>
        )}
        {activeTab === "appointments" && <MyAppointments />}
        {activeTab === "guests" && <GuestsPlanner />}
        {activeTab === "combos" && <MyCombos />}
        {activeTab === "contracts" && (
          <>
            <CustomerLeads />
            <MyQuotes />
            <MyContracts />
            <CustomerDisputes />
          </>
        )}
        {activeTab === "invitations" && (
          <>
            <InvitationManage embedded />
            {invitation && <PremiumUpgrade key={String(invitation.isPremium)} isPremium={invitation.isPremium} />}
            <fieldset disabled={viewer}><legend className="sr-only">Thiết kế thiệp</legend><InvitationBuilder /></fieldset>
          </>
        )}
        {viewer && ["budget", "checklist", "invitations"].includes(activeTab) && <p role="status" className="p-3 text-sm">Bạn đang xem hồ sơ với quyền chỉ xem.</p>}
        {activeTab === "budget" && <fieldset disabled={viewer}><legend className="sr-only">Ngân sách</legend><WeddingBudgetPlanner /></fieldset>}
        {activeTab === "checklist" && <fieldset disabled={viewer}><legend className="sr-only">Công việc</legend><WeddingChecklist /></fieldset>}
      </div>
    </main>
  );
}

function PlannerOverview({
  onSelect,
}: {
  onSelect: (tab: CustomerTab) => void;
}) {
  const budget = useWeddingBudget();
  const checklist = useWeddingChecklist();
  const { invitation, isError: invitationError } = useMyInvitation();
  const completed = checklist.tasks.filter((task) => task.isCompleted).length;
  const percentage = checklist.tasks.length
    ? Math.round((completed / checklist.tasks.length) * 100)
    : 0;
  const upcoming = checklist.tasks
    .filter((task) => !task.isCompleted)
    .slice(0, 4);
  const money = (value: number | undefined) =>
    value === undefined
      ? "—"
      : new Intl.NumberFormat("vi-VN", {
          notation: "compact",
          maximumFractionDigits: 1,
        }).format(value) + " đ";

  return (
    <>
      {(budget.isError || checklist.isError || invitationError) && (
        <div
          role="alert"
          className="mb-5 p-4 rounded-2xl bg-amber-50 text-amber-800 text-xs"
        >
          Một số dữ liệu chưa tải được. Vui lòng kiểm tra kết nối và tải lại
          trang.
        </div>
      )}
      <div className="planner-overview">
        <div className="overview-main">
          <div className="overview-stats">
            <button
              className="overview-stat"
              onClick={() => onSelect("budget")}
            >
              <span className="overview-stat-head">
                Ngân sách <Wallet />
              </span>
              <strong>{money(budget.budget?.remainingBudget)}</strong>
              <small>Ngân sách dự kiến còn lại</small>
            </button>
            <button
              className="overview-stat"
              onClick={() => onSelect("invitations")}
            >
              <span className="overview-stat-head">
                Khách mời <Users />
              </span>
              <strong>
                {invitationError ? "—" : (invitation?.attendingCount ?? 0)}
              </strong>
              <small>Đã phản hồi tham dự</small>
            </button>
            <button
              className="overview-stat"
              onClick={() => onSelect("checklist")}
            >
              <span className="overview-stat-head">
                Việc đã xong <CheckCheck />
              </span>
              <strong>
                {checklist.isLoading || checklist.isError
                  ? "—"
                  : `${completed}/${checklist.tasks.length}`}
              </strong>
              <small>Từng bước đến ngày hạnh phúc</small>
            </button>
          </div>
          <section className="overview-panel">
            <div className="overview-panel-title">
              <h2>Việc cần làm</h2>
              <button onClick={() => onSelect("checklist")}>
                Xem tất cả <ArrowRight size={12} />
              </button>
            </div>
            <p>Mọi đầu việc, gọn gàng trong một nơi.</p>
            <div className="overview-progress">
              <div className="progress-track">
                <span style={{ width: `${percentage}%` }} />
              </div>
              <span>{percentage}%</span>
            </div>
            {checklist.isLoading ? (
              <div className="overview-empty">Đang tải công việc...</div>
            ) : upcoming.length ? (
              upcoming.map((task) => (
                <div className="overview-task" key={task.id}>
                  <Circle size={18} />
                  <span>
                    {task.title}
                    <small>
                      {task.dueDate
                        ? new Date(task.dueDate).toLocaleDateString("vi-VN")
                        : "Trong kế hoạch của hai bạn"}
                    </small>
                  </span>
                </div>
              ))
            ) : (
              <div className="overview-empty">
                <CheckCheck size={30} />
                <p>
                  {checklist.tasks.length
                    ? "Hai bạn đã hoàn thành mọi công việc hiện có!"
                    : "Thêm công việc đầu tiên cho ngày trọng đại nhé."}
                </p>
                <button
                  className="wm-button small"
                  onClick={() => onSelect("checklist")}
                >
                  Mở checklist <ArrowRight size={13} />
                </button>
              </div>
            )}
          </section>
          <div className="overview-shortcuts">
            <button onClick={() => onSelect("invitations")}>
              <span className="flex items-center gap-2">
                <Mail size={17} />
                Thiết kế thiệp & xem RSVP
              </span>
              <ArrowRight size={15} />
            </button>
            <button onClick={() => onSelect("contracts")}>
              <span className="flex items-center gap-2">
                <FileCheck2 size={17} />
                Hợp đồng, voucher & trò chuyện
              </span>
              <ArrowRight size={15} />
            </button>
          </div>
        </div>
        <aside className="overview-panel overview-milo">
          <span className="wm-eyebrow">Gợi ý dành cho bạn</span>
          <img
            src="/wedding-match/milo.png"
            alt="Milo đồng hành cùng hai bạn"
          />
          <h2>
            Từ từ thôi,
            <br />
            mình cùng lo!
          </h2>
          <p>
            Một kế hoạch nhỏ mỗi ngày. Hai bạn đã sẵn sàng tìm người đồng hành
            cho ngày trọng đại chưa?
          </p>
          <Link to="/browse" className="wm-button">
            Tìm dịch vụ phù hợp <ArrowRight size={14} />
          </Link>
        </aside>
      </div>
    </>
  );
}
