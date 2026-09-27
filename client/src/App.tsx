import { Link } from 'react-router-dom';
import { useAuthStore } from './store/authStore';
import { Heart, Sparkles, CheckCircle2, ArrowRight, UserCircle, LogIn, Search } from 'lucide-react';

export default function App() {
  const { user, token } = useAuthStore();

  const getDashboardLink = () => {
    if (!user) return '/login';
    if (user.role === 'VendorOwner') return '/vendor/dashboard';
    if (['Moderator', 'Finance', 'CustomerCare', 'SuperAdmin'].includes(user.role)) return '/admin/dashboard';
    return '/customer/dashboard';
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-rose-50 via-white to-slate-50 text-slate-800 flex flex-col font-sans">
      {/* Header */}
      <header className="border-b border-rose-100 bg-white/80 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <Link to="/" className="flex items-center space-x-2">
            <div className="w-10 h-10 rounded-full bg-rose-500 flex items-center justify-center text-white shadow-md shadow-rose-200">
              <Heart className="w-5 h-5 fill-current" />
            </div>
            <span className="text-xl font-bold bg-gradient-to-r from-rose-600 to-pink-600 bg-clip-text text-transparent">
              Wedding Platform
            </span>
          </Link>

          <div className="flex items-center space-x-4">
            {token && user ? (
              <Link
                to={getDashboardLink()}
                className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-sm font-semibold hover:bg-rose-100 transition"
              >
                <UserCircle className="w-4 h-4" />
                <span>{user.fullName} ({user.role})</span>
              </Link>
            ) : (
              <div className="flex items-center space-x-2">
                <Link
                  to="/login"
                  className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-slate-700 hover:text-slate-900 text-sm font-medium hover:bg-slate-100 transition"
                >
                  <LogIn className="w-4 h-4" />
                  <span>Đăng Nhập</span>
                </Link>
                <Link
                  to="/register"
                  className="px-4 py-2 rounded-xl bg-rose-500 hover:bg-rose-600 text-white text-sm font-semibold shadow-sm transition"
                >
                  Đăng Ký
                </Link>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* Hero Section */}
      <main className="flex-1 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12 flex flex-col items-center text-center">
        <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-rose-100/70 text-rose-700 text-sm font-medium mb-6">
          <Sparkles className="w-4 h-4 text-rose-500" />
          <span>Xác Thực & Phân Quyền RBAC (Task AI-03 Sẵn Sàng)</span>
        </div>

        <h1 className="text-4xl sm:text-5xl font-extrabold text-slate-900 tracking-tight max-w-3xl leading-tight">
          Nền Tảng Dịch Vụ Cưới <br className="hidden sm:inline" />
          <span className="bg-gradient-to-r from-rose-500 via-pink-500 to-amber-500 bg-clip-text text-transparent">
            Wedding Service Platform
          </span>
        </h1>

        <p className="mt-4 text-lg text-slate-600 max-w-2xl">
          Hệ thống được thiết kế theo mô hình Clean Architecture 4 tầng chuẩn mực, tích hợp AI Stylist Matching, Quản lý Lead có mã Voucher, và Đối soát hoa hồng 2 kỳ VietQR.
        </p>

        {/* 4 Clean Architecture Layers Status Grid */}
        <div className="mt-12 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 w-full max-w-5xl text-left">
          {/* Layer 1 */}
          <div className="p-6 rounded-2xl bg-white border border-slate-200 shadow-sm hover:shadow-md transition">
            <div className="w-8 h-8 rounded-lg bg-indigo-50 text-indigo-600 flex items-center justify-center font-bold text-sm mb-4">
              01
            </div>
            <h3 className="font-semibold text-slate-900 mb-1">Wedding.Domain</h3>
            <p className="text-xs text-slate-500 mb-4">15 Core Entities, Enums, BaseEntity độc lập hoàn toàn.</p>
            <div className="flex items-center gap-1.5 text-xs text-emerald-600 font-medium">
              <CheckCircle2 className="w-4 h-4" /> Hoàn thành 100%
            </div>
          </div>

          {/* Layer 2 */}
          <div className="p-6 rounded-2xl bg-white border border-slate-200 shadow-sm hover:shadow-md transition">
            <div className="w-8 h-8 rounded-lg bg-rose-50 text-rose-600 flex items-center justify-center font-bold text-sm mb-4">
              02
            </div>
            <h3 className="font-semibold text-slate-900 mb-1">Wedding.Application</h3>
            <p className="text-xs text-slate-500 mb-4">MediatR CQRS, Auth Commands, DTOs & FluentValidation.</p>
            <div className="flex items-center gap-1.5 text-xs text-emerald-600 font-medium">
              <CheckCircle2 className="w-4 h-4" /> Đã gắn Auth Handlers
            </div>
          </div>

          {/* Layer 3 */}
          <div className="p-6 rounded-2xl bg-white border border-slate-200 shadow-sm hover:shadow-md transition">
            <div className="w-8 h-8 rounded-lg bg-amber-50 text-amber-600 flex items-center justify-center font-bold text-sm mb-4">
              03
            </div>
            <h3 className="font-semibold text-slate-900 mb-1">Wedding.Infrastructure</h3>
            <p className="text-xs text-slate-500 mb-4">EF Core DbContext, BCrypt PasswordHasher, JwtProvider.</p>
            <div className="flex items-center gap-1.5 text-xs text-emerald-600 font-medium">
              <CheckCircle2 className="w-4 h-4" /> Đã sinh Migration
            </div>
          </div>

          {/* Layer 4 */}
          <div className="p-6 rounded-2xl bg-white border border-slate-200 shadow-sm hover:shadow-md transition">
            <div className="w-8 h-8 rounded-lg bg-teal-50 text-teal-600 flex items-center justify-center font-bold text-sm mb-4">
              04
            </div>
            <h3 className="font-semibold text-slate-900 mb-1">Wedding.WebApi</h3>
            <p className="text-xs text-slate-500 mb-4">AuthController (/register, /login, /me), Swagger JWT Bearer.</p>
            <div className="flex items-center gap-1.5 text-xs text-emerald-600 font-medium">
              <CheckCircle2 className="w-4 h-4" /> Sẵn sàng API
            </div>
          </div>
        </div>

        {/* Quick Action Buttons */}
        <div className="mt-10 flex flex-wrap gap-4 justify-center">
          <Link
            to="/browse"
            className="px-6 py-3 rounded-xl bg-rose-500 hover:bg-rose-600 text-white font-semibold text-sm shadow-md shadow-rose-200 transition flex items-center gap-2"
          >
            <Search className="w-4 h-4" />
            <span>Khám Phá Dịch Vụ Cưới</span>
            <ArrowRight className="w-4 h-4" />
          </Link>
          <Link
            to="/register"
            className="px-6 py-3 rounded-xl bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 font-semibold text-sm transition"
          >
            Đăng Ký Làm Đối Tác
          </Link>
          <Link
            to="/login"
            className="px-6 py-3 rounded-xl bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 font-semibold text-sm transition"
          >
            Đăng Nhập Hệ Thống
          </Link>
        </div>
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-200 py-6 text-center text-xs text-slate-500">
        Wedding Service Platform © 2026 — Lean MVP Clean Architecture
      </footer>
    </div>
  );
}
