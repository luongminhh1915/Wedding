import { useEffect } from "react";
import {
  Link,
  NavLink,
  Outlet,
  useLocation,
  useNavigate,
} from "react-router-dom";
import {
  ArrowUpRight,
  BookOpen,
  BriefcaseBusiness,
  ChevronDown,
  Heart,
  House,
  LogOut,
  PanelsTopLeft,
  Star,
  FileCheck2,
  UserRound,
  Package,
  MessageSquare,
  ShieldCheck,
  Wallet,
  Gavel,
  Users,
} from "lucide-react";
import { useAuthStore } from "../store/authStore";
import { safeAnchorId } from "../utils/anchorNavigation";
import NotificationBell from "./NotificationBell";
import ComboCartLink from "./ComboCartLink";
import "../styles/combo-builder.css";
import { useIntentStore } from "../store/intentStore";
import { CustomerNavigationGroups } from "../features/customer/CustomerToolNavigation";
import { customerMobileSection, isCustomerWorkspace } from "../features/customer/customerNavigationModel";
import { actorWorkspaceNavigation, usesCustomerNavigation } from "./actorNavigationModel";

const actorNavigationIcons = {
  overview: PanelsTopLeft, listings: Star, combos: Package, leads: MessageSquare,
  moderation: ShieldCheck, reviews: Star, "published-listings": Star,
  finance: Wallet, settlements: FileCheck2, disputes: Gavel,
  emergency: ShieldCheck, users: Users, account: UserRound, cms: BookOpen,
};

export function ActorWorkspaceLinks({ role, pathname, search = "", mobile = false }: {
  role: unknown; pathname: string; search?: string; mobile?: boolean;
}) {
  return <>{actorWorkspaceNavigation(role, pathname, search, mobile).map(item => {
    const Icon = actorNavigationIcons[item.id as keyof typeof actorNavigationIcons];
    return <Link key={item.id} to={item.to} className={item.active ? "active" : undefined}
      aria-current={item.active ? "page" : undefined}>
      {mobile && <Icon size={21} aria-hidden="true" />}
      {mobile ? <span>{item.mobileLabel}</span> : item.label}
    </Link>;
  })}</>;
}

export function WeddingMatchLogo({ light = false }: { light?: boolean }) {
  return (
    <Link
      to="/"
      className={`wm-logo${light ? " is-light" : ""}`}
      aria-label="Wedding Match — Trang chủ"
    >
      <img src="/wedding-match/brand-mark.svg" alt="" width="44" height="44" />
      <span className="brand-wordmark">
        WEDDING <strong>MATCH</strong>
      </span>
    </Link>
  );
}

export function WeddingMatchHeader() {
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
        {usesCustomerNavigation(user?.role) ? <>
        <NavLink to="/" end>
          Trang chủ
        </NavLink>
        <NavLink to="/combo">Combo cưới</NavLink>
        <details
          className="wm-nav-menu"
          data-active={isCustomerWorkspace(location.pathname)}
        >
          <summary>
            Wedding Planner <ChevronDown size={13} />
          </summary>
          <div className="wm-dropdown !w-72 max-h-[calc(100dvh-120px)] max-w-[calc(100vw-32px)] overflow-y-auto">
            <CustomerNavigationGroups pathname={location.pathname} search={location.search} compact />
          </div>
        </details>
        <NavLink to="/cam-nang">Cẩm nang</NavLink>
        <Link className="wm-about-nav" to="/#cong-cu">
          Về Wedding Match
        </Link>
        <Link className="wm-partner-nav" to="/register?role=vendor">
          Dành cho NCC <ArrowUpRight size={13} aria-hidden="true" />
        </Link>
        </> : <ActorWorkspaceLinks role={user?.role} pathname={location.pathname} search={location.search} />}
      </nav>
      <div className="wm-header-actions">
        <ComboCartLink />
        {token && user ? (
          <>
            <NotificationBell />
            <Link
              className="wm-account"
              to="/account"
              aria-label={`Tài khoản ${user.fullName}`}
            >
              <span>{user.fullName.slice(0, 1)}</span>
              <span className="account-name">
                {user.vendorBrandName || user.fullName}
              </span>
            </Link>
            <button
              className="wm-logout"
              aria-label="Đăng xuất"
              onClick={() => {
                logout();
                navigate("/login", { replace: true });
              }}
            >
              <LogOut size={17} />
            </button>
          </>
        ) : (
          <button type="button" className="wm-login" onClick={() => { useIntentStore.getState().remember(location.pathname + location.search); navigate("/login", { state: { from: location.pathname + location.search } }); }}>
            Đăng nhập
          </button>
        )}
      </div>
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

export function MobileNavigation() {
  const { user } = useAuthStore();
  const { pathname, search, hash } = useLocation();
  if (!usesCustomerNavigation(user?.role)) return (
    <nav className="wm-mobile-nav" aria-label="Điều hướng chính">
      <ActorWorkspaceLinks role={user?.role} pathname={pathname} search={search} />
    </nav>
  );
  const customerActive = customerMobileSection(pathname);
  const items = [
    { to: "/", label: "Trang chủ", icon: House, active: pathname === "/" && !hash },
    { to: "/combo", label: "Combo cưới", icon: Star, active: customerActive === "combo" },
    {
      to: "/customer/dashboard",
      label: "Wedding Planner",
      icon: PanelsTopLeft,
      active: customerActive === "planner",
    },
    { to: "/cam-nang", label: "Cẩm nang", icon: BookOpen, active: pathname === "/cam-nang" || pathname.startsWith("/cam-nang/") },
    { to: "/#cong-cu", label: "Về Wedding Match", icon: Heart, active: pathname === "/" && hash === "#cong-cu" },
    { to: "/register?role=vendor", label: "Dành cho NCC", icon: BriefcaseBusiness, active: pathname === "/register" && search.includes("role=vendor") },
  ];
  return (
    <nav className="wm-mobile-nav" aria-label="Điều hướng chính">
      {items.map(({ to, label, icon: Icon, active }) => (
        <Link
          key={label}
          to={to}
          className={active ? "active" : ""}
          aria-current={active ? "page" : undefined}
        >
          <Icon size={17} aria-hidden="true" />
          <span>{label}</span>
        </Link>
      ))}
    </nav>
  );
}

export default function WeddingMatchLayout() {
  const { pathname, hash } = useLocation();
  useEffect(() => {
    const anchorId = safeAnchorId(hash);
    if (anchorId)
      document
        .getElementById(anchorId)
        ?.scrollIntoView({ behavior: "smooth" });
    else if (!hash) window.scrollTo(0, 0);
  }, [pathname, hash]);
  return (
    <div className="wm-theme">
      <a href="#main-content" className="skip-link">
        Đến nội dung chính
      </a>
      <WeddingMatchHeader />
      <MobileNavigation />
      <div id="main-content">
        <Outlet />
      </div>
      <WeddingMatchFooter />
    </div>
  );
}
