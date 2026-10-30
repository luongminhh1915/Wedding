import { Routes, Route } from "react-router-dom";
import { lazy, Suspense } from "react";
import ProtectedRoute from "./ProtectedRoute";
import WeddingMatchLayout from "../components/WeddingMatchLayout";
import HomePage from "../features/home/HomePage";
import CustomerOnboardingGuard from "./CustomerOnboardingGuard";
import PostLoginRedirect from "./PostLoginRedirect";
import PublicAuthRoute from "./PublicAuthRoute";

const LoginForm = lazy(() => import("../features/auth/components/LoginForm"));
const PasswordResetPage = lazy(() => import("../features/auth/components/PasswordResetPage"));
const RegisterForm = lazy(
  () => import("../features/auth/components/RegisterForm"),
);
const CustomerDashboard = lazy(
  () => import("../features/customer/CustomerDashboard"),
);
const BrowsePage = lazy(() => import("../features/customer/BrowsePage"));
const ListingDetailPage = lazy(
  () => import("../features/customer/ListingDetailPage"),
);
const VendorDashboard = lazy(
  () => import("../features/vendor-portal/VendorDashboard"),
);
const AdminDashboard = lazy(() => import("../features/admin/AdminDashboard"));
const AccountPage = lazy(() => import("../features/account/AccountPage"));
const NotificationsPage = lazy(() => import("../features/notifications/NotificationsPage"));
// Hành trình cặp đôi kiểu Weddy (P1–P6)
const SetupWizard = lazy(() => import("../features/journey/planner/SetupWizard"));
const ComboBuilderPage = lazy(() => import("../features/journey/combo/ComboBuilderPage"));
const ComboDetailPage = lazy(() => import("../features/journey/combo/ComboDetailPage"));
const ArticlesPage = lazy(() => import("../features/journey/ArticlesPage"));
const ArticlePage = lazy(() => import("../features/journey/admin/OperationsPanels").then((m) => ({ default: m.ArticlePage })));
const TemplateGallery = lazy(() => import("../features/journey/invitations/TemplateGallery"));
const InvitationManage = lazy(() => import("../features/journey/invitations/InvitationManage"));
const PublicInvitationPage = lazy(
  () =>
    import("../features/customer/components/invitation/PublicInvitationPage"),
);

export default function AppRoutes() {
  return (
    <Suspense
      fallback={
        <div
          role="status"
          className="min-h-screen grid place-items-center text-sm text-slate-500"
        >
          Đang mở không gian của bạn...
        </div>
      }
    >
      <Routes>
        <Route path="/invitation/:slug" element={<PublicInvitationPage />} />
        <Route element={<WeddingMatchLayout />}>
          <Route path="/reset-password" element={<PasswordResetPage />} />
          <Route element={<ProtectedRoute />}>
            <Route path="/account" element={<AccountPage />} />
            <Route path="/notifications" element={<NotificationsPage />} />
          </Route>
          <Route element={<CustomerOnboardingGuard />}>
          <Route path="/auth/continue" element={<PostLoginRedirect />} />
          <Route path="/" element={<HomePage />} />
          <Route path="/browse" element={<BrowsePage />} />
          <Route path="/vendors" element={<BrowsePage />} />
          <Route path="/listings/:id" element={<ListingDetailPage />} />
          <Route element={<PublicAuthRoute />}>
            <Route path="/login" element={<LoginForm />} />
            <Route path="/register" element={<RegisterForm />} />
          </Route>
          <Route path="/combo" element={<ComboBuilderPage />} />
          <Route path="/combo/:templateId" element={<ComboDetailPage />} />
          <Route path="/cam-nang" element={<ArticlesPage />} />
          <Route path="/wiki" element={<ArticlesPage />} />
          <Route path="/wiki/:slug" element={<ArticlePage />} />
          <Route path="/invitation" element={<TemplateGallery />} />

          {/* Customer Protected Routes */}
          <Route element={<ProtectedRoute allowedRoles={["Customer"]} />}>
            <Route path="/planner" element={<CustomerDashboard />} />
            <Route path="/planner/setup" element={<SetupWizard />} />
            <Route path="/customer/dashboard" element={<CustomerDashboard />} />
            <Route path="/customer/contracts" element={<CustomerDashboard />} />
            <Route path="/thiep/create" element={<CustomerDashboard />} />
            <Route path="/invitation/manage" element={<InvitationManage />} />
            <Route path="/lich-hen" element={<CustomerDashboard />} />
            <Route path="/hop-dong" element={<CustomerDashboard />} />
          </Route>

          {/* Vendor Protected Routes (BR-001) */}
          <Route element={<ProtectedRoute allowedRoles={["VendorOwner"]} />}>
            <Route path="/vendor/dashboard" element={<VendorDashboard />} />
          </Route>

          {/* Admin Back-office Protected Routes */}
          <Route
            element={
              <ProtectedRoute
                allowedRoles={[
                  "Moderator",
                  "Finance",
                  "CustomerCare",
                  "SuperAdmin",
                ]}
              />
            }
          >
            <Route path="/admin/dashboard" element={<AdminDashboard />} />
          </Route>
          </Route>
        </Route>
        <Route path="/:slug" element={<PublicInvitationPage />} />
        <Route path="*" element={<main className="wd wd-page container"><h1>Không tìm thấy trang</h1><a href="/">Về trang chủ</a></main>} />
      </Routes>
    </Suspense>
  );
}
