import { useEffect, useState } from "react";
import {
  Link,
  NavLink,
  Outlet,
  useLocation,
  useNavigate,
} from "react-router-dom";
import {
  ArrowUpRight,
  ChevronDown,
  Heart,
  House,
  LayoutDashboard,
  LogOut,
  PanelsTopLeft,
  Star,
  Wallet,
  CheckCheck,
  FileCheck2,
  KeyRound,
} from "lucide-react";
import { useAuthStore } from "../store/authStore";
import { dashboardPath } from "../utils/navigation";
import AuthModal from "../features/auth/components/AuthModal";
import ChangePasswordDialog from "../features/auth/components/ChangePasswordDialog";
import NotificationBell from "./NotificationBell";
import ComboCartLink from "./ComboCartLink";
import "../styles/combo-builder.css";
import { useIntentStore } from "../store/intentStore";

export function WeddingMatchLogo({ light = false }: { light?: boolean }) {
  return (
    <Link
      to="/"
      className={`wm-logo${light ? " is-light" : ""}`}
      aria-label="Wedding Match — Trang chủ"
    >
      <img src="/wedding-match/mark.svg" alt="" width="36" height="36" />
      <span className="brand-wordmark">
        WEDDING <strong>MATCH</strong>
      </span>
    </Link>
  );
}

export function WeddingMatchHeader() {
  const [authOpen, setAuthOpen] = useState(false);
  const [securityToken, setSecurityToken] = useState<string | null>(null);
  const { user, token, logout } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  useEffect(() => {
    document
      .querySelectorAll<HTMLDetailsElement>(".wm-nav-menu[open]")
      .forEach((menu) => {
        menu.open = false;
      });
  }, [location.pathname, location.search]);

  return (
    <header className="wm-header">
      <WeddingMatchLogo light />
      <nav className="wm-desktop-nav" aria-label="Điều hướng chính">
        <NavLink to="/" end>
          Trang chủ
        </NavLink>
        {user && user.role !== "Customer" ? <NavLink to={dashboardPath(user)}>{user.role === "VendorOwner" ? "Quản lý nhà cung cấp" : "Không gian quản trị"}</NavLink> : <details
          className="wm-nav-menu"
          data-active={
            location.pathname.startsWith("/customer/") &&
            !location.search.includes("tab=invitations")
          }
        >
          <summary>
            Planner <ChevronDown size={13} />
          </summary>
          <div className="wm-dropdown">
            <Link to="/customer/dashboard">
              <LayoutDashboard size={17} /> Tổng quan kế hoạch
            </Link>
            <Link to="/customer/dashboard?tab=checklist">
              <CheckCheck size={17} /> Checklist cưới
            </Link>
            <Link to="/customer/dashboard?tab=budget">
              <Wallet size={17} /> Ngân sách cưới
            </Link>
            <Link to="/customer/dashboard?tab=contracts">
              <FileCheck2 size={17} /> Hợp đồng & tư vấn
            </Link>
          </div>
        </details>}
        {(!user || user.role === "Customer") && <Link
          className={
            location.pathname.startsWith("/customer/") &&
            location.search.includes("tab=invitations")
              ? "active"
              : ""
          }
          to="/invitation"
        >
          Thiệp mời <ChevronDown size={13} />
        </Link>}
        <NavLink to="/browse">Dịch vụ</NavLink>
        <NavLink to="/combo">Combo cưới</NavLink>
        <NavLink to="/cam-nang">Cẩm nang</NavLink>
      </nav>
      <div className="wm-header-actions">
        <ComboCartLink />
        {token && user ? (
          <>
            <NotificationBell />
            <Link
              className="wm-account"
              to={dashboardPath(user)}
              aria-label={`Tài khoản ${user.fullName}`}
            >
              <span>{user.fullName.slice(0, 1)}</span>
              <span className="account-name">
                {user.vendorBrandName || user.fullName}
              </span>
            </Link>
            <button type="button" className="wm-logout" aria-label="Bảo mật và liên hệ" title="Bảo mật và số liên hệ" onClick={() => setSecurityToken(token)}><KeyRound size={17} /></button>
            <button
              className="wm-logout"
              aria-label="Đăng xuất"
              onClick={() => {
                logout();
                setAuthOpen(false);
                navigate("/login", { replace: true });
              }}
            >
              <LogOut size={17} />
            </button>
          </>
        ) : (
          <button type="button" className="wm-login" onClick={() => { useIntentStore.getState().remember(location.pathname + location.search); setAuthOpen(true); }}>
            Đăng nhập
          </button>
        )}
      </div>
      {authOpen && <AuthModal onClose={() => setAuthOpen(false)} />}
      {securityToken === token && token && user && <ChangePasswordDialog key={token} onClose={() => setSecurityToken(null)} />}
    </header>
  );
}

export function WeddingMatchFooter() {
  return (
    <footer className="wm-footer">
      <div className="wm-footer-top">
        <div className="wm-footer-brand">
          <span className="wm-eyebrow">Craft your perfect day</span>
          <WeddingMatchLogo />
          <p>
            Mọi điều cần cho một ngày cưới trọn vẹn. Cùng hai bạn lên kế hoạch,
            kết nối dịch vụ và lưu giữ những khoảnh khắc đáng nhớ.
          </p>
          <span className="made-with">
            MADE WITH <Heart size={11} fill="currentColor" /> IN VIETNAM
          </span>
        </div>
        <div>
          <h3>Khám phá</h3>
          <Link to="/">Về Wedding Match</Link>
          <Link to="/customer/dashboard">Công cụ Planner</Link>
          <Link to="/browse">Dịch vụ cưới</Link>
          <Link to="/customer/dashboard?tab=invitations">
            Thiệp cưới online
          </Link>
        </div>
        <div>
          <h3>Dành cho bạn</h3>
          <Link to="/customer/dashboard?tab=checklist">
            Checklist chuẩn bị cưới
          </Link>
          <Link to="/customer/dashboard?tab=budget">Quản lý ngân sách</Link>
          <Link to="/customer/dashboard?tab=contracts">Hợp đồng & tư vấn</Link>
          <Link to="/register?role=vendor">
            Trở thành đối tác <ArrowUpRight size={13} />
          </Link>
        </div>
        <div className="footer-note">
          <h3>Một khởi đầu đẹp</h3>
          <p>
            Ngày cưới của hai bạn,
            <br />
            theo cách của hai bạn.
          </p>
          <Link to="/register" className="wm-button small">
            Bắt đầu miễn phí <ArrowUpRight size={15} />
          </Link>
        </div>
      </div>
      <div className="wm-footer-bottom">
        <span>© {new Date().getFullYear()} WEDDING MATCH</span>
        <span>Được tạo nên bằng sự yêu thương.</span>
      </div>
    </footer>
  );
}

function MobileNavigation() {
  const { user } = useAuthStore();
  const { pathname, search } = useLocation();
  const tab = new URLSearchParams(search).get("tab");
  const items = user && user.role !== "Customer" ? [
    { to: "/", label: "Trang chủ", icon: House, active: pathname === "/" },
    { to: dashboardPath(user), label: user.role === "VendorOwner" ? "Quản lý NCC" : "Quản trị", icon: PanelsTopLeft, active: pathname === dashboardPath(user) },
    { to: "/browse", label: "Dịch vụ", icon: Star, active: pathname === "/browse" || pathname.startsWith("/listings") },
    { to: "/cam-nang", label: "Cẩm nang", icon: FileCheck2, active: pathname === "/cam-nang" },
  ] : [
    { to: "/", label: "Trang chủ", icon: House, active: pathname === "/" },
    {
      to: "/customer/dashboard",
      label: "Kế hoạch",
      icon: PanelsTopLeft,
      active: pathname.startsWith("/customer") && tab !== "invitations",
    },
    {
      to: "/customer/dashboard?tab=invitations",
      label: "Thiệp",
      icon: Heart,
      active: tab === "invitations",
    },
    {
      to: "/browse",
      label: "Dịch vụ",
      icon: Star,
      active: pathname === "/browse" || pathname.startsWith("/listings"),
    },
    {
      to: user ? dashboardPath(user) : "/login",
      label: "Tài khoản",
      icon: LayoutDashboard,
      active:
        pathname.includes("/admin") ||
        pathname.includes("/vendor") ||
        pathname === "/login",
    },
  ];
  return (
    <nav className="wm-mobile-nav" aria-label="Điều hướng di động">
      {items.map(({ to, label, icon: Icon, active }) => (
        <Link
          key={label}
          to={to}
          className={active ? "active" : ""}
          aria-current={active ? "page" : undefined}
        >
          <Icon size={21} />
          <span>{label}</span>
        </Link>
      ))}
    </nav>
  );
}

export default function WeddingMatchLayout() {
  const { pathname, hash } = useLocation();
  useEffect(() => {
    if (hash)
      document
        .getElementById(decodeURIComponent(hash.slice(1)))
        ?.scrollIntoView({ behavior: "smooth" });
    else window.scrollTo(0, 0);
  }, [pathname, hash]);
  return (
    <div className="wm-theme">
      <a href="#main-content" className="skip-link">
        Đến nội dung chính
      </a>
      <WeddingMatchHeader />
      <div id="main-content">
        <Outlet />
      </div>
      <WeddingMatchFooter />
      <MobileNavigation />
    </div>
  );
}
