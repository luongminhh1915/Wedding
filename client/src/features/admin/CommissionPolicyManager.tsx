import React, { useState } from "react";
import {
  Percent,
  Plus,
  CheckCircle2,
  XCircle,
  Send,
  Filter,
  AlertCircle,
  FileText,
  Building2,
  Tag,
  Layers,
} from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import axiosClient from "../../services/axiosClient";
import { useAuthStore } from "../../store/authStore";
import {
  CommissionRuleScope,
  CommissionBasisKind,
  CommissionPolicyStatus,
  CommissionAdjustmentStatus,
  type CommissionPolicyRuleDto,
  type CreateCommissionPolicyRuleCommand,
  useCommissionPolicyRules,
  useCreateCommissionPolicyRule,
  useSubmitCommissionPolicyRule,
  useReviewCommissionPolicyRule,
  useCommissionAdjustments,
  useDecideCommissionAdjustment,
} from "./commissionPolicyApi";

interface CategoryOption {
  id: string;
  name: string;
  businessGroupName?: string;
  defaultCommissionRate: number;
}

interface VendorOption {
  id: string;
  brandName: string;
  ownerName: string;
}

export default function CommissionPolicyManager() {
  const { user } = useAuthStore();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const [activeSubTab, setActiveSubTab] = useState<"policies" | "adjustments">("policies");
  const [statusFilter, setStatusFilter] = useState<CommissionPolicyStatus | undefined>(undefined);
  const [adjStatusFilter, setAdjStatusFilter] = useState<CommissionAdjustmentStatus | undefined>(undefined);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [rejectingRule, setRejectingRule] = useState<CommissionPolicyRuleDto | null>(null);
  const [rejectReason, setRejectReason] = useState("");
  const [decidingAdj, setDecidingAdj] = useState<{ id: string; version: string; approve: boolean } | null>(null);
  const [adjDecisionReason, setAdjDecisionReason] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  // Queries
  const { data: rules = [], isLoading: isLoadingRules } = useCommissionPolicyRules(statusFilter);
  const { data: adjustments = [], isLoading: isLoadingAdj } = useCommissionAdjustments(adjStatusFilter);

  // Categories lookup
  const { data: categories = [] } = useQuery({
    queryKey: ["public-categories"],
    queryFn: async () => {
      const { data } = await axiosClient.get<CategoryOption[]>("/api/categories");
      return data;
    },
  });

  // Vendors lookup
  const { data: vendors = [] } = useQuery({
    queryKey: ["admin-vendors-lookup"],
    queryFn: async () => {
      const { data } = await axiosClient.get<{ vendorId?: string; vendorBrandName?: string; fullName: string }[]>("/api/users");
      const map = new Map<string, VendorOption>();
      data.forEach((u) => {
        if (u.vendorId && u.vendorBrandName) {
          map.set(u.vendorId, {
            id: u.vendorId,
            brandName: u.vendorBrandName,
            ownerName: u.fullName,
          });
        }
      });
      return Array.from(map.values());
    },
  });

  // Mutations
  const createRuleMutation = useCreateCommissionPolicyRule();
  const submitRuleMutation = useSubmitCommissionPolicyRule();
  const reviewRuleMutation = useReviewCommissionPolicyRule();
  const decideAdjMutation = useDecideCommissionAdjustment();

  // New Rule Form State
  const [formData, setFormData] = useState<{
    scope: CommissionRuleScope;
    categoryId: string;
    vendorId: string;
    serviceItemId: string;
    ratePercent: string;
    basisKind: CommissionBasisKind;
    policyVersion: string;
    reason: string;
    effectiveFrom: string;
    effectiveTo: string;
  }>({
    scope: CommissionRuleScope.System,
    categoryId: "",
    vendorId: "",
    serviceItemId: "",
    ratePercent: "8.0",
    basisKind: CommissionBasisKind.StandardService,
    policyVersion: `v${new Date().getFullYear()}.${new Date().getMonth() + 1}`,
    reason: "",
    effectiveFrom: new Date().toISOString().slice(0, 10),
    effectiveTo: "",
  });

  const handleCreateRule = async (e: React.FormEvent) => {
    e.preventDefault();
    setActionError(null);
    setActionSuccess(null);

    const rateVal = parseFloat(formData.ratePercent) / 100;
    if (isNaN(rateVal) || rateVal < 0.03 || rateVal > 0.12) {
      setActionError("Tỷ lệ hoa hồng phải nằm trong khoảng 3% – 12% (0.03 – 0.12).");
      return;
    }

    const command: CreateCommissionPolicyRuleCommand = {
      scope: formData.scope,
      categoryId: formData.scope === CommissionRuleScope.Category || formData.scope === CommissionRuleScope.VendorCategory ? formData.categoryId || null : null,
      vendorId: formData.scope === CommissionRuleScope.Vendor || formData.scope === CommissionRuleScope.VendorCategory ? formData.vendorId || null : null,
      serviceItemId: formData.scope === CommissionRuleScope.Service ? formData.serviceItemId.trim() || null : null,
      rate: rateVal,
      basisKind: formData.basisKind,
      policyVersion: formData.policyVersion.trim(),
      reason: formData.reason.trim(),
      effectiveFrom: new Date(formData.effectiveFrom + "T00:00:00Z").toISOString(),
      effectiveTo: formData.effectiveTo ? new Date(formData.effectiveTo + "T23:59:59Z").toISOString() : null,
    };

    try {
      await createRuleMutation.mutateAsync(command);
      setShowCreateModal(false);
      setActionSuccess("Đã tạo chính sách hoa hồng nháp thành công.");
    } catch (err: any) {
      setActionError(err.response?.data?.message || err.message || "Lỗi khi tạo chính sách hoa hồng.");
    }
  };

  const handleSubmitRule = async (rule: CommissionPolicyRuleDto) => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await submitRuleMutation.mutateAsync({ id: rule.id, expectedVersion: rule.version });
      setActionSuccess(`Đã gửi duyệt chính sách ${rule.policyVersion}.`);
    } catch (err: any) {
      setActionError(err.response?.data?.message || err.message || "Lỗi gửi duyệt chính sách.");
    }
  };

  const handleApproveRule = async (rule: CommissionPolicyRuleDto) => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await reviewRuleMutation.mutateAsync({
        id: rule.id,
        expectedVersion: rule.version,
        approve: true,
      });
      setActionSuccess(`Đã phê duyệt chính sách ${rule.policyVersion}.`);
    } catch (err: any) {
      setActionError(err.response?.data?.message || err.message || "Lỗi phê duyệt chính sách.");
    }
  };

  const handleConfirmRejectRule = async () => {
    if (!rejectingRule) return;
    setActionError(null);
    setActionSuccess(null);
    try {
      await reviewRuleMutation.mutateAsync({
        id: rejectingRule.id,
        expectedVersion: rejectingRule.version,
        approve: false,
        reason: rejectReason.trim(),
      });
      setRejectingRule(null);
      setRejectReason("");
      setActionSuccess(`Đã từ chối chính sách ${rejectingRule.policyVersion}.`);
    } catch (err: any) {
      setActionError(err.response?.data?.message || err.message || "Lỗi khi từ chối chính sách.");
    }
  };

  const handleConfirmDecideAdj = async () => {
    if (!decidingAdj) return;
    setActionError(null);
    setActionSuccess(null);
    try {
      await decideAdjMutation.mutateAsync({
        id: decidingAdj.id,
        expectedVersion: decidingAdj.version,
        approve: decidingAdj.approve,
        reason: adjDecisionReason.trim(),
      });
      setDecidingAdj(null);
      setAdjDecisionReason("");
      setActionSuccess(`Đã ghi nhận quyết định cho yêu cầu điều chỉnh.`);
    } catch (err: any) {
      setActionError(err.response?.data?.message || err.message || "Lỗi xử lý yêu cầu điều chỉnh.");
    }
  };

  const getScopeLabel = (scope: CommissionRuleScope) => {
    switch (scope) {
      case CommissionRuleScope.System:
        return { label: "Toàn sàn (System)", bg: "bg-purple-100 text-purple-700 border-purple-200" };
      case CommissionRuleScope.Category:
        return { label: "Ngành hàng (Category)", bg: "bg-blue-100 text-blue-700 border-blue-200" };
      case CommissionRuleScope.Vendor:
        return { label: "Toàn bộ NCC (Vendor)", bg: "bg-amber-100 text-amber-700 border-amber-200" };
      case CommissionRuleScope.VendorCategory:
        return { label: "NCC theo Ngành", bg: "bg-indigo-100 text-indigo-700 border-indigo-200" };
      case CommissionRuleScope.Service:
        return { label: "Dịch vụ đơn lẻ", bg: "bg-emerald-100 text-emerald-700 border-emerald-200" };
      case CommissionRuleScope.Combo:
        return { label: "Gói Combo", bg: "bg-rose-100 text-rose-700 border-rose-200" };
      default:
        return { label: "Không xác định", bg: "bg-slate-100 text-slate-700 border-slate-200" };
    }
  };

  const getBasisLabel = (basis: CommissionBasisKind) => {
    switch (basis) {
      case CommissionBasisKind.StandardService:
        return "Dịch vụ thông thường";
      case CommissionBasisKind.AzManagementFee:
        return "Phí quản lý A-Z";
      case CommissionBasisKind.ComboComponents:
        return "Thành phần Combo";
      default:
        return "Mặc định";
    }
  };

  const getStatusBadge = (status: CommissionPolicyStatus) => {
    switch (status) {
      case CommissionPolicyStatus.Draft:
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200">Nháp</span>;
      case CommissionPolicyStatus.Submitted:
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800 border border-amber-200">Chờ duyệt</span>;
      case CommissionPolicyStatus.Approved:
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-100 text-emerald-800 border border-emerald-200"><CheckCircle2 size={12} /> Đã duyệt</span>;
      case CommissionPolicyStatus.Rejected:
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-rose-100 text-rose-800 border border-rose-200"><XCircle size={12} /> Từ chối</span>;
      default:
        return null;
    }
  };

  return (
    <div className="space-y-6">
      {/* Alert Banners */}
      {actionError && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 flex items-start gap-3">
          <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5 text-rose-600" />
          <div className="flex-1 text-sm font-medium">{actionError}</div>
          <button onClick={() => setActionError(null)} className="text-rose-400 hover:text-rose-600 text-xs">✕ Đóng</button>
        </div>
      )}
      {actionSuccess && (
        <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 flex items-start gap-3">
          <CheckCircle2 className="w-5 h-5 flex-shrink-0 mt-0.5 text-emerald-600" />
          <div className="flex-1 text-sm font-medium">{actionSuccess}</div>
          <button onClick={() => setActionSuccess(null)} className="text-emerald-400 hover:text-emerald-600 text-xs">✕ Đóng</button>
        </div>
      )}

      {/* Header Bar */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="p-2 rounded-xl bg-rose-100 text-rose-600">
              <Percent className="w-5 h-5" />
            </span>
            <h2 className="text-xl font-bold text-slate-800">Chính sách hoa hồng & Điều chỉnh</h2>
          </div>
          <p className="text-xs text-slate-500 mt-1 max-w-2xl">
            Cơ chế phân giải 5 tầng: Dịch vụ → NCC theo danh mục → Toàn NCC → Ngành hàng → Toàn sàn.
            Chính sách chỉ có hiệu lực khi được SuperAdmin duyệt và không bị chồng lấn thời gian.
          </p>
        </div>

        <div className="flex items-center gap-3">
          {isSuperAdmin && activeSubTab === "policies" && (
            <button
              onClick={() => setShowCreateModal(true)}
              className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl bg-rose-600 text-white font-medium text-sm hover:bg-rose-700 shadow-sm transition-colors"
            >
              <Plus size={16} /> Thêm chính sách
            </button>
          )}
        </div>
      </div>

      {/* Sub Tabs */}
      <div className="flex items-center gap-2 border-b border-slate-200 pb-2">
        <button
          onClick={() => setActiveSubTab("policies")}
          className={`px-4 py-2 rounded-xl text-sm font-semibold transition-colors flex items-center gap-2 ${
            activeSubTab === "policies"
              ? "bg-rose-50 text-rose-700"
              : "text-slate-600 hover:text-slate-900 hover:bg-slate-100"
          }`}
        >
          <Layers size={16} /> Chính sách 5 tầng ({rules.length})
        </button>
        <button
          onClick={() => setActiveSubTab("adjustments")}
          className={`px-4 py-2 rounded-xl text-sm font-semibold transition-colors flex items-center gap-2 ${
            activeSubTab === "adjustments"
              ? "bg-rose-50 text-rose-700"
              : "text-slate-600 hover:text-slate-900 hover:bg-slate-100"
          }`}
        >
          <FileText size={16} /> Đề xuất điều chỉnh ({adjustments.length})
        </button>
      </div>

      {/* SubTab 1: 5-Tier Policies */}
      {activeSubTab === "policies" && (
        <div className="space-y-4">
          {/* Status Filter */}
          <div className="flex items-center gap-2 overflow-x-auto py-1">
            <Filter size={15} className="text-slate-400 ml-1" />
            <button
              onClick={() => setStatusFilter(undefined)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                statusFilter === undefined
                  ? "bg-slate-800 text-white border-slate-800"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Tất cả
            </button>
            <button
              onClick={() => setStatusFilter(CommissionPolicyStatus.Approved)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                statusFilter === CommissionPolicyStatus.Approved
                  ? "bg-emerald-600 text-white border-emerald-600"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Đã duyệt
            </button>
            <button
              onClick={() => setStatusFilter(CommissionPolicyStatus.Submitted)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                statusFilter === CommissionPolicyStatus.Submitted
                  ? "bg-amber-600 text-white border-amber-600"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Chờ duyệt
            </button>
            <button
              onClick={() => setStatusFilter(CommissionPolicyStatus.Draft)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                statusFilter === CommissionPolicyStatus.Draft
                  ? "bg-slate-600 text-white border-slate-600"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Bản nháp
            </button>
            <button
              onClick={() => setStatusFilter(CommissionPolicyStatus.Rejected)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                statusFilter === CommissionPolicyStatus.Rejected
                  ? "bg-rose-600 text-white border-rose-600"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Đã từ chối
            </button>
          </div>

          {/* Policy Rules Table */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider">
                  <tr>
                    <th className="py-3.5 px-4">Tầng / Phạm vi</th>
                    <th className="py-3.5 px-4">Đối tượng đích</th>
                    <th className="py-3.5 px-4 text-center">Tỷ lệ</th>
                    <th className="py-3.5 px-4">Cơ sở tính</th>
                    <th className="py-3.5 px-4">Phiên bản</th>
                    <th className="py-3.5 px-4">Thời gian hiệu lực</th>
                    <th className="py-3.5 px-4 text-center">Trạng thái</th>
                    <th className="py-3.5 px-4 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-slate-700">
                  {isLoadingRules ? (
                    <tr>
                      <td colSpan={8} className="py-8 text-center text-slate-400">Đang tải danh sách chính sách...</td>
                    </tr>
                  ) : rules.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="py-12 text-center text-slate-400">
                        <Percent className="w-8 h-8 mx-auto mb-2 text-slate-300" />
                        Chưa có chính sách hoa hồng nào được tạo trong phạm vi đã chọn.
                      </td>
                    </tr>
                  ) : (
                    rules.map((rule) => {
                      const scopeInfo = getScopeLabel(rule.scope);
                      const catName = categories.find((c) => c.id === rule.categoryId)?.name || rule.categoryId;
                      const venName = vendors.find((v) => v.id === rule.vendorId)?.brandName || rule.vendorId;

                      return (
                        <tr key={rule.id} className="hover:bg-slate-50/80 transition-colors">
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            <span className={`inline-block px-2.5 py-1 rounded-lg text-[11px] font-semibold border ${scopeInfo.bg}`}>
                              {scopeInfo.label}
                            </span>
                          </td>
                          <td className="py-3.5 px-4">
                            <div className="font-medium text-slate-800">
                              {rule.scope === CommissionRuleScope.System && "Mọi giao dịch trên nền tảng"}
                              {rule.scope === CommissionRuleScope.Category && (
                                <span className="flex items-center gap-1 text-blue-700">
                                  <Tag size={12} /> {catName}
                                </span>
                              )}
                              {rule.scope === CommissionRuleScope.Vendor && (
                                <span className="flex items-center gap-1 text-amber-700">
                                  <Building2 size={12} /> {venName}
                                </span>
                              )}
                              {rule.scope === CommissionRuleScope.VendorCategory && (
                                <div>
                                  <div className="text-indigo-700 font-semibold">{venName}</div>
                                  <div className="text-slate-500 text-[11px]">Ngành: {catName}</div>
                                </div>
                              )}
                              {rule.scope === CommissionRuleScope.Service && (
                                <div className="font-mono text-[11px] text-emerald-800">
                                  Dịch vụ ID: {rule.serviceItemId?.slice(0, 8)}...
                                </div>
                              )}
                              {rule.scope === CommissionRuleScope.Combo && (
                                <div className="font-mono text-[11px] text-rose-800">
                                  Combo ID: {rule.comboTemplateId?.slice(0, 8)}...
                                </div>
                              )}
                            </div>
                            {rule.reason && (
                              <div className="text-[11px] text-slate-400 truncate max-w-xs mt-0.5" title={rule.reason}>
                                {rule.reason}
                              </div>
                            )}
                          </td>
                          <td className="py-3.5 px-4 text-center font-bold text-slate-900 whitespace-nowrap">
                            {rule.rate != null ? (
                              <span className="text-rose-600 bg-rose-50 px-2 py-0.5 rounded-md font-mono">
                                {(rule.rate * 100).toFixed(2)}%
                              </span>
                            ) : (
                              <span className="text-slate-400 italic font-normal">Theo từng gói con</span>
                            )}
                          </td>
                          <td className="py-3.5 px-4 whitespace-nowrap text-slate-600">
                            {getBasisLabel(rule.basisKind)}
                          </td>
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            <span className="font-mono font-medium text-slate-700 bg-slate-100 px-2 py-0.5 rounded">
                              {rule.policyVersion}
                            </span>
                          </td>
                          <td className="py-3.5 px-4 whitespace-nowrap text-slate-500 text-[11px]">
                            <div>Từ: {new Date(rule.effectiveFrom).toLocaleDateString("vi-VN")}</div>
                            {rule.effectiveTo ? (
                              <div>Đến: {new Date(rule.effectiveTo).toLocaleDateString("vi-VN")}</div>
                            ) : (
                              <div className="text-emerald-600">Vô thời hạn</div>
                            )}
                          </td>
                          <td className="py-3.5 px-4 text-center whitespace-nowrap">
                            {getStatusBadge(rule.status)}
                          </td>
                          <td className="py-3.5 px-4 text-right whitespace-nowrap">
                            <div className="inline-flex items-center gap-1.5">
                              {rule.status === CommissionPolicyStatus.Draft && isSuperAdmin && (
                                <button
                                  onClick={() => handleSubmitRule(rule)}
                                  className="px-2.5 py-1 rounded-lg bg-amber-500 text-white font-medium text-[11px] hover:bg-amber-600 inline-flex items-center gap-1 shadow-sm"
                                >
                                  <Send size={11} /> Gửi duyệt
                                </button>
                              )}
                              {rule.status === CommissionPolicyStatus.Submitted && isSuperAdmin && (
                                <>
                                  <button
                                    onClick={() => handleApproveRule(rule)}
                                    className="px-2.5 py-1 rounded-lg bg-emerald-600 text-white font-medium text-[11px] hover:bg-emerald-700 inline-flex items-center gap-1 shadow-sm"
                                  >
                                    <CheckCircle2 size={11} /> Duyệt
                                  </button>
                                  <button
                                    onClick={() => setRejectingRule(rule)}
                                    className="px-2.5 py-1 rounded-lg bg-rose-600 text-white font-medium text-[11px] hover:bg-rose-700 inline-flex items-center gap-1 shadow-sm"
                                  >
                                    <XCircle size={11} /> Từ chối
                                  </button>
                                </>
                              )}
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* SubTab 2: Adjustments */}
      {activeSubTab === "adjustments" && (
        <div className="space-y-4">
          {/* Adjustments Filter */}
          <div className="flex items-center gap-2 overflow-x-auto py-1">
            <Filter size={15} className="text-slate-400 ml-1" />
            <button
              onClick={() => setAdjStatusFilter(undefined)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                adjStatusFilter === undefined
                  ? "bg-slate-800 text-white border-slate-800"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Tất cả
            </button>
            <button
              onClick={() => setAdjStatusFilter(CommissionAdjustmentStatus.Submitted)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                adjStatusFilter === CommissionAdjustmentStatus.Submitted
                  ? "bg-amber-600 text-white border-amber-600"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Chờ quyết định
            </button>
            <button
              onClick={() => setAdjStatusFilter(CommissionAdjustmentStatus.Approved)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                adjStatusFilter === CommissionAdjustmentStatus.Approved
                  ? "bg-emerald-600 text-white border-emerald-600"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Đã duyệt
            </button>
            <button
              onClick={() => setAdjStatusFilter(CommissionAdjustmentStatus.Rejected)}
              className={`px-3 py-1 rounded-lg text-xs font-medium border ${
                adjStatusFilter === CommissionAdjustmentStatus.Rejected
                  ? "bg-rose-600 text-white border-rose-600"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              Đã từ chối
            </button>
          </div>

          {/* Adjustments Table */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider">
                  <tr>
                    <th className="py-3.5 px-4">Hợp đồng / Nghĩa vụ</th>
                    <th className="py-3.5 px-4">Nhà cung cấp</th>
                    <th className="py-3.5 px-4 text-center">Tỷ lệ cũ</th>
                    <th className="py-3.5 px-4 text-center">Đề xuất</th>
                    <th className="py-3.5 px-4 text-right">Dư nợ chưa trả</th>
                    <th className="py-3.5 px-4">Lý do đề xuất</th>
                    <th className="py-3.5 px-4 text-center">Trạng thái</th>
                    <th className="py-3.5 px-4 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-slate-700">
                  {isLoadingAdj ? (
                    <tr>
                      <td colSpan={8} className="py-8 text-center text-slate-400">Đang tải yêu cầu điều chỉnh...</td>
                    </tr>
                  ) : adjustments.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="py-12 text-center text-slate-400">
                        <FileText className="w-8 h-8 mx-auto mb-2 text-slate-300" />
                        Chưa có yêu cầu điều chỉnh hoa hồng nào.
                      </td>
                    </tr>
                  ) : (
                    adjustments.map((adj) => {
                      const vendor = vendors.find((v) => v.id === adj.vendorId);
                      return (
                        <tr key={adj.id} className="hover:bg-slate-50/80 transition-colors">
                          <td className="py-3.5 px-4 font-mono text-[11px]">
                            <div className="text-slate-900 font-semibold">HĐ: {adj.contractId.slice(0, 8)}...</div>
                            <div className="text-slate-400">Obligation: {adj.commissionId.slice(0, 8)}...</div>
                          </td>
                          <td className="py-3.5 px-4">
                            <div className="font-semibold text-slate-800">{vendor?.brandName || adj.vendorId}</div>
                            <div className="text-[11px] text-slate-400">{vendor?.ownerName}</div>
                          </td>
                          <td className="py-3.5 px-4 text-center font-mono">
                            {(adj.originalRateSnapshot * 100).toFixed(2)}%
                          </td>
                          <td className="py-3.5 px-4 text-center font-mono font-bold text-rose-600 bg-rose-50/50">
                            {(adj.proposedRate * 100).toFixed(2)}%
                          </td>
                          <td className="py-3.5 px-4 text-right font-mono font-semibold text-slate-900">
                            {adj.outstandingAmountSnapshot.toLocaleString("vi-VN")} đ
                          </td>
                          <td className="py-3.5 px-4 text-slate-600 max-w-xs">
                            <div className="truncate" title={adj.reason}>{adj.reason}</div>
                            {adj.decisionReason && (
                              <div className="text-[11px] text-slate-400 italic mt-0.5">
                                Quyết định: {adj.decisionReason}
                              </div>
                            )}
                          </td>
                          <td className="py-3.5 px-4 text-center whitespace-nowrap">
                            {adj.status === CommissionAdjustmentStatus.Submitted && (
                              <span className="px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800 border border-amber-200">Chờ duyệt</span>
                            )}
                            {adj.status === CommissionAdjustmentStatus.Approved && (
                              <span className="px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-100 text-emerald-800 border border-emerald-200">Đã duyệt</span>
                            )}
                            {adj.status === CommissionAdjustmentStatus.Rejected && (
                              <span className="px-2.5 py-0.5 rounded-full text-xs font-medium bg-rose-100 text-rose-800 border border-rose-200">Từ chối</span>
                            )}
                          </td>
                          <td className="py-3.5 px-4 text-right whitespace-nowrap">
                            {adj.status === CommissionAdjustmentStatus.Submitted && isSuperAdmin && (
                              <div className="inline-flex items-center gap-1.5">
                                <button
                                  onClick={() => setDecidingAdj({ id: adj.id, version: adj.version, approve: true })}
                                  className="px-2.5 py-1 rounded-lg bg-emerald-600 text-white font-medium text-[11px] hover:bg-emerald-700 shadow-sm"
                                >
                                  Duyệt
                                </button>
                                <button
                                  onClick={() => setDecidingAdj({ id: adj.id, version: adj.version, approve: false })}
                                  className="px-2.5 py-1 rounded-lg bg-rose-600 text-white font-medium text-[11px] hover:bg-rose-700 shadow-sm"
                                >
                                  Từ chối
                                </button>
                              </div>
                            )}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Create Policy Rule */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
          <div className="bg-white rounded-3xl max-w-xl w-full p-6 shadow-2xl border border-slate-200 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <span className="p-2 rounded-xl bg-rose-100 text-rose-600">
                  <Percent size={18} />
                </span>
                <h3 className="font-bold text-slate-800 text-lg">Tạo chính sách hoa hồng mới</h3>
              </div>
              <button
                onClick={() => setShowCreateModal(false)}
                className="text-slate-400 hover:text-slate-600 text-lg font-bold"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleCreateRule} className="space-y-4 pt-4">
              {/* Scope Selection */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  1. Phạm vi áp dụng (5 Tầng ưu tiên) <span className="text-rose-500">*</span>
                </label>
                <select
                  value={formData.scope}
                  onChange={(e) => setFormData({ ...formData, scope: parseInt(e.target.value, 10) as CommissionRuleScope })}
                  className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                >
                  <option value={CommissionRuleScope.System}>Tầng 5: Toàn hệ thống (Platform Default)</option>
                  <option value={CommissionRuleScope.Category}>Tầng 4: Theo ngành hàng (Category)</option>
                  <option value={CommissionRuleScope.Vendor}>Tầng 3: Toàn bộ dịch vụ của 1 NCC (Vendor)</option>
                  <option value={CommissionRuleScope.VendorCategory}>Tầng 2: NCC trong 1 ngành hàng cụ thể (VendorCategory)</option>
                  <option value={CommissionRuleScope.Service}>Tầng 1: Từng dịch vụ đơn vị (Service Item - Ưu tiên cao nhất)</option>
                </select>
              </div>

              {/* Dynamic Target Selection */}
              {(formData.scope === CommissionRuleScope.Category || formData.scope === CommissionRuleScope.VendorCategory) && (
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Ngành hàng mục tiêu <span className="text-rose-500">*</span>
                  </label>
                  <select
                    value={formData.categoryId}
                    onChange={(e) => setFormData({ ...formData, categoryId: e.target.value })}
                    required
                    className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                  >
                    <option value="">-- Chọn ngành hàng --</option>
                    {categories.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name} (Gốc: {(c.defaultCommissionRate * 100).toFixed(1)}%)
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {(formData.scope === CommissionRuleScope.Vendor || formData.scope === CommissionRuleScope.VendorCategory) && (
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Nhà cung cấp mục tiêu <span className="text-rose-500">*</span>
                  </label>
                  <select
                    value={formData.vendorId}
                    onChange={(e) => setFormData({ ...formData, vendorId: e.target.value })}
                    required
                    className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                  >
                    <option value="">-- Chọn Nhà cung cấp --</option>
                    {vendors.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.brandName} ({v.ownerName})
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {formData.scope === CommissionRuleScope.Service && (
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Service Item ID (UUID) <span className="text-rose-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={formData.serviceItemId}
                    onChange={(e) => setFormData({ ...formData, serviceItemId: e.target.value })}
                    required
                    placeholder="e.g. 11111111-2222-3333-4444-555555555555"
                    className="w-full text-xs font-mono rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                  />
                </div>
              )}

              {/* Rate & Basis */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Tỷ lệ hoa hồng (%) <span className="text-rose-500">*</span>
                  </label>
                  <div className="relative">
                    <input
                      type="number"
                      step="0.01"
                      min="3"
                      max="12"
                      value={formData.ratePercent}
                      onChange={(e) => setFormData({ ...formData, ratePercent: e.target.value })}
                      required
                      className="w-full text-xs font-mono rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                    />
                    <span className="absolute right-3 top-2.5 text-xs text-slate-400 font-semibold">%</span>
                  </div>
                  <span className="text-[10px] text-slate-400 mt-0.5 block">Quy chuẩn 3% – 12%</span>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Cơ sở tính hoa hồng <span className="text-rose-500">*</span>
                  </label>
                  <select
                    value={formData.basisKind}
                    onChange={(e) => setFormData({ ...formData, basisKind: parseInt(e.target.value, 10) as CommissionBasisKind })}
                    className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                  >
                    <option value={CommissionBasisKind.StandardService}>Dịch vụ thông thường</option>
                    <option value={CommissionBasisKind.AzManagementFee}>Phí quản lý A-Z</option>
                  </select>
                </div>
              </div>

              {/* Version & Reason */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Mã phiên bản chính sách <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.policyVersion}
                  onChange={(e) => setFormData({ ...formData, policyVersion: e.target.value })}
                  required
                  placeholder="e.g. v2026.Q4-HCM"
                  className="w-full text-xs font-mono rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Căn cứ / Lý do ban hành <span className="text-rose-500">*</span>
                </label>
                <textarea
                  rows={2}
                  value={formData.reason}
                  onChange={(e) => setFormData({ ...formData, reason: e.target.value })}
                  required
                  placeholder="Ghi rõ lý do ban hành, quyết định đính kèm hoặc điều chỉnh theo phân khúc..."
                  className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              {/* Date Ranges */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Hiệu lực từ ngày <span className="text-rose-500">*</span>
                  </label>
                  <input
                    type="date"
                    value={formData.effectiveFrom}
                    onChange={(e) => setFormData({ ...formData, effectiveFrom: e.target.value })}
                    required
                    className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">
                    Đến ngày (Tùy chọn)
                  </label>
                  <input
                    type="date"
                    value={formData.effectiveTo}
                    onChange={(e) => setFormData({ ...formData, effectiveTo: e.target.value })}
                    className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500"
                  />
                </div>
              </div>

              {/* Submit Buttons */}
              <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 hover:bg-slate-100"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={createRuleMutation.isPending}
                  className="px-5 py-2.5 rounded-xl bg-rose-600 text-white font-semibold text-xs hover:bg-rose-700 shadow-sm disabled:opacity-50"
                >
                  {createRuleMutation.isPending ? "Đang lưu..." : "Tạo bản nháp chính sách"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Reject Rule */}
      {rejectingRule && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
          <div className="bg-white rounded-3xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
            <h3 className="font-bold text-slate-800 text-lg mb-2">Từ chối chính sách hoa hồng</h3>
            <p className="text-xs text-slate-500 mb-4">
              Vui lòng nhập lý do từ chối chính sách phiên bản <strong>{rejectingRule.policyVersion}</strong> để lưu vào nhật ký kiểm toán.
            </p>
            <textarea
              rows={3}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Nhập lý do từ chối..."
              required
              className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500 mb-4"
            />
            <div className="flex items-center justify-end gap-2">
              <button
                onClick={() => setRejectingRule(null)}
                className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 hover:bg-slate-100"
              >
                Hủy
              </button>
              <button
                onClick={handleConfirmRejectRule}
                disabled={!rejectReason.trim()}
                className="px-4 py-2 rounded-xl bg-rose-600 text-white font-semibold text-xs hover:bg-rose-700 disabled:opacity-50"
              >
                Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Decide Adjustment */}
      {decidingAdj && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
          <div className="bg-white rounded-3xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
            <h3 className="font-bold text-slate-800 text-lg mb-2">
              {decidingAdj.approve ? "Phê duyệt điều chỉnh hoa hồng" : "Từ chối điều chỉnh hoa hồng"}
            </h3>
            <p className="text-xs text-slate-500 mb-4">
              Nhập căn cứ xử lý yêu cầu điều chỉnh này. Lưu ý: Phê duyệt chỉ ghi nhận mức điều chỉnh delta cho nghĩa vụ chưa thanh toán; không tự động sửa sổ cái hoặc hoàn tiền lịch sử.
            </p>
            <textarea
              rows={3}
              value={adjDecisionReason}
              onChange={(e) => setAdjDecisionReason(e.target.value)}
              placeholder="Nhập căn cứ xử lý..."
              required
              className="w-full text-xs rounded-xl border border-slate-200 p-2.5 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-500 mb-4"
            />
            <div className="flex items-center justify-end gap-2">
              <button
                onClick={() => setDecidingAdj(null)}
                className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 hover:bg-slate-100"
              >
                Hủy
              </button>
              <button
                onClick={handleConfirmDecideAdj}
                disabled={!adjDecisionReason.trim()}
                className={`px-4 py-2 rounded-xl text-white font-semibold text-xs ${
                  decidingAdj.approve ? "bg-emerald-600 hover:bg-emerald-700" : "bg-rose-600 hover:bg-rose-700"
                } disabled:opacity-50`}
              >
                {decidingAdj.approve ? "Xác nhận duyệt" : "Xác nhận từ chối"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
