import { useState } from 'react';
import {
  Wallet, Plus, Trash2, CheckCircle2, AlertTriangle,
  RotateCw, Loader2, Sparkles, X
} from 'lucide-react';
import { useWeddingBudget } from '../../hooks/useWeddingTools';
import type { SaveBudgetItemPayload } from '../../../../types/wedding-tools.types';

export function WeddingBudgetPlanner() {
  const { budget, isLoading, refetch, saveItem, isSaving, deleteItem } = useWeddingBudget();

  const [customTotalBudget, setCustomTotalBudget] = useState<number | null>(null);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState<string | null>(null);

  // Form thêm khoản chi mới
  const [newItemForm, setNewItemForm] = useState<SaveBudgetItemPayload>({
    itemName: '',
    plannedCost: 10000000,
    actualCost: 0,
    notes: ''
  });

  const totalPlanned = customTotalBudget ?? budget?.totalPlannedBudget ?? 300000000;
  const items = budget?.items ?? [];
  const totalActual = items.reduce((acc, cur) => acc + cur.actualCost, 0);

  // FM-006: Remaining_Budget = Total_Planned_Budget - Sum(Actual_Spent)
  const remainingBudget = totalPlanned - totalActual;
  const spentPercentage = totalPlanned > 0 ? (totalActual / totalPlanned) * 100 : 0;
  const isOverBudget = remainingBudget < 0;

  const handleAddItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newItemForm.itemName.trim()) return;

    try {
      await saveItem(newItemForm);
      setIsAddModalOpen(false);
      setNewItemForm({ itemName: '', plannedCost: 10000000, actualCost: 0, notes: '' });
      setSaveSuccessMsg('Đã thêm hạng mục chi phí mới thành công!');
      setTimeout(() => setSaveSuccessMsg(null), 4000);
    } catch {
      alert('Không thể thêm mục chi phí.');
    }
  };

  const handleDeleteItem = async (id: string) => {
    if (!confirm('Bạn có chắc chắn muốn xóa hạng mục này không?')) return;
    try {
      await deleteItem(id);
    } catch {
      alert('Không thể xóa mục chi phí.');
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[300px] text-slate-500">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-semibold">Đang tải bảng dự toán ngân sách cưới...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Top Header Card */}
      <div className="bg-white p-6 rounded-3xl border border-slate-200 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-0.5 rounded-full bg-amber-100 text-amber-800 font-extrabold text-[11px] uppercase tracking-wider">
              FM-006 • Wedding Budget Planner
            </span>
            <span className="text-xs text-slate-400">•</span>
            <span className="text-xs font-semibold text-slate-500">
              Công thức: Còn lại = Dự toán - Thực tế
            </span>
          </div>
          <h2 className="text-2xl font-black text-slate-900 mt-1">Bảng Dự Toán & Quản Lý Chi Phí Cưới</h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Tự động tính toán số dư ngân sách theo quy tắc FM-006 và phân bổ theo tỷ lệ vàng của chuyên gia.
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
            onClick={() => setIsAddModalOpen(true)}
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
              <span className="text-[10px] text-slate-400 font-semibold">(Nhấp để chỉnh sửa)</span>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="number"
                step="5000000"
                value={totalPlanned}
                onChange={(e) => setCustomTotalBudget(Math.max(0, Number(e.target.value)))}
                className="w-full text-2xl font-black text-rose-600 bg-white border border-rose-200 rounded-xl px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500"
              />
              <span className="text-sm font-bold text-slate-600">đ</span>
            </div>

            {/* Spent and Remaining */}
            <div className="mt-5 space-y-3">
              <div className="flex items-center justify-between text-xs font-semibold text-slate-600">
                <span>Đã Chi Thực Tế:</span>
                <span className="font-extrabold text-slate-900 text-sm">
                  {totalActual.toLocaleString('vi-VN')} đ{' '}
                  <span className="text-xs font-medium text-slate-500">({spentPercentage.toFixed(1)}%)</span>
                </span>
              </div>

              {/* Progress Bar */}
              <div className="w-full h-3 rounded-full bg-slate-100 overflow-hidden relative">
                <div
                  className={`h-full transition-all duration-500 rounded-full ${
                    isOverBudget
                      ? 'bg-red-500'
                      : spentPercentage > 80
                      ? 'bg-amber-500'
                      : 'bg-gradient-to-r from-emerald-500 to-teal-500'
                  }`}
                  style={{ width: `${Math.min(spentPercentage, 100)}%` }}
                />
              </div>

              <div className="flex items-center justify-between text-xs pt-1 border-t border-slate-200/80">
                <span className="font-semibold text-slate-600">Ngân Sách Còn Lại (FM-006):</span>
                <span className={`font-black text-base ${isOverBudget ? 'text-red-600' : 'text-emerald-700'}`}>
                  {remainingBudget.toLocaleString('vi-VN')} đ
                </span>
              </div>
            </div>
          </div>

          {isOverBudget ? (
            <div className="mt-4 p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-2xl flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 shrink-0 text-red-500" />
              <span>
                <strong>Cảnh báo bội chi:</strong> Chi phí thực tế đã vượt ngân sách{' '}
                {Math.abs(remainingBudget).toLocaleString('vi-VN')} đ. Bạn nên cân nhắc điều chỉnh lại!
              </span>
            </div>
          ) : (
            <div className="mt-4 text-[11px] text-slate-500 italic">
              ✨ Quản lý ngân sách an toàn. Bạn còn khả dụng {remainingBudget.toLocaleString('vi-VN')} đ cho các khoản chi tiếp theo.
            </div>
          )}
        </div>

        {/* Right: Golden Ratio Distribution (col-span-7) */}
        <div className="lg:col-span-7 bg-white p-6 rounded-3xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div>
            <div className="text-[11px] font-extrabold uppercase tracking-wider text-slate-400 mb-2 flex items-center gap-1">
              <Sparkles className="w-3.5 h-3.5 text-amber-500" />
              Gợi Ý Phân Bổ Tỷ Lệ Vàng Theo Tổng Ngân Sách
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 mt-3">
              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">Tiệc Cưới (50%)</div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.5).toLocaleString('vi-VN')} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">Decor & Hoa (15%)</div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.15).toLocaleString('vi-VN')} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">Ảnh & Váy (15%)</div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.15).toLocaleString('vi-VN')} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">Nhẫn & Nữ Trang (10%)</div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.1).toLocaleString('vi-VN')} đ
                </div>
              </div>

              <div className="bg-slate-50 p-3 rounded-2xl border border-slate-100 text-center">
                <div className="text-[10px] font-bold text-slate-500 uppercase">Dự Phòng (10%)</div>
                <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                  {(totalPlanned * 0.1).toLocaleString('vi-VN')} đ
                </div>
              </div>

              <div className="bg-rose-50/60 p-3 rounded-2xl border border-rose-100 text-center flex flex-col items-center justify-center">
                <div className="text-[10px] font-bold text-rose-600 uppercase">Số Hạng Mục</div>
                <div className="text-base font-black text-rose-700 mt-0.5">{items.length} Khoản</div>
              </div>
            </div>
          </div>

          <p className="text-[11px] text-slate-400 mt-4">
            💡 <strong>Mẹo chuyên gia:</strong> Giữ lại 10% ngân sách dự phòng cho các chi phí phát sinh bất ngờ như bàn tiệc dự phòng hoặc phương tiện đi lại ngày rước dâu.
          </p>
        </div>
      </div>

      {/* Main Budget Items Table */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-xs overflow-hidden">
        <div className="p-6 border-b border-slate-100 flex items-center justify-between">
          <div>
            <h3 className="font-extrabold text-base text-slate-900">Chi Tiết Hạng Mục Chi Phí Cưới</h3>
            <p className="text-xs text-slate-500 mt-0.5">So sánh chi phí dự toán ban đầu và thực tế thanh toán</p>
          </div>
          <button
            onClick={() => setIsAddModalOpen(true)}
            className="px-3.5 py-1.5 rounded-xl border border-rose-200 text-rose-600 font-bold text-xs hover:bg-rose-50 transition flex items-center gap-1"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Thêm Hạng Mục</span>
          </button>
        </div>

        <div className="overflow-x-auto">
          {items.length === 0 ? (
            <div className="p-12 text-center text-slate-400 text-xs">
              Chưa có khoản chi nào trong bảng dự toán. Bấm [+ Thêm Khoản Chi] để bắt đầu lập kế hoạch!
            </div>
          ) : (
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-slate-50 text-slate-500 font-bold text-xs uppercase tracking-wider border-b border-slate-200">
                  <th className="py-3 px-5">Hạng Mục Chi Phí</th>
                  <th className="py-3 px-5">Nhà Cung Cấp / Ghi Chú</th>
                  <th className="py-3 px-5 text-right">Dự Toán Ban Đầu</th>
                  <th className="py-3 px-5 text-right">Chi Phí Thực Tế</th>
                  <th className="py-3 px-5 text-center">Trạng Thái</th>
                  <th className="py-3 px-5 text-center">Thao Tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map(item => (
                  <tr key={item.id} className="hover:bg-slate-50/50 transition">
                    <td className="py-3.5 px-5 font-bold text-slate-900">{item.itemName}</td>
                    <td className="py-3.5 px-5 text-xs text-slate-600">{item.notes || '—'}</td>
                    <td className="py-3.5 px-5 text-right font-medium text-slate-700">
                      {item.plannedCost.toLocaleString('vi-VN')} đ
                    </td>
                    <td className="py-3.5 px-5 text-right font-black text-rose-600">
                      {item.actualCost.toLocaleString('vi-VN')} đ
                    </td>
                    <td className="py-3.5 px-5 text-center">
                      {item.actualCost > 0 ? (
                        <span className="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded-full">
                          <CheckCircle2 className="w-3 h-3" /> Đã Chi
                        </span>
                      ) : (
                        <span className="text-[11px] font-bold text-slate-500 bg-slate-100 px-2 py-0.5 rounded-full">
                          Dự Kiến
                        </span>
                      )}
                    </td>
                    <td className="py-3.5 px-5 text-center">
                      <button
                        onClick={() => handleDeleteItem(item.id)}
                        className="p-1 text-slate-400 hover:text-red-600 rounded transition"
                        title="Xóa khoản này"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Modal Thêm Khoản Chi */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in">
          <div className="bg-white rounded-3xl max-w-md w-full p-6 shadow-2xl border border-slate-100">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="font-extrabold text-base text-slate-900">Thêm Khoản Chi Phí Mới</h3>
              <button onClick={() => setIsAddModalOpen(false)} className="text-slate-400 hover:text-slate-600">
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleAddItem} className="space-y-4 my-4 text-xs">
              <div>
                <label className="block font-bold text-slate-700 mb-1">Tên Hạng Mục Chi Phí:</label>
                <input
                  type="text"
                  required
                  value={newItemForm.itemName}
                  onChange={(e) => setNewItemForm(f => ({ ...f, itemName: e.target.value }))}
                  placeholder="Ví dụ: Thiệp cưới & in ấn, Hoa cầm tay..."
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-bold text-slate-700 mb-1">Dự Toán Ban Đầu (đ):</label>
                  <input
                    type="number"
                    step="1000000"
                    required
                    value={newItemForm.plannedCost}
                    onChange={(e) => setNewItemForm(f => ({ ...f, plannedCost: Number(e.target.value) }))}
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                  />
                </div>
                <div>
                  <label className="block font-bold text-slate-700 mb-1">Thực Tế Đã Chi (đ):</label>
                  <input
                    type="number"
                    step="1000000"
                    value={newItemForm.actualCost}
                    onChange={(e) => setNewItemForm(f => ({ ...f, actualCost: Number(e.target.value) }))}
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                  />
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1">Nhà Cung Cấp / Ghi Chú:</label>
                <input
                  type="text"
                  value={newItemForm.notes || ''}
                  onChange={(e) => setNewItemForm(f => ({ ...f, notes: e.target.value }))}
                  placeholder="Tên studio hoặc ghi chú thêm..."
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setIsAddModalOpen(false)}
                  className="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl"
                >
                  Hủy Bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSaving}
                  className="px-5 py-2 text-xs font-bold bg-rose-600 text-white rounded-xl hover:bg-rose-500 transition disabled:opacity-50"
                >
                  {isSaving ? 'Đang lưu...' : 'Thêm Vào Dự Toán'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default WeddingBudgetPlanner;
