import { Link, useSearchParams } from "react-router-dom";
import { useIntentStore } from "../../../store/intentStore";
import { safeIntent, OVERVIEW_PATH, SETUP_PATH } from "./onboardingDecision";
export function ContinueIntent() {
  const [params] = useSearchParams(); const stored = useIntentStore(x => x.returnTo); const path = safeIntent(stored);
  if (!params.has("welcome") || !path || ["/", "/planner", SETUP_PATH, OVERVIEW_PATH].includes(path)) return null;
  return <section className="wd card p-5 mb-5"><h2 className="serif text-xl">Kế hoạch của hai bạn đã sẵn sàng!</h2><p className="text-sm muted mt-2">35 công việc và 14 khoản ngân sách đã được lưu. Bạn có thể xem Tổng quan trước hoặc quay lại trang đang xem khi đăng nhập.</p><Link className="btn btn-primary mt-3" to={path} onClick={() => useIntentStore.getState().consume()}>Tiếp tục việc trước đó</Link></section>;
}
