import { useState, useEffect } from 'react';
import { X, PlusCircle, Loader2, AlertCircle } from 'lucide-react';
import { useCreateListing, useUpdateListing } from '../hooks/useListings';
import type { ListingSummaryDto } from '../../../types/listing.types';

interface Props {
  editing?: ListingSummaryDto;
  categories: { id: string; name: string }[];
  onClose: () => void;
}

const INITIAL_FORM = {
  categoryId: '',
  title: '',
  minPrice: '',
  maxPrice: '',
  description: '',
  location: '',
};

export default function ListingForm({ editing, categories, onClose }: Props) {
  const [form, setForm] = useState(INITIAL_FORM);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const createMutation = useCreateListing();
  const updateMutation = useUpdateListing();
  const isLoading = createMutation.isPending || updateMutation.isPending;

  useEffect(() => {
    if (editing) {
      setForm({
        categoryId: editing.categoryName, // display only
        title: editing.title,
        minPrice: String(editing.minPrice),
        maxPrice: String(editing.maxPrice),
        description: '',
        location: editing.location,
      });
    }
  }, [editing]);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.categoryId && !editing) e.categoryId = 'Vui lòng chọn ngành hàng';
    if (!form.title.trim()) e.title = 'Tiêu đề không được để trống';
    if (!form.minPrice || Number(form.minPrice) < 0) e.minPrice = 'Giá tối thiểu không hợp lệ';
    if (!form.maxPrice || Number(form.maxPrice) < Number(form.minPrice))
      e.maxPrice = 'Giá tối đa phải ≥ giá tối thiểu';
    if (!form.description.trim()) e.description = 'Mô tả không được để trống';
    if (!form.location.trim()) e.location = 'Địa điểm không được để trống';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    if (editing) {
      await updateMutation.mutateAsync({
        id: editing.id,
        title: form.title,
        minPrice: Number(form.minPrice),
        maxPrice: Number(form.maxPrice),
        description: form.description,
        location: form.location,
      });
    } else {
      await createMutation.mutateAsync({
        categoryId: form.categoryId,
        title: form.title,
        minPrice: Number(form.minPrice),
        maxPrice: Number(form.maxPrice),
        description: form.description,
        location: form.location,
      });
    }
    onClose();
  };

  const set = (field: keyof typeof INITIAL_FORM) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    setForm(prev => ({ ...prev, [field]: e.target.value }));
    if (errors[field]) setErrors(prev => ({ ...prev, [field]: '' }));
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div>
            <h2 className="text-lg font-bold text-slate-900">
              {editing ? 'Chỉnh Sửa Gói Dịch Vụ' : '+ Đăng Gói Dịch Vụ Mới'}
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Sau khi tạo, bài đăng ở trạng thái <span className="font-semibold text-amber-600">Nháp</span> — hãy nộp duyệt khi sẵn sàng (SLA 24h)
            </p>
          </div>
          <button onClick={onClose} className="p-2 rounded-lg hover:bg-slate-100 text-slate-500 transition">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          {/* Ngành hàng */}
          {!editing && (
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                Ngành Hàng <span className="text-red-500">*</span>
              </label>
              <select
                value={form.categoryId}
                onChange={set('categoryId')}
                className={`w-full px-3 py-2.5 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition ${errors.categoryId ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
              >
                <option value="">— Chọn ngành hàng dịch vụ cưới —</option>
                {categories.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
              {errors.categoryId && <FieldError msg={errors.categoryId} />}
            </div>
          )}

          {/* Tiêu đề */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
              Tiêu Đề Gói Dịch Vụ <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={form.title}
              onChange={set('title')}
              placeholder="Vd: Sảnh Tiệc Pha Lê Hoàng Gia 350 Khách"
              maxLength={200}
              className={`w-full px-3 py-2.5 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition ${errors.title ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
            />
            {errors.title && <FieldError msg={errors.title} />}
          </div>

          {/* Khoảng giá */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                Giá Tối Thiểu (VNĐ) <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                value={form.minPrice}
                onChange={set('minPrice')}
                placeholder="12500000"
                min={0}
                className={`w-full px-3 py-2.5 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition ${errors.minPrice ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
              />
              {errors.minPrice && <FieldError msg={errors.minPrice} />}
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                Giá Tối Đa (VNĐ) <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                value={form.maxPrice}
                onChange={set('maxPrice')}
                placeholder="25000000"
                min={0}
                className={`w-full px-3 py-2.5 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition ${errors.maxPrice ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
              />
              {errors.maxPrice && <FieldError msg={errors.maxPrice} />}
            </div>
          </div>

          {/* Địa điểm */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
              Địa Điểm / Khu Vực Hoạt Động <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={form.location}
              onChange={set('location')}
              placeholder="Vd: Quận 1, TP. Hồ Chí Minh"
              className={`w-full px-3 py-2.5 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition ${errors.location ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
            />
            {errors.location && <FieldError msg={errors.location} />}
          </div>

          {/* Mô tả */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
              Mô Tả Chi Tiết <span className="text-red-500">*</span>
            </label>
            <textarea
              value={form.description}
              onChange={set('description')}
              placeholder="Mô tả chi tiết về gói dịch vụ, điểm nổi bật, tiện ích đi kèm..."
              rows={5}
              maxLength={5000}
              className={`w-full px-3 py-2.5 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition resize-none ${errors.description ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
            />
            <div className="text-right text-xs text-slate-400 mt-1">{form.description.length}/5000</div>
            {errors.description && <FieldError msg={errors.description} />}
          </div>

          {/* Footer actions */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-slate-600 rounded-xl hover:bg-slate-100 transition"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="inline-flex items-center gap-2 px-5 py-2 rounded-xl bg-rose-500 hover:bg-rose-600 text-white text-sm font-semibold shadow-sm transition disabled:opacity-60"
            >
              {isLoading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <PlusCircle className="w-4 h-4" />
              )}
              {editing ? 'Lưu Thay Đổi' : 'Tạo Bài Đăng (Nháp)'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function FieldError({ msg }: { msg: string }) {
  return (
    <p className="mt-1 text-xs text-red-600 flex items-center gap-1">
      <AlertCircle className="w-3 h-3" /> {msg}
    </p>
  );
}
