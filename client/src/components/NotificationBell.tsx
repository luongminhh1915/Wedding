import { Bell } from "lucide-react";
import { Link } from "react-router-dom";
import { useNotifications } from "../features/notifications/notificationApi";
import "../features/notifications/notifications.css";
export default function NotificationBell() {
  const query = useNotifications(1, 1);
  const unread = query.data?.unreadCount ?? 0;
  return <Link to="/notifications" className="wm-notification-link" aria-label={`Thông báo${unread ? `, ${unread} chưa đọc` : ""}`}>
    <Bell size={19} aria-hidden="true" />
    {unread > 0 && <span className="wm-notification-count">{unread > 99 ? "99+" : unread}</span>}
    {query.isError && <span className="wm-notification-unavailable" aria-label="Chưa tải được số thông báo">!</span>}
  </Link>;
}
