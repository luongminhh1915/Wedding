import { Routes, Route } from 'react-router-dom';
import LoginForm from '../features/auth/components/LoginForm';
import RegisterForm from '../features/auth/components/RegisterForm';
import ProtectedRoute from './ProtectedRoute';
import CustomerDashboard from '../features/customer/CustomerDashboard';
import BrowsePage from '../features/customer/BrowsePage';
import ListingDetailPage from '../features/customer/ListingDetailPage';
import VendorDashboard from '../features/vendor-portal/VendorDashboard';
import AdminDashboard from '../features/admin/AdminDashboard';

export default function AppRoutes() {
  return (
    <Routes>
      {/* Public Routes - Màn hình chính của web */}
      <Route path="/" element={<BrowsePage />} />
      <Route path="/browse" element={<BrowsePage />} />
      <Route path="/listings/:id" element={<ListingDetailPage />} />
      <Route path="/login" element={<LoginForm />} />
      <Route path="/register" element={<RegisterForm />} />

      {/* Customer Protected Routes */}
      <Route element={<ProtectedRoute allowedRoles={['Customer']} />}>
        <Route path="/customer/dashboard" element={<CustomerDashboard />} />
      </Route>

      {/* Vendor Protected Routes (BR-001) */}
      <Route element={<ProtectedRoute allowedRoles={['VendorOwner']} />}>
        <Route path="/vendor/dashboard" element={<VendorDashboard />} />
      </Route>

      {/* Admin Back-office Protected Routes */}
      <Route element={<ProtectedRoute allowedRoles={['Moderator', 'Finance', 'CustomerCare', 'SuperAdmin']} />}>
        <Route path="/admin/dashboard" element={<AdminDashboard />} />
      </Route>
    </Routes>
  );
}
