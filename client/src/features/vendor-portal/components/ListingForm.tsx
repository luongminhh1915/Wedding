import { useState, useEffect, useRef } from 'react';
import {
  X, PlusCircle, Loader2, AlertCircle, Send, CheckCircle2,
  Sparkles, UploadCloud, Image as ImageIcon, Trash2, Star, Plus
} from 'lucide-react';
import { useCreateListing, useUpdateListing, useSubmitListing } from '../hooks/useListings';
import axiosClient from '../../../services/axiosClient';
import type { ListingSummaryDto } from '../../../types/listing.types';

interface Props {
  editing?: ListingSummaryDto;
  categories: { id: string; name: string }[];
  onClose: () => void;
}

interface ImageItem {
  id: string;
  preview: string; // ObjectURL hoặc URL hiển thị chính xác ảnh người dùng chọn
  url: string;     // URL lưu trữ để gửi lên server
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
  const [images, setImages] = useState<ImageItem[]>([]);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');
  const [urlInput, setUrlInput] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [apiError, setApiError] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Sau khi tạo: lưu id để hỏi có nộp duyệt không
  const [createdId, setCreatedId] = useState<string | null>(null);
  const [showSubmitConfirm, setShowSubmitConfirm] = useState(false);

  const createMutation = useCreateListing();
  const updateMutation = useUpdateListing();
  const submitMutation = useSubmitListing();
  const isLoading = createMutation.isPending || updateMutation.isPending || isUploading;

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
      if (editing.primaryImageUrl) {
        setImages([{
          id: 'initial-1',
          preview: editing.primaryImageUrl,
          url: editing.primaryImageUrl,
        }]);
      }
    }
  }, [editing]);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.categoryId && !editing) e.categoryId = 'Vui lòng chọn ngành hàng';
    if (!form.title.trim()) e.title = 'Tiêu đề không được để trống';
    if (!form.minPrice || Number(form.minPrice) < 0) e.minPrice = 'Giá tối thiểu không hợp lệ';
    if (!form.maxPrice || Number(form.maxPrice) < Number(form.minPrice))
      e.maxPrice = 'Giá tối đa phải ≥ giá tối thiểu';
    if (!form.location.trim()) e.location = 'Địa điểm không được để trống';
    if (!form.description.trim()) e.description = 'Mô tả không được để trống';

    // Ràng buộc hình ảnh: Bắt buộc tối thiểu 1 ảnh, nhiều nhất là 10 bức ảnh
    if (images.length === 0) {
      e.images = 'Bắt buộc phải tải lên ít nhất 1 hình ảnh dịch vụ.';
    } else if (images.length > 10) {
      e.images = 'Số lượng ảnh tối đa là 10 bức ảnh.';
    }

    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const fileList = e.target.files;
    if (!fileList || fileList.length === 0) return;

    const files = Array.from(fileList);

    if (images.length + files.length > 10) {
      setUploadError(`Bạn chỉ có thể chọn tối đa thêm ${10 - images.length} ảnh nữa (tổng tối đa 10 ảnh).`);
      return;
    }

    setUploadError('');
    setIsUploading(true);

    // Tạo preview trực tiếp từ file trên máy người dùng (hiển thị ngay lập tức, 100% đúng ảnh)
    const newItems: ImageItem[] = files.map(file => ({
      id: Math.random().toString(36).substring(7),
      preview: URL.createObjectURL(file),
      url: '', // sẽ được cập nhật sau khi server upload xong
    }));

    try {
      const formData = new FormData();
      for (const file of files) {
        formData.append('files', file);
      }

      const res = await axiosClient.post('/api/upload/images', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });

      const serverUrls: string[] = res.data.urls || [];
      newItems.forEach((item, idx) => {
        item.url = serverUrls[idx] || item.preview;
      });

      setImages(prev => [...prev, ...newItems].slice(0, 10));
      if (errors.images) setErrors(prev => ({ ...prev, images: '' }));
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      setUploadError(error.response?.data?.message || 'Tải ảnh lên thất bại. Vui lòng thử lại.');
    } finally {
      setIsUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const handleAddUrl = () => {
    const url = urlInput.trim();
    if (!url) return;
    if (images.length >= 10) {
      setUploadError('Đã đạt số lượng tối đa 10 bức ảnh.');
      return;
    }
    setUploadError('');
    setImages(prev => [
      ...prev,
      { id: Math.random().toString(36).substring(7), preview: url, url },
    ]);
    setUrlInput('');
    if (errors.images) setErrors(prev => ({ ...prev, images: '' }));
  };

  const handleRemoveImage = (index: number) => {
    setImages(prev => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;
    setApiError('');

    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          title: form.title,
          minPrice: Number(form.minPrice),
          maxPrice: Number(form.maxPrice),
          description: form.description,
          location: form.location,
        });
        onClose();
      } else {
        // Tạo bài mới kèm danh sách ảnh → hiện modal xác nhận nộp duyệt
        const result = await createMutation.mutateAsync({
          categoryId: form.categoryId,
          title: form.title,
          minPrice: Number(form.minPrice),
          maxPrice: Number(form.maxPrice),
          description: form.description,
          location: form.location,
          imageUrls: images.map(img => img.url),
        });
        setCreatedId(result.listingId);
        setShowSubmitConfirm(true);
      }
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string; errors?: Record<string, string[]> } } };
      const msg = error.response?.data?.message
        || Object.values(error.response?.data?.errors ?? {}).flat().join(', ')
        || 'Đã xảy ra lỗi. Vui lòng thử lại.';
      setApiError(msg);
    }
  };

  const handleSubmitNow = async () => {
    if (!createdId) return;
    try {
      await submitMutation.mutateAsync(createdId);
      onClose();
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      setApiError(error.response?.data?.message || 'Không thể nộp duyệt. Vui lòng thử lại.');
    }
  };

  const handleSaveDraft = () => {
    onClose();
  };

  const set = (field: keyof typeof INITIAL_FORM) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    setForm(prev => ({ ...prev, [field]: e.target.value }));
    if (errors[field]) setErrors(prev => ({ ...prev, [field]: '' }));
  };

  // ── Modal xác nhận nộp duyệt (hiện sau khi tạo bài thành công) ────────────
  if (showSubmitConfirm) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md p-8 text-center animate-in fade-in zoom-in-95 duration-150">
          {/* Success icon */}
          <div className="w-16 h-16 bg-emerald-100 rounded-2xl flex items-center justify-center mx-auto mb-5">
            <CheckCircle2 className="w-8 h-8 text-emerald-500" />
          </div>

          {/* Error in confirm modal */}
          {apiError && (
            <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl text-xs text-red-700 flex items-start gap-2 text-left">
              <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
              <span>{apiError}</span>
            </div>
          )}

          <h2 className="text-xl font-bold text-slate-900 mb-2">Tạo Bài Đăng Thành Công!</h2>
          <p className="text-sm text-slate-500 mb-6">
            Bài đăng đã được lưu cùng {images.length} hình ảnh ở trạng thái <span className="font-semibold text-amber-600">Nháp</span>.{' '}
            Bạn có muốn{' '}
            <span className="font-semibold text-rose-600">nộp duyệt ngay</span>{' '}
            để gửi bài lên hàng đợi kiểm duyệt của Admin không?
          </p>

          {/* Info box */}
          <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 mb-6 text-left">
            <div className="flex items-start gap-3">
              <Sparkles className="w-5 h-5 text-amber-500 flex-shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-semibold text-amber-800">Sau khi nộp duyệt (BR-008):</p>
                <ul className="text-xs text-amber-700 mt-1.5 space-y-1">
                  <li>• Moderator / Admin sẽ xem xét trong tối đa <strong>24 giờ</strong></li>
                  <li>• Bài được duyệt → hiển thị công khai cho khách hàng</li>
                  <li>• Bị từ chối → bạn nhận được lý do và có thể chỉnh sửa lại</li>
                </ul>
              </div>
            </div>
          </div>

          {/* Action buttons */}
          <div className="flex flex-col gap-3">
            <button
              onClick={handleSubmitNow}
              disabled={submitMutation.isPending}
              className="w-full inline-flex items-center justify-center gap-2 py-3 px-5 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-semibold text-sm shadow-md shadow-rose-200 transition disabled:opacity-60"
            >
              {submitMutation.isPending ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <Send className="w-4 h-4" />
              )}
              {submitMutation.isPending ? 'Đang gửi duyệt...' : '📨 Nộp Duyệt Ngay (Gửi Admin)'}
            </button>

            <button
              onClick={handleSaveDraft}
              disabled={submitMutation.isPending}
              className="w-full py-3 px-5 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-600 font-medium text-sm transition disabled:opacity-60"
            >
              Lưu Nháp — Nộp Sau
            </button>
          </div>
        </div>
      </div>
    );
  }

  // ── Form tạo / chỉnh sửa ──────────────────────────────────────────────────
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
              Sau khi tạo, hệ thống sẽ hỏi bạn có muốn{' '}
              <span className="font-semibold text-rose-600">nộp duyệt ngay cho Admin</span>{' '}
              hay lưu Nháp trước (SLA 24h)
            </p>
          </div>
          <button onClick={onClose} className="p-2 rounded-lg hover:bg-slate-100 text-slate-500 transition">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          {/* API Error */}
          {apiError && (
            <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-xs text-red-700 flex items-start gap-2">
              <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
              <span>{apiError}</span>
            </div>
          )}

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
              rows={4}
              maxLength={5000}
              className={`w-full px-3 py-2.5 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition resize-none ${errors.description ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
            />
            <div className="text-right text-xs text-slate-400 mt-1">{form.description.length}/5000</div>
            {errors.description && <FieldError msg={errors.description} />}
          </div>

          {/* ─── HÌNH ẢNH DỊCH VỤ (BẮT BUỘC 1 ĐẾN 10 ẢNH) ──────────────────── */}
          <div className="bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div className="flex items-center justify-between mb-2">
              <label className="block text-sm font-semibold text-slate-800">
                Hình Ảnh Dịch Vụ <span className="text-red-500">*</span>
                <span className="text-xs font-normal text-slate-500 ml-1.5">
                  (Bắt buộc tối thiểu 1 ảnh, tối đa 10 ảnh)
                </span>
              </label>
              <span className={`text-xs px-2.5 py-0.5 rounded-full font-bold ${
                images.length >= 1 && images.length <= 10
                  ? 'bg-emerald-100 text-emerald-700'
                  : images.length > 10
                  ? 'bg-red-100 text-red-700'
                  : 'bg-rose-100 text-rose-700'
              }`}>
                {images.length}/10 ảnh
              </span>
            </div>

            {/* Error notifications */}
            {errors.images && (
              <div className="mb-3">
                <FieldError msg={errors.images} />
              </div>
            )}
            {uploadError && (
              <div className="mb-3 p-2.5 bg-red-50 border border-red-200 rounded-xl text-xs text-red-700 flex items-center gap-1.5">
                <AlertCircle className="w-4 h-4 flex-shrink-0" />
                <span>{uploadError}</span>
              </div>
            )}

            {/* Upload Area / Dropzone */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
              {/* File upload button box */}
              <div>
                <input
                  type="file"
                  ref={fileInputRef}
                  onChange={handleFileUpload}
                  multiple
                  accept="image/png, image/jpeg, image/jpg, image/webp"
                  className="hidden"
                  disabled={images.length >= 10 || isUploading}
                />
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={images.length >= 10 || isUploading}
                  className="w-full flex items-center justify-center gap-2.5 py-3 px-4 rounded-xl border-2 border-dashed border-rose-300 hover:border-rose-400 bg-rose-50/50 hover:bg-rose-50 text-rose-700 text-xs font-semibold transition disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isUploading ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin text-rose-600" />
                      <span>Đang tải ảnh lên...</span>
                    </>
                  ) : (
                    <>
                      <UploadCloud className="w-4 h-4 text-rose-500" />
                      <span>Chọn ảnh từ máy tính (Tối đa 10 ảnh)</span>
                    </>
                  )}
                </button>
              </div>

              {/* Direct image URL input */}
              <div className="flex gap-2">
                <input
                  type="url"
                  placeholder="Hoặc dán URL ảnh trực tiếp..."
                  value={urlInput}
                  onChange={(e) => setUrlInput(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleAddUrl(); } }}
                  disabled={images.length >= 10 || isUploading}
                  className="flex-1 px-3 py-2 text-xs bg-white border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-rose-300 disabled:opacity-50"
                />
                <button
                  type="button"
                  onClick={handleAddUrl}
                  disabled={!urlInput.trim() || images.length >= 10 || isUploading}
                  className="px-3 py-2 bg-slate-800 hover:bg-slate-900 disabled:bg-slate-300 text-white text-xs font-semibold rounded-xl transition flex items-center gap-1"
                >
                  <Plus className="w-3.5 h-3.5" />
                  Thêm
                </button>
              </div>
            </div>

            {/* Images Preview Grid */}
            {images.length > 0 ? (
              <div className="grid grid-cols-2 sm:grid-cols-5 gap-2.5 pt-2">
                {images.map((item, idx) => (
                  <div
                    key={item.id || idx}
                    className="relative group aspect-square rounded-xl overflow-hidden border border-slate-200 bg-slate-100 shadow-xs"
                  >
                    <img
                      src={item.preview}
                      alt={`Ảnh ${idx + 1}`}
                      className="w-full h-full object-cover"
                    />

                    {/* Order badge / Featured badge */}
                    {idx === 0 ? (
                      <span className="absolute top-1.5 left-1.5 px-1.5 py-0.5 bg-rose-600/90 backdrop-blur-xs text-white text-[10px] font-bold rounded flex items-center gap-0.5 shadow-xs">
                        <Star className="w-2.5 h-2.5 fill-white" />
                        Ảnh bìa
                      </span>
                    ) : (
                      <span className="absolute top-1.5 left-1.5 px-1.5 py-0.5 bg-slate-900/70 backdrop-blur-xs text-white text-[10px] font-bold rounded shadow-xs">
                        #{idx + 1}
                      </span>
                    )}

                    {/* Remove button */}
                    <button
                      type="button"
                      onClick={() => handleRemoveImage(idx)}
                      className="absolute top-1.5 right-1.5 w-6 h-6 rounded-full bg-red-600 text-white flex items-center justify-center opacity-90 hover:opacity-100 hover:scale-110 transition shadow-sm"
                      title="Xóa ảnh này"
                    >
                      <Trash2 className="w-3 h-3" />
                    </button>
                  </div>
                ))}
              </div>
            ) : (
              <div className="py-6 border border-dashed border-slate-200 rounded-xl text-center text-slate-400 text-xs flex flex-col items-center justify-center gap-1.5 bg-white">
                <ImageIcon className="w-7 h-7 text-slate-300" />
                <span>Chưa có ảnh nào được thêm.</span>
                <span className="text-[11px] text-rose-500 font-medium">* Bắt buộc phải có ít nhất 1 ảnh để đăng bài</span>
              </div>
            )}
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
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-rose-500 hover:bg-rose-600 text-white text-sm font-semibold shadow-sm transition disabled:opacity-60"
            >
              {isLoading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <PlusCircle className="w-4 h-4" />
              )}
              {editing ? 'Lưu Thay Đổi' : 'Tạo Bài Đăng'}
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
