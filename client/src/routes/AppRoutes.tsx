import { Routes, Route } from "react-router-dom";
import { lazy, Suspense } from "react";
import ProtectedRoute from "./ProtectedRoute";
import WeddingMatchLayout from "../components/WeddingMatchLayout";
import HomePage from "../features/home/HomePage";

const LoginForm = lazy(() => import("../features/auth/components/LoginForm"));
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
// Hành trình cặp đôi kiểu Weddy (P1–P6)
const SetupWizard = lazy(() => import("../features/journey/planner/SetupWizard"));
const ComboBuilderPage = lazy(() => import("../features/journey/combo/ComboBuilderPage"));
const ArticlesPage = lazy(() => import("../features/journey/ArticlesPage"));
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
          <Route path="/" element={<HomePage />} />
          <Route path="/browse" element={<BrowsePage />} />
          <Route path="/vendors" element={<BrowsePage />} />
          <Route path="/listings/:id" element={<ListingDetailPage />} />
          <Route path="/login" element={<LoginForm />} />
          <Route path="/register" element={<RegisterForm />} />
          <Route path="/combo" element={<ComboBuilderPage />} />
          <Route path="/cam-nang" element={<ArticlesPage />} />

          {/* Customer Protected Routes */}
          <Route element={<ProtectedRoute allowedRoles={["Customer"]} />}>
            <Route path="/planner" element={<CustomerDashboard />} />
            <Route path="/planner/setup" element={<SetupWizard />} />
            <Route path="/customer/dashboard" element={<CustomerDashboard />} />
            <Route path="/customer/contracts" element={<CustomerDashboard />} />
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
      </Routes>
    </Suspense>
  );
}
