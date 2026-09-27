import { useState } from 'react';
import {
  Plus, Trash2, CheckCircle2,
  RotateCw, Loader2, X
} from 'lucide-react';
import { useWeddingChecklist } from '../../hooks/useWeddingTools';
import type { SaveChecklistTaskPayload } from '../../../../types/wedding-tools.types';

const MILESTONES = [
  '9 - 12 Tháng',
  '6 - 9 Tháng',
  '3 - 6 Tháng',
  '1 - 2 Tháng',
  '1 Tuần & Ngày Cưới'
];

export function WeddingChecklist() {
  const { tasks, isLoading, refetch, toggleTask, saveTask, isSaving, deleteTask } = useWeddingChecklist();

  const [selectedMilestone, setSelectedMilestone] = useState<string>('all');
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [newTaskForm, setNewTaskForm] = useState<SaveChecklistTaskPayload>({
    title: '',
    milestone: '3 - 6 Tháng',
    notes: 'Cả hai'
  });

  const totalTasks = tasks.length;
  const completedTasks = tasks.filter(t => t.isCompleted).length;
  const progressPercent = totalTasks > 0 ? (completedTasks / totalTasks) * 100 : 0;

  const filteredTasks = selectedMilestone === 'all'
    ? tasks
    : tasks.filter(t => t.milestone.includes(selectedMilestone) || selectedMilestone.includes(t.milestone));

  const handleToggle = async (id: string) => {
    try {
      await toggleTask(id);
    } catch {
      alert('Không thể cập nhật trạng thái.');
    }
  };

  const handleAddTask = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTaskForm.title.trim()) return;

    try {
      await saveTask(newTaskForm);
      setIsAddModalOpen(false);
      setNewTaskForm({ title: '', milestone: '3 - 6 Tháng', notes: 'Cả hai' });
    } catch {
      alert('Không thể thêm công việc.');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Bạn có chắc chắn muốn xóa việc này không?')) return;
    try {
      await deleteTask(id);
    } catch {
      alert('Không thể xóa công việc.');
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[300px] text-slate-500">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-semibold">Đang tải kế hoạch chuẩn bị cưới 12 tháng...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Top Header Card */}
      <div className="bg-white p-6 rounded-3xl border border-slate-200 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-0.5 rounded-full bg-indigo-100 text-indigo-800 font-extrabold text-[11px] uppercase tracking-wider">
              Wedding Checklist • Kế Hoạch 12 Tháng
            </span>
            <span className="text-xs text-slate-400">•</span>
            <span className="text-xs font-semibold text-slate-500">
              Lộ trình từng bước tới ngày cưới
            </span>
          </div>
          <h2 className="text-2xl font-black text-slate-900 mt-1">Danh Sách Việc Cần Làm Chuẩn Bị Cưới</h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Tích chọn các công việc đã hoàn thành và phân công rõ ràng cho Chú rể hoặc Cô dâu.
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
            className="px-4 py-2.5 rounded-xl bg-gradient-to-r from-indigo-600 to-rose-600 hover:from-indigo-500 hover:to-rose-500 text-white font-bold text-xs shadow-md shadow-indigo-600/20 flex items-center gap-1.5 transition"
          >
            <Plus className="w-4 h-4" />
            <span>Thêm Việc Mới</span>
          </button>
        </div>
      </div>

      {/* Progress Summary Card */}
      <div className="bg-gradient-to-br from-white via-indigo-50/20 to-rose-50/30 p-6 rounded-3xl border border-slate-200 shadow-xs">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-3">
          <div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-extrabold text-slate-800">Tiến Độ Chuẩn Bị Cưới Của Bạn</span>
              {progressPercent === 100 && (
                <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-emerald-100 text-emerald-700 text-xs font-bold">
                  <CheckCircle2 className="w-3.5 h-3.5" /> Hoàn Tất 100%!
                </span>
              )}
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
              Đã hoàn thành <strong>{completedTasks}</strong> trên tổng số <strong>{totalTasks}</strong> công việc
            </p>
          </div>

          <div className="text-2xl font-black text-indigo-600">
            {progressPercent.toFixed(0)}%
          </div>
        </div>

        {/* Progress Bar */}
        <div className="w-full h-3 rounded-full bg-slate-100 overflow-hidden">
          <div
            className="h-full bg-gradient-to-r from-indigo-500 via-rose-500 to-amber-500 rounded-full transition-all duration-500"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      {/* Milestone Filter Tabs */}
      <div className="flex items-center gap-1.5 overflow-x-auto pb-2">
        <button
          onClick={() => setSelectedMilestone('all')}
          className={`px-3.5 py-2 rounded-xl text-xs font-bold whitespace-nowrap transition ${
            selectedMilestone === 'all'
              ? 'bg-slate-900 text-white shadow-xs'
              : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50'
          }`}
        >
          Tất Cả ({tasks.length})
        </button>

        {MILESTONES.map(m => {
          const count = tasks.filter(t => t.milestone.includes(m) || m.includes(t.milestone)).length;
          return (
            <button
              key={m}
              onClick={() => setSelectedMilestone(m)}
              className={`px-3.5 py-2 rounded-xl text-xs font-bold whitespace-nowrap transition flex items-center gap-1.5 ${
                selectedMilestone === m
                  ? 'bg-rose-500 text-white shadow-xs'
                  : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50'
              }`}
            >
              <span>{m}</span>
              <span className={`text-[10px] px-1.5 py-0.2 rounded-full ${
                selectedMilestone === m ? 'bg-white/20 text-white' : 'bg-slate-100 text-slate-500'
              }`}>
                {count}
              </span>
            </button>
          );
        })}
      </div>

      {/* Tasks List */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-xs p-6 space-y-4">
        {filteredTasks.length === 0 ? (
          <div className="text-center py-12 text-slate-400 text-xs">
            Không có công việc nào trong mốc này. Bấm [+ Thêm Việc Mới] để thêm nhé!
          </div>
        ) : (
          <div className="divide-y divide-slate-100">
            {filteredTasks.map(task => (
              <div
                key={task.id}
                className="py-3.5 flex items-center justify-between gap-3 group hover:bg-slate-50/60 px-3 rounded-2xl transition"
              >
                <div className="flex items-center gap-3 flex-1">
                  <input
                    type="checkbox"
                    checked={task.isCompleted}
                    onChange={() => handleToggle(task.id)}
                    className="w-5 h-5 rounded-md border-slate-300 text-rose-500 focus:ring-rose-400 cursor-pointer accent-rose-500"
                  />
                  <div className="flex-1">
                    <span className={`text-sm font-semibold transition ${
                      task.isCompleted ? 'line-through text-slate-400' : 'text-slate-800'
                    }`}>
                      {task.title}
                    </span>
                    <div className="flex items-center gap-2 mt-0.5 text-[11px] text-slate-400">
                      <span className="font-bold text-rose-600/80">⏳ {task.milestone}</span>
                      {task.notes && <span>• Phụ trách: <strong>{task.notes}</strong></span>}
                    </div>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  {task.isCompleted ? (
                    <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-200">
                      ✓ Đã xong
                    </span>
                  ) : (
                    <span className="text-[10px] font-bold text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full border border-amber-200">
                      Chưa làm
                    </span>
                  )}

                  <button
                    onClick={() => handleDelete(task.id)}
                    className="p-1.5 text-slate-300 hover:text-red-600 rounded-lg transition opacity-0 group-hover:opacity-100"
                    title="Xóa công việc"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Modal Thêm Việc Mới */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in">
          <div className="bg-white rounded-3xl max-w-md w-full p-6 shadow-2xl border border-slate-100">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="font-extrabold text-base text-slate-900">Thêm Công Việc Mới Vào Checklist</h3>
              <button onClick={() => setIsAddModalOpen(false)} className="text-slate-400 hover:text-slate-600">
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleAddTask} className="space-y-4 my-4 text-xs">
              <div>
                <label className="block font-bold text-slate-700 mb-1">Nội Dung Công Việc:</label>
                <input
                  type="text"
                  required
                  value={newTaskForm.title}
                  onChange={(e) => setNewTaskForm(f => ({ ...f, title: e.target.value }))}
                  placeholder="Ví dụ: Đặt thiệp cưới, Thuê xe hoa rước dâu..."
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1">Mốc Thời Gian:</label>
                <select
                  value={newTaskForm.milestone}
                  onChange={(e) => setNewTaskForm(f => ({ ...f, milestone: e.target.value }))}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500 bg-white"
                >
                  {MILESTONES.map(m => (
                    <option key={m} value={m}>{m}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1">Phân Công Phụ Trách:</label>
                <select
                  value={newTaskForm.notes || 'Cả hai'}
                  onChange={(e) => setNewTaskForm(f => ({ ...f, notes: e.target.value }))}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500 bg-white"
                >
                  <option value="Cả hai">Cả hai cùng làm</option>
                  <option value="Chú Rể">Chú Rể phụ trách</option>
                  <option value="Cô Dâu">Cô Dâu phụ trách</option>
                </select>
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
                  className="px-5 py-2 text-xs font-bold bg-indigo-600 text-white rounded-xl hover:bg-indigo-500 transition disabled:opacity-50"
                >
                  {isSaving ? 'Đang lưu...' : 'Thêm Vào Kế Hoạch'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default WeddingChecklist;
