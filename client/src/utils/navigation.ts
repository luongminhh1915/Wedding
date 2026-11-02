import type { User } from "../types/auth";
import { actorDashboard } from "../features/auth/authNavigation";

export function dashboardPath(user: User | null) {
  return user ? actorDashboard(user.role) : "/login";
}
