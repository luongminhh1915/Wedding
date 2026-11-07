import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { useState } from "react";
import {
  ArrowRight,
  CalendarDays,
  CheckCheck,
  Circle,
  FileCheck2,
  Heart,
  Mail,
  MessageCircle,
  MessageSquare,
  Users,
  Wallet,
} from "lucide-react";
import { ChatRoomModal } from "../chat/components/ChatRoomModal";
import { useCustomerOrderPage } from "../journey/customerOrders/customerOrderApi";
import type { Lead } from "../../types/lead.types";
import { useAuthStore } from "../../store/authStore";
import { ContractWorkspace } from "./components/ContractWorkspace";
import { InvitationBuilder } from "./components/invitation/InvitationBuilder";
import { WeddingBudgetPlanner } from "./components/tools/WeddingBudgetPlanner";
import { WeddingChecklist } from "./components/tools/WeddingChecklist";
import { useMyInvitation } from "./hooks/useInvitations";
import { useWeddingBudget, useWeddingChecklist } from "./hooks/useWeddingTools";
import { CustomerJourney } from "./CustomerJourney";
import { usePlannerOverview } from "../journey/api";
import { PremiumUpgrade } from "../journey/planner/PremiumUpgrade";
import { MyAppointments } from "../journey/appointments/MyAppointments";
import { MyCombos } from "../journey/combo/MyCombos";
import { GuestsPlanner } from "../journey/planner/GuestsPlanner";
import { WeddingMembers } from "../journey/planner/WeddingMembers";
import { useMembers } from "../journey/planner/plannerApi";
import InvitationManage from "../journey/invitations/InvitationManage";
import { ContinueIntent } from "../journey/planner/ContinueIntent";
import { CustomerToolNavigation } from "./CustomerToolNavigation";
import CustomerReviewCenter from "./CustomerReviewCenter";
import { customerTab, customerTabHref, type CustomerTab } from "./customerNavigationModel";

export default function CustomerDashboard() {
  const { user } = useAuthStore();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const location = useLocation();
  const activeTab = customerTab(location.pathname, params);
  const selectTab = (tab: CustomerTab) => {
    navigate(customerTabHref(tab, params));
  };
  const { invitation } = useMyInvitation();
  const [today] = useState(() => Date.now());
  const plannerQuery = usePlannerOverview();
  const planner = plannerQuery.data;
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
      <div className="grid min-w-0 items-start gap-5 lg:grid-cols-[230px_minmax(0,1fr)]">
      <CustomerToolNavigation pathname={location.pathname} search={location.search} />
      <div className="feature-content min-w-0">
        <CustomerJourney active={activeTab} data={planner} isLoading={plannerQuery.isPending} isError={plannerQuery.isError} retry={() => void plannerQuery.refetch()} />
        {activeTab === "overview" && (
          <>
            <ContinueIntent />
            <PlannerOverview onSelect={selectTab} />
            <CustomerChatOverview />
            <details className="planner-members" open={params.get("join") ? true : undefined}><summary>Cùng nhau lên kế hoạch · Thành viên & chia sẻ</summary><WeddingMembers /></details>
          </>
        )}
        {activeTab === "appointments" && <MyAppointments />}
        {activeTab === "guests" && <GuestsPlanner />}
        {activeTab === "combos" && <MyCombos />}
        {activeTab === "contracts" && (
          <ContractWorkspace />
        )}
        {activeTab === "reviews" && <CustomerReviewCenter />}
        {activeTab === "invitations" && (
          <>
            <InvitationManage embedded />
            {invitation && <PremiumUpgrade key={String(invitation.isPremium)} isPremium={invitation.isPremium} />}
            <fieldset className="min-w-0" disabled={viewer}><legend className="sr-only">Thiết kế thiệp</legend><InvitationBuilder /></fieldset>
          </>
        )}
        {viewer && ["budget", "checklist", "invitations"].includes(activeTab) && <p role="status" className="p-3 text-sm">Bạn đang xem hồ sơ với quyền chỉ xem.</p>}
        {activeTab === "budget" && <fieldset className="min-w-0" disabled={viewer}><legend className="sr-only">Ngân sách</legend><WeddingBudgetPlanner /></fieldset>}
        {activeTab === "checklist" && <fieldset className="min-w-0" disabled={viewer}><legend className="sr-only">Công việc</legend><WeddingChecklist /></fieldset>}
      </div>
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
            <Link to="/anniversary" className="flex items-center justify-between text-rose-700 hover:text-rose-800 font-semibold text-xs p-2.5 rounded-xl bg-rose-50/70 border border-rose-100 hover:bg-rose-100/70 transition">
              <span className="flex items-center gap-2">
                <Heart size={16} className="text-rose-500 fill-rose-500" />
                Kỷ niệm ngày cưới &amp; Đếm ngày yêu
              </span>
              <ArrowRight size={14} />
            </Link>
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
            src="/wedding-match/be-hy-mascot.png"
            alt="Bé Hý đồng hành cùng hai bạn"
            className="wm-mascot-image"
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


function CustomerChatOverview() {
  const query = useCustomerOrderPage<Lead>("lead", 1, 6, "active");
  const [activeChatLead, setActiveChatLead] = useState<Lead | null>(null);
  const leads = query.data?.items ?? [];
  const activeCount = leads.filter(l => !["Cancelled", "Expired"].includes(l.status)).length;

  return (
    <section className="overview-panel my-5 p-5 md:p-6 rounded-2xl bg-white border border-rose-100 shadow-xs">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-3 border-b border-slate-100 pb-3">
        <div className="flex items-center gap-2.5">
          <div className="w-9 h-9 rounded-xl bg-rose-50 text-rose-600 border border-rose-200/60 flex items-center justify-center">
            <MessageSquare size={18} />
          </div>
          <div>
            <h2 className="serif text-lg font-bold text-slate-800">Box Chat &amp; Tin Nhắn với Nhà Cung Cấp</h2>
            <p className="text-xs text-slate-500">Nhắn tin trực tiếp 1-1, nhận báo giá chi tiết và voucher ưu đãi từ các đối tác cưới.</p>
          </div>
        </div>
        {activeCount > 0 && (
          <span className="px-2.5 py-1 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 text-xs font-semibold flex items-center gap-1.5">
            <span className="inline-block w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
            <span>{activeCount} cuộc trò chuyện sẵn sàng</span>
          </span>
        )}
      </div>

      {query.isLoading ? (
        <div className="py-6 text-center text-xs text-slate-400">Đang tải danh sách cuộc trò chuyện...</div>
      ) : leads.length > 0 ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {leads.map((lead) => (
            <div
              key={lead.id}
              className="p-4 rounded-xl border border-slate-200/80 bg-slate-50/50 hover:bg-white hover:border-rose-300 transition-all flex flex-col justify-between gap-3"
            >
              <div>
                <div className="flex items-start justify-between gap-2">
                  <span className="font-bold text-sm text-slate-900 line-clamp-1">{lead.vendorBrandName}</span>
                  <span className="text-[10px] px-2 py-0.5 rounded-full bg-rose-50 text-rose-700 border border-rose-200 font-semibold shrink-0">
                    {lead.status}
                  </span>
                </div>
                <p className="text-xs text-slate-600 mt-1 line-clamp-1">{lead.listingTitle}</p>
                {lead.voucher?.code && (
                  <div className="mt-2 text-[11px] text-amber-800 font-mono bg-amber-50 px-2 py-0.5 rounded border border-amber-200 inline-block">
                    Voucher: {lead.voucher.code}
                  </div>
                )}
              </div>
              <button
                type="button"
                onClick={() => setActiveChatLead(lead)}
                className="w-full py-2 px-3 rounded-lg bg-rose-600 hover:bg-rose-700 text-white font-semibold text-xs flex items-center justify-center gap-1.5 transition shadow-xs cursor-pointer"
              >
                <MessageCircle size={14} />
                <span>Mở Hộp Chat</span>
              </button>
            </div>
          ))}
        </div>
      ) : (
        <div className="p-6 rounded-xl border border-dashed border-slate-200 text-center bg-slate-50/50">
          <MessageCircle className="w-8 h-8 text-slate-300 mx-auto mb-2" />
          <p className="text-xs font-semibold text-slate-700">Chưa có cuộc trò chuyện nào</p>
          <p className="text-[11px] text-slate-500 mt-1 max-w-md mx-auto">
            Khi bạn chọn "Nhận tư vấn" ở bất kỳ dịch vụ nào trên sàn, hộp chat trực tiếp 1-1 với Nhà cung cấp sẽ tự động mở tại đây để hai bạn trao đổi chi tiết và chốt ưu đãi.
          </p>
          <Link
            to="/browse"
            className="inline-flex items-center gap-1.5 mt-3 px-4 py-2 rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-700 font-semibold text-xs transition border border-rose-200"
          >
            <span>Khám phá dịch vụ cưới</span>
            <ArrowRight size={13} />
          </Link>
        </div>
      )}

      {activeChatLead && (
        <ChatRoomModal
          isOpen={!!activeChatLead}
          onClose={() => setActiveChatLead(null)}
          leadId={activeChatLead.id}
          partnerName={activeChatLead.vendorBrandName}
          isVendorUser={false}
          listingTitle={activeChatLead.listingTitle}
          voucherCode={activeChatLead.voucher?.code}
        />
      )}
    </section>
  );
}
