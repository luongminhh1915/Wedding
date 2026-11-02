import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuthStore } from "../store/authStore";

export default function PublicAuthRoute() {
  const { token, user } = useAuthStore();
  const location = useLocation();
  return token && user ? <Navigate to="/auth/continue" state={location.state} replace /> : <Outlet />;
}
