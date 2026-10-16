import { Navigate, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { useIntentStore } from "../store/intentStore";
import { useOnboardingStatus } from "../features/journey/planner/useOnboarding";
import { isJoinIntent, SETUP_PATH } from "../features/journey/planner/onboardingDecision";
import { usePlannerAction } from "../features/journey/planner/plannerApi";
import { apiError } from "../features/journey/api";

export function OnboardingUnavailable({ retry }: { retry: () => void }) {
  const logout = useAuthStore(x => x.logout); const navigate = useNavigate();
  return <main className="wd wd-page"><section className="container card p-7 max-w-xl text-center" role="alert"><h1 className="serif text-2xl mb-3">Chưa tải được hồ sơ cưới</h1><p className="text-sm muted mb-5">Mình chưa thể xác định tiến độ thiết lập của bạn. Không tạo hồ sơ mới hoặc chuyển trang khi chưa tải được dữ liệu.</p><div className="flex justify-center gap-3"><button className="btn btn-primary" onClick={retry}>Thử lại</button><button className="btn btn-ghost" onClick={() => { logout(); navigate("/login", { replace: true }); }}>Đăng nhập lại</button></div></section></main>;
}
function JoinBeforeOnboarding({ code }: { code: string }) {
  const action = usePlannerAction(); const navigate = useNavigate();
  return <main className="wd wd-page"><section className="container card p-7 max-w-xl"><h1 className="serif text-2xl mb-3">Cùng người ấy lên kế hoạch</h1><p className="text-sm muted mb-5">Bạn có một lời mời dùng chung hồ sơ cưới. Chấp nhận lời mời để dùng kế hoạch có sẵn, hoặc tạo kế hoạch riêng qua 10 câu hỏi.</p>{action.isError && <p role="alert" className="text-red-700 mb-4">{apiError(action.error)}</p>}<div className="flex flex-wrap gap-3"><button className="btn btn-primary" disabled={action.isPending} onClick={() => action.mutate({ path: `join/${encodeURIComponent(code)}` }, { onSuccess: () => { useIntentStore.getState().consume(); navigate("/customer/dashboard", { replace: true }); } })}>{action.isPending ? "Đang tham gia..." : "Chấp nhận lời mời"}</button><button className="btn btn-ghost" onClick={() => navigate(SETUP_PATH, { replace: true })}>Tạo hồ sơ riêng</button></div></section></main>;
}
export default function CustomerOnboardingGuard() {
  const user = useAuthStore(x => x.user); const token = useAuthStore(x => x.token); const status = useOnboardingStatus(); const location = useLocation();
  if (!token || user?.role !== "Customer" || ["/login", "/register", "/auth/continue"].includes(location.pathname)) return <Outlet />;
  if (status.isPending || status.isFetching && !status.data) return <main className="wd wd-page text-center" role="status">Đang kiểm tra kế hoạch cưới của bạn...</main>;
  if (status.isError || !status.data) return <OnboardingUnavailable retry={() => void status.refetch()} />;
  if (status.data.completed || location.pathname === SETUP_PATH) return <Outlet />;
  const path = location.pathname + location.search;
  if (isJoinIntent(path)) return <JoinBeforeOnboarding code={new URLSearchParams(location.search).get("join")!} />;
  return <Navigate to={SETUP_PATH} state={{ from: path }} replace />;
}
