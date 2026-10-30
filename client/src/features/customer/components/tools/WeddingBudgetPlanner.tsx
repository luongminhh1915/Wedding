import { useRef, useState } from "react";
import {
  Wallet,
  Plus,
  Trash2,
  CheckCircle2,
  AlertTriangle,
  RotateCw,
  Loader2,
  Sparkles,
  Pencil,
} from "lucide-react";
import { useWeddingBudget } from "../../hooks/useWeddingTools";
import type { SaveBudgetItemPayload } from "../../../../types/wedding-tools.types";
import { usePlannerAction } from "../../../journey/planner/plannerApi";
import { apiError } from "../../../journey/api";
import { MoneyInput } from "../../../../components/MoneyInput";
import { personalBudgetProgress } from "./personalBudgetModel";
import { ComboDialog } from "../../../journey/combo/ComboDialog";

export function WeddingBudgetPlanner() {
  const action = usePlannerAction();
  const {
    budget,
    isLoading,
    isError,
    refetch,
    saveItem,
    isSaving,
    deleteItem,
    isDeleting,
  } = useWeddingBudget();

  const [customTotalBudget, setCustomTotalBudget] = useState<number | null>(
    null,
  );
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState<string | null>(null);
  const [itemError, setItemError] = useState<string | null>(null);
  const itemSubmission = useRef(false);

  // Form thêm khoản chi mới
  const [newItemForm, setNewItemForm] = useState<SaveBudgetItemPayload>({
    itemName: "",
    plannedCost: 10000000,
    actualCost: 0,
    notes: "",
  });

  const totalPlanned = customTotalBudget ?? budget?.totalPlannedBudget ?? 0;
  const items = budget?.items ?? [];
  const totalActual = items.reduce((acc, cur) => acc + cur.actualCost, 0);

  // FM-006: Remaining_Budget = Total_Planned_Budget - Sum(Actual_Spent)
  const remainingBudget = totalPlanned - totalActual;
  const spentPercentage =
    totalPlanned > 0 ? (totalActual / totalPlanned) * 100 : 0;
  const isOverBudget = remainingBudget < 0;
  const draftProgress = personalBudgetProgress(newItemForm.plannedCost, newItemForm.actualCost ?? 0);
  const budgetBusy = isSaving || isDeleting || action.isPending;

  function openItem(form: SaveBudgetItemPayload = { itemName: "", plannedCost: 0, actualCost: 0, owner: "Shared" }) {
    if (budgetBusy || itemSubmission.current) return;
    setItemError(null);
    setNewItemForm(form);
    setIsAddModalOpen(true);
  }

  function closeItem() {
    if (!itemSubmission.current && !isSaving) setIsAddModalOpen(false);
  }

  const handleAddItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newItemForm.itemName.trim() || budgetBusy || itemSubmission.current) return;

    itemSubmission.current = true;
    setItemError(null);
    try {
      await saveItem(newItemForm);
      setIsAddModalOpen(false);
      setNewItemForm({
        itemName: "",
        plannedCost: 10000000,
        actualCost: 0,
        notes: "",
      });
      setSaveSuccessMsg("Đã lưu hạng mục chi phí thành công!");
      setTimeout(() => setSaveSuccessMsg(null), 4000);
    } catch (error) {
      setItemError(apiError(error, "Không thể lưu mục chi phí. Nội dung đang nhập được giữ lại; hãy thử lưu lại."));
    } finally {
      itemSubmission.current = false;
    }
  };

  const handleDeleteItem = async (id: string) => {
    if (budgetBusy || itemSubmission.current) return;
    if (!confirm("Bạn có chắc chắn muốn xóa hạng mục này không?")) return;
    itemSubmission.current = true;
    try {
      await deleteItem(id);
    } catch {
      alert("Không thể xóa mục chi phí.");
    } finally {
      itemSubmission.current = false;
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[300px] text-slate-500">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-semibold">
          Đang tải bảng dự toán ngân sách cưới...
        </p>
      </div>
    );
  }

  if (isError)
    return (
      <div role="alert" className="overview-panel text-center py-12">
        <p>Chưa thể tải ngân sách cưới.</p>
        <button className="wm-button small mt-4" onClick={() => refetch()}>
          Thử lại
        </button>
      </div>
    );

  return (
    <div className="space-y-6">
      {/* Top Header Card */}
      <div className="bg-white p-6 rounded-3xl border border-slate-200 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-0.5 rounded-full bg-amber-100 text-amber-800 font-extrabold text-[11px] uppercase tracking-wider">
              Ngân sách của hai bạn
            </span>
            <span className="text-xs text-slate-400">•</span>
            <span className="text-xs font-semibold text-slate-500">
              Còn lại = Ngân sách dự kiến − Tổng đã chi / đã cọc
            </span>
          </div>
          <h2 className="text-2xl font-black text-slate-900 mt-1">
            Sổ Ngân Sách Của Hai Bạn
          </h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Tự ghi chi phí, kể cả khoản trả ngoài hệ thống, để biết đã chi bao nhiêu và còn cần chuẩn bị bao nhiêu.
          </p>
          <p className="text-xs text-slate-500 mt-2">
            Đây là công cụ lập kế hoạch cá nhân, không xác nhận thanh toán với NCC hay đối soát hợp đồng.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => refetch()}
            className="p-2.5 rounded-xl border border-slate-200 hover:bg-slate-100 text-slate-600 transition"
            title="Làm mới"
          >
            <RotateCw className="w-4 h-4" />
          </button>
          <button
            onClick={() => openItem()}
            disabled={budgetBusy}
            className="px-4 py-2.5 rounded-xl bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white font-bold text-xs shadow-md shadow-rose-600/20 flex items-center gap-1.5 transition"
          >
            <Plus className="w-4 h-4" />
            <span>Thêm Khoản Chi</span>
          </button>
        </div>
      </div>

      {saveSuccessMsg && (
        <div className="p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-2xl flex items-center gap-2 text-sm shadow-xs animate-in fade-in">
          <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
          <span className="font-semibold">{saveSuccessMsg}</span>
        </div>
      )}

      {/* Grid: Overview Box & Golden Ratio Cards */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        {/* Left: Total Budget Card (col-span-5) */}
        <div className="lg:col-span-5 bg-gradient-to-br from-white to-rose-50/30 p-6 rounded-3xl border border-rose-200 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-extrabold uppercase tracking-wider text-rose-600 flex items-center gap-1">
                <Wallet className="w-3.5 h-3.5" />
                Tổng Ngân Sách Dự Kiến
              </span>
              <span className="text-[10px] text-slate-400 font-semibold">
                (Nhấp để chỉnh sửa)
              </span>
            </div>

            <p className="text-3xl font-black text-rose-600 break-all" data-testid="budget-total">
              {Math.round(totalPlanned).toLocaleString("vi-VN")}đ
            </p>
            <div className="flex items-center gap-2 mt-2">
              <MoneyInput
                aria-label="Chỉnh tổng ngân sách dự kiến"
                value={totalPlanned}
                onValueChange={value => setCustomTotalBudget(value ?? 0)}
                className="min-w-0 flex-1 text-base font-bold text-rose-600 bg-white border border-rose-200 rounded-xl px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500"
              />
              <span className="text-sm font-bold text-slate-600">đ</span>
            </div>
            <button className="wm-button small mt-3" disabled={action.isPending || customTotalBudget === null} onClick={() => action.mutate({ path: "budget", method: "put", data: { totalBudget: totalPlanned } }, { onSuccess: () => { setCustomTotalBudget(null); setSaveSuccessMsg("Đã lưu tổng ngân sách vào hồ sơ cưới."); } })}>Lưu tổng ngân sách</button>
            {action.isError && <p role="alert" className="text-red-700 text-xs mt-2">{apiError(action.error)}</p>}

            {/* Spent and Remaining */}
            <div className="mt-5 space-y-3">
              <div className="flex items-center justify-between text-xs font-semibold text-slate-600">
                <span>Đã chi / đã cọc:</span>
                <span className="font-extrabold text-slate-900 text-sm">
                  {totalActual.toLocaleString("vi-VN")} đ{" "}
                  <span className="text-xs font-medium text-slate-500">
                    ({spentPercentage.toFixed(1)}%)
                  </span>
                </span>
              </div>

              {/* Progress Bar */}
              <div className="w-full h-3 rounded-full bg-slate-100 overflow-hidden relative">
                <div
                  className={`h-full transition-all duration-500 rounded-full ${
                    isOverBudget
                      ? "bg-red-500"
                      : spentPercentage > 80
                        ? "bg-amber-500"
                        : "bg-gradient-to-r from-emerald-500 to-teal-500"
                  }`}
                  style={{ width: `${Math.min(spentPercentage, 100)}%` }}
                />
              </div>

              <div className="flex items-center justify-between text-xs pt-1 border-t border-slate-200/80">
                <span className="font-semibold text-slate-600">
                  Còn trong ngân sách dự kiến:
                </span>
                <span
                  className={`font-black text-base ${isOverBudget ? "text-red-600" : "text-emerald-700"}`}
                >
                  {remainingBudget.toLocaleString("vi-VN")} đ
                </span>
              </div>
            </div>
          </div>

          {isOverBudget ? (
            <div className="mt-4 p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-2xl flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 shrink-0 text-red-500" />
              <span>
                <strong>Cảnh báo bội chi:</strong> Chi phí thực tế đã vượt ngân
                sách {Math.abs(remainingBudget).toLocaleString("vi-VN")} đ. Bạn
                nên cân nhắc điều chỉnh lại!
              </span>
            </div>
          ) : (
            <div className="mt-4 text-[11px] text-slate-500 italic">
              ✨ Quản lý ngân sách an toàn. Bạn còn khả dụng{" "}
              {remainingBudget.toLocaleString("vi-VN")} đ cho các khoản chi tiếp
              theo.
            </div>
          )}
        </div>

        {/* Right: Golden Ratio Distribution (col-span-7) */}
        <div className="lg:col-span-7 bg-white p-6 rounded-3xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div>
            <div className="text-[11px] font-extrabold uppercase tracking-wider text-slate-400 mb-2 flex items-center gap-1">
              <Sparkles className="w-3.5 h-3.5 text-amber-500" />
              Gợi ý phân bổ tham khảo theo ngân sách
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 mt-3">
              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">
                  Tiệc Cưới (50%)
                </div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.5).toLocaleString("vi-VN")} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">
                  Decor & Hoa (15%)
                </div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.15).toLocaleString("vi-VN")} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">
                  Ảnh & Váy (15%)
                </div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.15).toLocaleString("vi-VN")} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">
                  Nhẫn & Nữ Trang (10%)
                </div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.1).toLocaleString("vi-VN")} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">
                  Dự Phòng (10%)
                </div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.1).toLocaleString("vi-VN")} đ
                </div>
              </div>

              <div className="bg-rose-50/60 p-3 rounded-2xl border border-rose-100 text-center flex flex-col items-center justify-center">
                <div className="text-[10px] font-bold text-rose-600 uppercase">
                  Số Hạng Mục
                </div>
                <div className="text-base font-black text-rose-700 mt-0.5">
                  {items.length} Khoản
                </div>
              </div>
            </div>
          </div>

          <p className="text-[11px] text-slate-400 mt-4">
            Các tỷ lệ trên chỉ để tham khảo, không bắt buộc. Bạn có thể tự điều chỉnh từng khoản theo kế hoạch của hai bạn.
          </p>
        </div>
      </div>

      {/* Main Budget Items Table */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-xs overflow-hidden">
        <div className="p-6 border-b border-slate-100 flex items-center justify-between">
          <div>
            <h3 className="font-extrabold text-base text-slate-900">
              Chi Tiết Hạng Mục Chi Phí Cưới
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Ghi tổng dự kiến và số đã chi, bao gồm tiền cọc. Cột còn lại là ước tính theo dự toán, không phải công nợ được xác nhận.
            </p>
          </div>
          <button
            onClick={() => openItem()}
            disabled={budgetBusy}
            className="px-3.5 py-1.5 rounded-xl border border-rose-200 text-rose-600 font-bold text-xs hover:bg-rose-50 transition flex items-center gap-1"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Thêm Hạng Mục</span>
          </button>
        </div>

        <div className="overflow-x-auto">
          {items.length === 0 ? (
            <div className="p-12 text-center text-slate-400 text-xs">
              Chưa có khoản chi nào trong bảng dự toán. Bấm [+ Thêm Khoản Chi]
              để bắt đầu lập kế hoạch!
            </div>
          ) : (
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-slate-50 text-slate-500 font-bold text-xs uppercase tracking-wider border-b border-slate-200">
                  <th className="py-3 px-5">Hạng Mục Chi Phí</th>
                  <th className="py-3 px-5">Nhà Cung Cấp / Ghi Chú</th>
                  <th className="py-3 px-5 text-right">Dự Toán Ban Đầu</th>
                  <th className="py-3 px-5 text-right">Đã chi / đã cọc</th>
                  <th className="py-3 px-5 text-right">Còn theo dự toán</th>
                  <th className="py-3 px-5 text-center">Trạng Thái</th>
                  <th className="py-3 px-5 text-center">Thao Tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map((item) => {
                  const progress = personalBudgetProgress(item.plannedCost, item.actualCost);
                  return (
                  <tr key={item.id} className="hover:bg-slate-50/50 transition">
                    <td className="py-3.5 px-5 font-bold text-slate-900">
                      {item.itemName}
                      <small className="block font-normal text-slate-500">{item.owner === "Bride" ? "Quỹ cô dâu" : item.owner === "Groom" ? "Quỹ chú rể" : "Quỹ chung"}{item.contractId ? " · Liên kết HĐ" : ""}</small>
                    </td>
                    <td className="py-3.5 px-5 text-xs text-slate-600">
                      {item.notes || "—"}
                    </td>
                    <td className="py-3.5 px-5 text-right font-medium text-slate-700">
                      {item.plannedCost.toLocaleString("vi-VN")} đ
                    </td>
                    <td className="py-3.5 px-5 text-right font-black text-rose-600">
                      {item.actualCost.toLocaleString("vi-VN")} đ
                    </td>
                    <td className="py-3.5 px-5 text-right font-semibold text-slate-700">
                      {progress.remaining.toLocaleString("vi-VN")} đ
                      {progress.excess > 0 && <small className="block text-red-700">Vượt {progress.excess.toLocaleString("vi-VN")} đ</small>}
                    </td>
                    <td className="py-3.5 px-5 text-center">
                      <span className={`inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full ${progress.status === "over-plan" ? "text-red-700 bg-red-50" : progress.status === "unrecorded" ? "text-slate-500 bg-slate-100" : "text-slate-700 bg-slate-100"}`}>
                        {progress.label}
                      </span>
                    </td>
                    <td className="py-3.5 px-5 text-center">
                      <button className="p-1 text-slate-500 mr-2" title="Sửa khoản chi" disabled={budgetBusy} onClick={() => openItem({ id: item.id, itemName: item.itemName, plannedCost: item.plannedCost, actualCost: item.actualCost, notes: item.notes, owner: item.owner })}><Pencil className="w-4 h-4" /></button>
                      <button
                        onClick={() => handleDeleteItem(item.id)}
                        className="p-1 text-slate-400 hover:text-red-600 rounded transition"
                        title="Xóa khoản này"
                        disabled={budgetBusy}
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                ); })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Modal Thêm Khoản Chi */}
      {isAddModalOpen && (
        <ComboDialog title={newItemForm.id ? "Chỉnh Sửa Khoản Chi" : "Thêm Khoản Chi Phí Mới"} onClose={closeItem}>
            <form onSubmit={handleAddItem} className="space-y-4 my-4 text-xs" aria-busy={isSaving}>
              <fieldset disabled={isSaving} className="space-y-4 min-w-0">
              <label className="block font-bold text-slate-700">Quỹ phụ trách<select className="block w-full border border-slate-300 rounded-xl p-2 mt-1" value={newItemForm.owner || "Shared"} onChange={e => setNewItemForm(f => ({ ...f, owner: e.target.value }))}><option value="Shared">Quỹ chung</option><option value="Bride">Cô dâu</option><option value="Groom">Chú rể</option></select></label>
              <div>
                <label htmlFor="budget-item-name" className="block font-bold text-slate-700 mb-1">
                  Tên Hạng Mục Chi Phí:
                </label>
                <input
                  id="budget-item-name"
                  type="text"
                  required
                  value={newItemForm.itemName}
                  onChange={(e) =>
                    setNewItemForm((f) => ({ ...f, itemName: e.target.value }))
                  }
                  placeholder="Ví dụ: Thiệp cưới & in ấn, Hoa cầm tay..."
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label htmlFor="budget-planned-cost" className="block font-bold text-slate-700 mb-1">
                    Dự Toán Ban Đầu (đ):
                  </label>
                  <MoneyInput
                    id="budget-planned-cost"
                    aria-label="Dự toán ban đầu (VNĐ)"
                    required
                    value={newItemForm.plannedCost}
                    onValueChange={(value) =>
                      setNewItemForm((f) => ({
                        ...f,
                        plannedCost: value ?? 0,
                      }))
                    }
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                  />
                </div>
                <div>
                  <label htmlFor="budget-recorded-spent" className="block font-bold text-slate-700 mb-1">
                    Đã chi / đã cọc (đ):
                  </label>
                  <MoneyInput
                    id="budget-recorded-spent"
                    aria-label="Đã chi / đã cọc (VNĐ)"
                    value={newItemForm.actualCost ?? 0}
                    onValueChange={(value) =>
                      setNewItemForm((f) => ({
                        ...f,
                        actualCost: value ?? 0,
                      }))
                    }
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                  />
                </div>
              </div>

              <div className="rounded-xl border border-slate-200 bg-slate-50 p-3" role="status" aria-live="polite">
                <p className="font-semibold text-slate-700">Còn theo dự toán: {draftProgress.remaining.toLocaleString("vi-VN")} đ</p>
                {draftProgress.excess > 0 && <p className="text-red-700 mt-1">Vượt dự toán: {draftProgress.excess.toLocaleString("vi-VN")} đ</p>}
                <p className="text-slate-500 mt-2">Nhập tổng số tiền bạn đã chi cho khoản này, bao gồm tiền cọc. Khi trả thêm, cập nhật tổng mới; không cộng tiền cọc lần nữa.</p>
              </div>

              <div>
                <label htmlFor="budget-item-notes" className="block font-bold text-slate-700 mb-1">
                  Nhà Cung Cấp / Ghi Chú:
                </label>
                <input
                  id="budget-item-notes"
                  type="text"
                  value={newItemForm.notes || ""}
                  onChange={(e) =>
                    setNewItemForm((f) => ({ ...f, notes: e.target.value }))
                  }
                  placeholder="Tên studio hoặc ghi chú thêm..."
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              </fieldset>
              {itemError && <p role="alert" className="text-red-700 rounded-xl border border-red-200 bg-red-50 p-3">{itemError}</p>}
              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={closeItem}
                  disabled={isSaving}
                  className="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl"
                >
                  Hủy Bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSaving}
                  className="px-5 py-2 text-xs font-bold bg-rose-600 text-white rounded-xl hover:bg-rose-500 transition disabled:opacity-50"
                >
                  {isSaving ? "Đang lưu..." : newItemForm.id ? "Lưu thay đổi" : "Thêm Vào Dự Toán"}
                </button>
              </div>
            </form>
        </ComboDialog>
      )}
    </div>
  );
}

export default WeddingBudgetPlanner;
