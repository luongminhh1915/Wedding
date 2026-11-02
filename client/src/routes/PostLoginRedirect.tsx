import { useEffect, useState } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { useIntentStore } from "../store/intentStore";
import { useOnboardingStatus } from "../features/journey/planner/useOnboarding";
import { afterLoginDestination, safeIntent } from "../features/journey/planner/onboardingDecision";
import { OnboardingUnavailable } from "./CustomerOnboardingGuard";
import { roleAwareReturnPath } from "../features/auth/authNavigation";

export default function PostLoginRedirect() {
  const { user, token } = useAuthStore(); const location = useLocation(); const query = useOnboardingStatus();
  const [intent] = useState(() => roleAwareReturnPath(user?.role, safeIntent((location.state as { from?: string } | null)?.from) ?? useIntentStore.getState().returnTo));
  const ready = user?.role !== "Customer" || query.isSuccess;
  useEffect(() => { if (ready) { useIntentStore.getState().consume(); if (user?.role === "Customer" && !query.data?.completed && intent) useIntentStore.getState().remember(intent); } }, [ready, user?.role, query.data?.completed, intent]);
  if (!user || !token) return <Navigate to="/login" replace />;
  if (user.role === "Customer" && query.isError) return <OnboardingUnavailable retry={() => void query.refetch()} />;
  if (!ready) return <main className="wd wd-page text-center" role="status">Đăng nhập thành công. Đang mở không gian của bạn...</main>;
  return <Navigate to={afterLoginDestination(user.role, query.data?.completed === true, intent)} replace />;
}
