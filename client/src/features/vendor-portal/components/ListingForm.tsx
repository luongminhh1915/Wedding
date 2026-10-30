import { useState, useEffect, useRef } from 'react';
import {
  X, PlusCircle, Loader2, AlertCircle, Send, CheckCircle2,
  Sparkles, UploadCloud, Image as ImageIcon, Trash2, Star, Plus,
  Layers, Type, Coins, MapPin, FileText, PanelLeftClose, PanelLeftOpen,
  FolderPlus, LayoutGrid, Check
} from 'lucide-react';
import { useCreateListing, useUpdateListing, useSubmitListing, useListingDetail } from '../hooks/useListings';
import axiosClient from '../../../services/axiosClient';
import type { ListingSummaryDto } from '../../../types/listing.types';
import { EMPTY_LISTING_FORM, hydrateListingEditor, isListingImageReference, listingImagePreview, listingUpdatePayload, moveListingCover, type ListingEditorState, type ListingImageItem } from './listingEditModel';
import { isServiceStudioImage, normalizeServiceDetails, serviceStudioImageSrc, validateServiceDetails } from '../../listings/serviceDetailsModel';
import { ServiceDetailsEditor } from './ServiceDetailsEditor';
import { MoneyInput } from '../../../components/MoneyInput';
import { listingUpdatedTime } from '../vendorListingFilters';
import { useVendorCategories } from '../hooks/useVendorCategories';
import { useVendorBusinessGroupPolicy } from '../hooks/useBusinessGroups';
import { vendorCategoryError, vendorCategoryGroups } from '../vendorCategoryModel';
import { ServiceCategoryPolicySummary } from './ServiceCategoryPolicySummary';
import { ServiceItemManager } from '../../journey/vendor/ServiceItemManager';
import { listingPricingPayload, listingPricingValues } from './listingPricingModel';
import { useAuthStore } from '../../../store/authStore';
import { ListingLivePreview, type ServiceCollectionItem, type ServiceLatestUpdate } from './ListingLivePreview';
import { listingStudioActorIsCurrent, uploadStudioImages, validateStudioUploadFiles } from './listingStudioUpload';
import { prepareInvitationImage } from '../../journey/invitations/invitationImages';

interface Props {
  editing?: ListingSummaryDto;
  categories: { id: string; name: string; requiresCommissionBasisApproval?: boolean }[];
  onClose: () => void;
}

export default function ListingForm(props: Props) {
  const [editorSession] = useState(() => crypto.randomUUID());
  const detail = useListingDetail(props.editing?.id ?? '', editorSession);
  let initial: ListingEditorState | undefined;
  let loadError = '';
  if (props.editing && detail.data) {
    try { initial = hydrateListingEditor(detail.data, props.editing.id, import.meta.env.VITE_API_URL || ''); }
    catch (error) { loadError = (error as Error).message; }
  }
  if (props.editing && !initial) return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4" role="dialog" aria-modal="true" aria-label="Tải bài đăng để chỉnh sửa">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md p-6">
        <div className="flex justify-between items-center mb-4"><h2 className="font-bold">Chỉnh sửa gói dịch vụ</h2><button onClick={props.onClose} aria-label="Đóng"><X size={20} /></button></div>
        {detail.isPending || detail.isFetching ? <p role="status" className="flex gap-2 text-sm text-slate-600"><Loader2 className="animate-spin" size={18} />Đang tải mô tả và toàn bộ ảnh đã lưu...</p> : <><p role="alert" className="text-sm text-red-700">{loadError || 'Không tải được bài đăng. Chưa có thay đổi nào được lưu; vui lòng thử lại.'}</p><button type="button" className="mt-4 px-4 py-2 rounded-xl bg-slate-800 text-white text-sm" onClick={() => void detail.refetch()}>Thử lại</button></>}
      </div>
    </div>
  );
  return <ListingEditor key={props.editing?.id ?? 'new-listing'} {...props} initial={initial} />;
}

function ListingEditor({ editing, categories, onClose, initial }: Props & { initial?: ListingEditorState }) {
  const currentUser = useAuthStore(state => state.user);
  const currentRevision = useAuthStore(state => state.sessionRevision);
  const currentToken = useAuthStore(state => state.token);
  const [actor] = useState(() => { const session = useAuthStore.getState(); return { id: session.user?.id, token: session.token, revision: session.sessionRevision }; });
  const mounted = useRef(true);
  const uploadFlight = useRef(false);
  const saveFlight = useRef(false);
  const uploadController = useRef<AbortController | null>(null);
  const isCurrentActor = () => mounted.current && listingStudioActorIsCurrent(actor, useAuthStore.getState());
  const categoryQuery = useVendorCategories();
  const policyQuery = useVendorBusinessGroupPolicy();
  const selectableCategories = categoryQuery.isError ? [] : categoryQuery.data?.categories ?? [];
  const published = editing?.status === 'Active' || editing?.status === 'Hidden';
  // The keyed editor mounts only after a complete detail response. Never hydrate through an effect:
  // a cache refresh or mutation invalidation must not replace text/images the vendor is editing.
  const [form, setForm] = useState(() => initial?.form ?? { ...EMPTY_LISTING_FORM });
  const [images, setImages] = useState<ListingImageItem[]>(() => initial?.images ?? []);
  const originalImages = useRef(initial?.images ?? []);
  const [serviceDetails, setServiceDetails] = useState(() => normalizeServiceDetails(initial?.serviceDetails));
  const originalServiceDetails = useRef(initial?.serviceDetails ?? null);
  const [pricing, setPricing] = useState(() => listingPricingValues(initial?.pricing));
  const [pricingChanged, setPricingChanged] = useState(!editing);
  const selectedCategory = [...selectableCategories, ...(categoryQuery.data?.existingCategories ?? [])].find(category => category.id === form.categoryId);
  const fullServiceEnabled = !!initial?.categoryRequiresCommissionBasisApproval || !!selectedCategory?.requiresCommissionBasisApproval || !!categories.find(category => category.id === form.categoryId)?.requiresCommissionBasisApproval;
  const previewUrls = useRef(new Set<string>());
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');
  const [urlInput, setUrlInput] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [apiError, setApiError] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Sau khi tạo: lưu id để hỏi có nộp duyệt không
  const [createdId, setCreatedId] = useState<string | null>(null);
  const [showSubmitConfirm, setShowSubmitConfirm] = useState(false);
  const [mobileTab, setMobileTab] = useState<'form' | 'preview'>('form');
  const [formCollapsed, setFormCollapsed] = useState(false);

  // Bộ sưu tập (Collections) & Cập nhật mới nhất (Latest Updates)
  const [collections, setCollections] = useState<ServiceCollectionItem[]>(() => normalizeServiceDetails(initial?.serviceDetails).collections ?? []);
  const [latestUpdate, setLatestUpdate] = useState<ServiceLatestUpdate>(() => normalizeServiceDetails(initial?.serviceDetails).latestUpdate ?? { content: '', imageUrl: '' });
  const currentDetails = () => normalizeServiceDetails({ ...serviceDetails, collections, latestUpdate });

  // Dialog Bộ sưu tập
  const [isCollectionModalOpen, setIsCollectionModalOpen] = useState(false);
  const [collectionNameInput, setCollectionNameInput] = useState('');
  const [selectedCollectionImages, setSelectedCollectionImages] = useState<string[]>([]);
  const [isUploadingAux, setIsUploadingAux] = useState(false);
  const [pendingPreviews, setPendingPreviews] = useState<string[]>([]);

  // Dialog Cập nhật mới nhất
  const [isUpdateModalOpen, setIsUpdateModalOpen] = useState(false);
  const [updateContentInput, setUpdateContentInput] = useState('');
  const [updateImageUrlInput, setUpdateImageUrlInput] = useState('');

  // Local previews are transient UI only. Only complete server URLs enter saved metadata.
  const uploadFiles = async (fileList: FileList | File[], auxiliary = true): Promise<string[]> => {
    if (uploadFlight.current || saveFlight.current || !isCurrentActor()) return [];
    const files = Array.from(fileList);
    if (files.length === 0) return [];
    try { validateStudioUploadFiles(files); } catch (error) { setUploadError((error as Error).message); return []; }
    uploadFlight.current = true;
    setUploadError('');
    if (auxiliary) setIsUploadingAux(true); else setIsUploading(true);
    const controller = new AbortController(); uploadController.current = controller;
    const previews = files.map(file => { const preview = URL.createObjectURL(file); previewUrls.current.add(preview); return preview; });
    setPendingPreviews(previews);
    try {
      return await uploadStudioImages(files, {
        isCurrent: isCurrentActor, signal: controller.signal, prepare: prepareInvitationImage,
        send: async (prepared, signal) => {
          const body = new FormData(); prepared.forEach(file => body.append('files', file));
          const response = await axiosClient.post('/api/upload/images', body, { signal, headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${actor.token}` } });
          return response.data.urls;
        },
      });
    } catch (error) {
      if (isCurrentActor() && !controller.signal.aborted) {
        const failure = error as { response?: { data?: { message?: string } }; message?: string };
        setUploadError(failure.response?.data?.message || failure.message || 'Không tải được ảnh. Hãy thử lại; ảnh tạm chưa được lưu.');
      }
      return [];
    } finally {
      for (const preview of previews) { URL.revokeObjectURL(preview); previewUrls.current.delete(preview); }
      uploadFlight.current = false; uploadController.current = null;
      if (mounted.current) { setPendingPreviews([]); setIsUploadingAux(false); setIsUploading(false); }
    }
  };

  const uploadFeedback = <>{uploadError && <p role="alert" className="my-2 rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-700">{uploadError}</p>}{pendingPreviews.length > 0 && <div role="status" className="my-3 space-y-2"><p className="text-xs text-slate-600">Đang tối ưu và lưu ảnh lên máy chủ…</p><div className="flex gap-2 overflow-x-auto">{pendingPreviews.map(url => <img key={url} src={url} alt="Ảnh đang tải, chưa lưu" className="h-16 w-16 rounded-lg object-cover opacity-70" />)}</div></div>}</>;
  const closeEditor = () => { if (saveFlight.current) return; uploadController.current?.abort(); onClose(); };

  const handleOpenCreateCollection = () => {
    if (uploadFlight.current || saveFlight.current || !isCurrentActor()) return;
    setUploadError('');
    setCollectionNameInput('');
    setSelectedCollectionImages([]);
    setIsCollectionModalOpen(true);
  };

  const handleCollectionFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const input = e.currentTarget;
    if (selectedCollectionImages.length + input.files!.length > 10) { setUploadError('Mỗi bộ sưu tập có tối đa 10 ảnh.'); input.value = ''; return; }
    const uploadedUrls = await uploadFiles(input.files!);
    if (isCurrentActor()) setSelectedCollectionImages(prev => [...new Set([...prev, ...uploadedUrls])]);
    input.value = '';
  };

  const handleCreateCollection = () => {
    const name = collectionNameInput.trim();
    if (uploadFlight.current || !isCurrentActor()) return;
    if (!name || name.length > 100 || !selectedCollectionImages.length || selectedCollectionImages.length > 10 || selectedCollectionImages.some(url => !isServiceStudioImage(url)) || collections.length >= 10) {
      setUploadError('Đặt tên tối đa 100 ký tự và chọn 1–10 ảnh đã tải thành công. Tối đa 10 bộ sưu tập.'); return;
    }
    const newCol: ServiceCollectionItem = {
      id: crypto.randomUUID(),
      name,
      images: [...selectedCollectionImages],
    };
    setCollections(prev => [...prev, newCol]);
    setIsCollectionModalOpen(false);
  };

  const handleDeleteCollection = (id: string) => {
    setCollections(prev => prev.filter(c => c.id !== id));
  };

  const handleToggleCollectionImage = (url: string) => {
    if (uploadFlight.current || !isServiceStudioImage(url)) return;
    if (!selectedCollectionImages.includes(url) && selectedCollectionImages.length >= 10) { setUploadError('Mỗi bộ sưu tập có tối đa 10 ảnh.'); return; }
    setSelectedCollectionImages(prev =>
      prev.includes(url) ? prev.filter(u => u !== url) : [...prev, url]
    );
  };

  const handleOpenUpdateEditor = () => {
    if (uploadFlight.current || saveFlight.current || !isCurrentActor()) return;
    setUploadError('');
    setUpdateContentInput(latestUpdate.content || '');
    setUpdateImageUrlInput(latestUpdate.imageUrl || '');
    setIsUpdateModalOpen(true);
  };

  const handleUpdateFileUploadForm = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const input = e.currentTarget;
    const uploadedUrls = await uploadFiles(input.files!);
    if (isCurrentActor() && uploadedUrls[0]) {
      setLatestUpdate(prev => ({ ...prev, imageUrl: uploadedUrls[0] }));
    }
    input.value = '';
  };

  const handleUpdateFileUploadModal = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const input = e.currentTarget;
    const uploadedUrls = await uploadFiles(input.files!);
    if (isCurrentActor() && uploadedUrls[0]) {
      setUpdateImageUrlInput(uploadedUrls[0]);
    }
    input.value = '';
  };

  const handleSaveUpdate = () => {
    if (uploadFlight.current || !isCurrentActor()) return;
    if (updateContentInput.trim().length > 4000 || updateImageUrlInput && !isServiceStudioImage(updateImageUrlInput)) { setUploadError('Cập nhật tối đa 4000 ký tự; hãy chọn ảnh đã được lưu thành công.'); return; }
    setLatestUpdate({
      content: updateContentInput.trim(),
      imageUrl: updateImageUrlInput.trim(),
    });
    setIsUpdateModalOpen(false);
  };

  const createMutation = useCreateListing(actor);
  const updateMutation = useUpdateListing(actor);
  const submitMutation = useSubmitListing(actor);
  const actorIsActive = currentUser?.id === actor.id && currentUser?.role === 'VendorOwner' && currentRevision === actor.revision && currentToken === actor.token;
  const isLoading = createMutation.isPending || updateMutation.isPending || isUploading || isUploadingAux || !actorIsActive;

  useEffect(() => {
    mounted.current = true;
    const previousBodyOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const urls = previewUrls.current;
    return () => {
      mounted.current = false;
      uploadController.current?.abort();
      for (const url of urls) URL.revokeObjectURL(url);
      urls.clear();
      document.body.style.overflow = previousBodyOverflow;
    };
  }, []);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!editing) {
      const categoryError = vendorCategoryError(form.categoryId, categoryQuery.data?.categories, categoryQuery.isPending, categoryQuery.isError);
      if (categoryError) e.categoryId = categoryError;
    }
    if (!form.title.trim()) e.title = 'Tiêu đề không được để trống';
    if (!form.minPrice || Number(form.minPrice) < 0) e.minPrice = 'Giá tối thiểu không hợp lệ';
    if (!form.maxPrice || Number(form.maxPrice) < Number(form.minPrice))
      e.maxPrice = 'Giá tối đa phải ≥ giá tối thiểu';
    if (!form.location.trim()) e.location = 'Địa điểm không được để trống';
    if (!form.description.trim()) e.description = 'Mô tả không được để trống';

    // Ràng buộc hình ảnh: Bắt buộc tối thiểu 1 ảnh, nhiều nhất là 10 bức ảnh
    if (images.length === 0 && (!editing || originalImages.current.length > 0)) {
      e.images = 'Bắt buộc phải tải lên ít nhất 1 hình ảnh dịch vụ.';
    } else if (images.length > 10 && (!editing || images.length !== originalImages.current.length)) {
      e.images = 'Số lượng ảnh tối đa là 10 bức ảnh.';
    }
    if (!editing || JSON.stringify(currentDetails()) !== JSON.stringify(normalizeServiceDetails(originalServiceDetails.current)))
      Object.assign(e, validateServiceDetails(currentDetails()));
    if (pricingChanged) {
      try { listingPricingPayload(pricing); } catch (error) { e.pricing = (error as Error).message; }
    }

    setErrors(e);
    if (Object.keys(e).length > 0) setMobileTab('form');
    return Object.keys(e).length === 0;
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const input = e.currentTarget;
    if (!input.files?.length) return;
    const files = Array.from(input.files);
    if (images.length + files.length > 10) { setUploadError(`Tổng tối đa 10 ảnh dịch vụ.`); input.value = ''; return; }
    const urls = await uploadFiles(files, false);
    if (isCurrentActor() && urls.length) {
      const items = urls.map(url => ({ id: crypto.randomUUID(), preview: listingImagePreview(url, import.meta.env.VITE_API_URL || ''), url }));
      setImages(current => [...current, ...items]);
      setErrors(current => ({ ...current, images: '' }));
    }
    input.value = '';
  };

  const handleAddUrl = () => {
    const url = urlInput.trim();
    if (!url) return;
    if (!isListingImageReference(url)) { setUploadError('Dùng ảnh HTTP(S) hợp lệ hoặc tải ảnh từ thiết bị; không dùng đường dẫn tạm hay đường dẫn chứa mã lệnh.'); return; }
    if (images.some(image => image.url === url)) { setUploadError('Ảnh này đã có trong album.'); return; }
    if (images.length >= 10) {
      setUploadError('Đã đạt số lượng tối đa 10 bức ảnh.');
      return;
    }
    setUploadError('');
    setImages(prev => [
      ...prev,
      { id: crypto.randomUUID(), preview: listingImagePreview(url, import.meta.env.VITE_API_URL || ''), url },
    ]);
    setUrlInput('');
    if (errors.images) setErrors(prev => ({ ...prev, images: '' }));
  };

  const handleRemoveImage = (index: number) => {
    const preview = images[index]?.preview;
    if (preview && previewUrls.current.has(preview)) { URL.revokeObjectURL(preview); previewUrls.current.delete(preview); }
    setImages(prev => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isLoading || uploadFlight.current || saveFlight.current || !isCurrentActor()) return;
    if (!validate()) return;
    saveFlight.current = true;
    setApiError('');

    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          ...listingUpdatePayload(form, images, originalImages.current, currentDetails(), originalServiceDetails.current),
          ...(pricingChanged ? { pricing: listingPricingPayload(pricing) } : {}),
        });
        if (isCurrentActor()) onClose();
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
          serviceDetails: currentDetails(),
          pricing: listingPricingPayload(pricing),
        });
        if (isCurrentActor()) { setCreatedId(result.listingId); setShowSubmitConfirm(true); }
      }
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string; errors?: Record<string, string[]> } } };
      const msg = error.response?.data?.message
        || Object.values(error.response?.data?.errors ?? {}).flat().join(', ')
        || (err as Error).message
        || 'Đã xảy ra lỗi. Vui lòng thử lại.';
      if (isCurrentActor()) setApiError(msg);
    } finally { saveFlight.current = false; }
  };

  const handleSubmitNow = async () => {
    if (!createdId || saveFlight.current || !isCurrentActor()) return;
    saveFlight.current = true;
    try {
      await submitMutation.mutateAsync(createdId);
      if (isCurrentActor()) onClose();
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      if (isCurrentActor()) setApiError(error.response?.data?.message || 'Không thể nộp duyệt. Vui lòng thử lại.');
    } finally { saveFlight.current = false; }
  };

  const handleSaveDraft = () => {
    closeEditor();
  };

  const set = (field: keyof typeof EMPTY_LISTING_FORM) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
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
                  <li>• Moderator / Admin sẽ xem xét nội dung và danh mục; theo dõi trạng thái trong shop</li>
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
    <div className="fixed inset-0 z-50 flex h-dvh w-full max-w-full flex-col overflow-hidden bg-slate-100" role="dialog" aria-modal="true" aria-label={editing ? 'Chỉnh sửa dịch vụ' : 'Tạo dịch vụ mới'}>
      {/* Top Header */}
      <div className="flex flex-wrap gap-3 items-center justify-between px-3 sm:px-6 py-3.5 border-b border-slate-200 bg-white shrink-0 shadow-2xs z-10">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={closeEditor}
            className="p-2 rounded-xl hover:bg-slate-100 text-slate-500 hover:text-slate-800 transition"
            aria-label="Đóng"
            title="Đóng và quay lại"
          >
            <X className="w-5 h-5" />
          </button>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-base sm:text-lg font-bold text-slate-900 leading-tight">
                {editing ? 'Chỉnh Sửa Gói Dịch Vụ' : '+ Đăng Gói Dịch Vụ Mới'}
              </h2>
              <span className="hidden sm:inline-flex px-2 py-0.5 rounded-full text-[11px] font-semibold bg-rose-50 text-rose-700 border border-rose-200">
                {formCollapsed ? 'Xem trước toàn màn hình' : 'Màn hình song song'}
              </span>
            </div>
            <p className="text-xs text-slate-500 hidden sm:block mt-0.5">
              Sau khi tạo, bạn có thể nộp duyệt ngay cho Admin hoặc lưu Nháp
            </p>
          </div>
        </div>

        <div className="flex w-full sm:w-auto items-center justify-end gap-2.5">
          {/* Desktop Form Collapse/Expand button */}
          <button
            type="button"
            onClick={() => setFormCollapsed(c => !c)}
            className="hidden lg:inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl border border-slate-200 bg-slate-50 hover:bg-slate-100 text-xs font-semibold text-slate-700 transition"
            title={formCollapsed ? "Mở lại form điền thông tin" : "Thu gọn form điền để mở rộng xem trước"}
          >
            {formCollapsed ? (
              <>
                <PanelLeftOpen className="w-4 h-4 text-rose-500" />
                <span>Mở form điền</span>
              </>
            ) : (
              <>
                <PanelLeftClose className="w-4 h-4 text-slate-500" />
                <span>Thu gọn form</span>
              </>
            )}
          </button>

          {/* Mobile Tab Switcher */}
          <div className="lg:hidden flex bg-slate-100 p-0.5 rounded-xl text-xs border border-slate-200">
            <button
              type="button"
              onClick={() => setMobileTab('form')}
              className={`px-3 py-1.5 rounded-lg font-semibold transition ${mobileTab === 'form' ? 'bg-white text-slate-900 shadow-2xs' : 'text-slate-600'}`}
            >
              Điền thông tin
            </button>
            <button
              type="button"
              onClick={() => setMobileTab('preview')}
              className={`px-3 py-1.5 rounded-lg font-semibold transition ${mobileTab === 'preview' ? 'bg-white text-slate-900 shadow-2xs' : 'text-slate-600'}`}
            >
              Xem trước
            </button>
          </div>

          <button
            type="button"
            onClick={closeEditor}
            className="hidden sm:inline-flex px-3.5 py-1.5 text-xs sm:text-sm font-medium text-slate-600 rounded-xl hover:bg-slate-100 transition"
          >
            Thoát
          </button>

          <button
            type="submit"
            form="vendor-listing-form"
            disabled={isLoading}
            className="inline-flex items-center gap-2 px-4 py-1.5 rounded-xl bg-rose-500 hover:bg-rose-600 text-white text-xs sm:text-sm font-semibold shadow-sm transition disabled:opacity-60"
          >
            {isLoading ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <PlusCircle className="w-4 h-4" />
            )}
            <span>{published ? 'Lưu bản sửa' : editing ? 'Lưu Thay Đổi' : 'Tạo Bài Đăng'}</span>
          </button>
        </div>
      </div>

      {/* Full-screen 2-column layout body */}
      <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-12 overflow-hidden">
        {/* Cột 1: Điền thông tin (Form) - kéo lên kéo xuống mượt mà */}
        <div className={`${formCollapsed ? 'hidden' : 'lg:col-span-5 xl:col-span-5'} h-full overflow-y-auto bg-white p-5 sm:p-7 border-r border-slate-200 ${mobileTab === 'preview' ? 'hidden lg:block' : 'block'}`}>
          <form id="vendor-listing-form" onSubmit={handleSubmit} className="space-y-5 max-w-2xl mx-auto">
                {published && <p role="note" className="rounded-xl border border-[#d4c894] bg-[#fcf6ea] p-3 text-xs leading-relaxed text-[#233a42]">Bạn đang chỉnh một bản sửa riêng. Lưu nháp hoặc gửi duyệt không đổi bản đang công khai; chỉ bản được kiểm duyệt chấp thuận mới thay thế nội dung cũ. Giao dịch đã chốt giữ nguyên.</p>}
                {published && initial?.revision?.status === 'Pending' && <p role="note" className="rounded-xl bg-amber-50 p-3 text-xs text-amber-900">Phiên bản này đang chờ duyệt. Nếu lưu bản sửa mới, yêu cầu cũ sẽ được ghi nhận đã thay thế; bạn cần gửi duyệt bản mới khi sẵn sàng.</p>}
                {initial?.revision?.reviewNote && <p role="note" className="rounded-xl bg-red-50 p-3 text-xs text-red-700">Lý do cần sửa phiên bản #{initial.revision.sequence}: {initial.revision.reviewNote}</p>}
                {!!initial?.revisionHistory?.length && <details className="rounded-xl border border-[#dedccf] p-3"><summary className="cursor-pointer text-sm font-semibold">Lịch sử phiên bản ({initial.revisionHistory.length})</summary><ul className="mt-3 space-y-3 text-xs">{initial.revisionHistory.map(revision => <li key={revision.id} className="border-t border-slate-100 pt-2"><p>#{revision.sequence} · {revision.status === 'Baseline' ? 'Ghi nhận bản công khai cũ, không suy diễn lịch sử phê duyệt' : revision.status} · {listingUpdatedTime(revision.reviewedAt, revision.createdAt)}</p>{revision.reviewNote && <p className="mt-1 whitespace-pre-wrap text-red-700">{revision.reviewNote}</p>}<a className="mt-1 inline-block underline" href={`/listings/${editing?.id}?preview=vendor&revision=${revision.id}`} target="_blank" rel="noreferrer">Xem bản chỉ đọc trong tab mới</a></li>)}</ul></details>}
                {/* API Error */}
                {apiError && (
                  <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-xs text-red-700 flex items-start gap-2">
                    <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
                    <span>{apiError}</span>
                  </div>
                )}

                {/* 1. THÔNG TIN CƠ BẢN (HIỂN THỊ THEO Ô) */}
                <details open className="rounded-2xl border border-[#dedccf] bg-[#fcf6ea] p-4 sm:p-5">
                  <summary className="cursor-pointer text-sm font-semibold text-[#233a42] flex items-center justify-between">
                    <span>1. Thông tin cơ bản</span>
                    <span className="text-xs font-normal text-slate-500">Bắt buộc</span>
                  </summary>

                  <div className="mt-4 grid grid-cols-1 md:grid-cols-2 gap-3.5">
                    {/* Ô 1: Danh mục dịch vụ */}
                    {!editing ? (
                      <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all flex flex-col justify-between">
                        <div>
                          <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 mb-1.5">
                            <Layers className="w-3.5 h-3.5 text-rose-500" />
                            <span>Danh mục dịch vụ</span> <span className="text-red-500">*</span>
                          </label>
                          <select
                            value={form.categoryId}
                            onChange={set('categoryId')}
                            aria-label="Loại dịch vụ"
                            disabled={categoryQuery.isPending || categoryQuery.isError || !selectableCategories.length}
                            className={`w-full px-3 py-2.5 border rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition ${errors.categoryId ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
                          >
                            <option value="">{categoryQuery.isPending ? 'Đang tải danh mục của shop...' : '— Chọn danh mục dịch vụ —'}</option>
                            {vendorCategoryGroups(selectableCategories).map(group => <optgroup key={group.id} label={group.label}>{group.categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</optgroup>)}
                          </select>
                          {errors.categoryId && <FieldError msg={errors.categoryId} />}
                          {categoryQuery.isError && <p role="alert" className="mt-2 text-xs text-red-700">Chưa tải được danh mục. <button type="button" className="underline" onClick={() => void categoryQuery.refetch()}>Thử lại</button></p>}
                        </div>
                        <p className="mt-2 text-[11px] leading-relaxed text-slate-500">Tự chọn loại dịch vụ phù hợp. Nhóm hoa hồng và mức mặc định tương ứng sẽ hiển thị ngay bên dưới.</p>
                        {!categoryQuery.isPending && !categoryQuery.isError && !selectableCategories.length && <p role="note" className="mt-2 text-xs text-slate-600">{categoryQuery.data?.requiresMembershipReview ? 'API hiện tại vẫn dùng luồng giới hạn nhóm cũ. Cần cập nhật API để sử dụng danh mục mở; không cần Admin bổ sung nhóm cho shop.' : 'Chưa có danh mục đang hoạt động được cấu hình. Vui lòng thử lại hoặc liên hệ quản trị viên.'}</p>}
                      </div>
                    ) : (
                      <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs text-sm flex flex-col justify-between">
                        <div>
                          <div className="flex items-center gap-1.5 text-xs text-slate-500 font-medium mb-1">
                            <Layers className="w-3.5 h-3.5 text-rose-500" />
                            <span>Danh mục dịch vụ đã lưu</span>
                          </div>
                          <p className="font-semibold text-slate-900">{selectedCategory?.name || editing.categoryName}</p>
                        </div>
                        <p className="mt-2 text-xs leading-relaxed text-slate-500">Danh mục được giữ cố định khi sửa bài để không làm lệch phân loại và hợp đồng. Muốn cung cấp dịch vụ thuộc danh mục khác, hãy tạo bài riêng.</p>
                      </div>
                    )}

                    {/* Ô 2: Tiêu Đề Gói Dịch Vụ */}
                    <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all flex flex-col justify-between">
                      <div>
                        <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 mb-1.5">
                          <Type className="w-3.5 h-3.5 text-rose-500" />
                          <span>Tiêu Đề Gói Dịch Vụ</span> <span className="text-red-500">*</span>
                        </label>
                        <input
                          type="text"
                          value={form.title}
                          onChange={set('title')}
                          placeholder="Vd: Sảnh Tiệc Pha Lê Hoàng Gia 350 Khách"
                          maxLength={200}
                          className={`w-full px-3 py-2.5 border rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition ${errors.title ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
                        />
                        {errors.title && <FieldError msg={errors.title} />}
                      </div>
                      <p className="mt-2 text-[11px] text-slate-400 leading-tight">Đặt tên ngắn gọn, rõ ràng, nêu bật điểm độc đáo hoặc quy mô gói.</p>
                    </div>

                    {/* Ô 3: Khoảng giá dịch vụ */}
                    <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all">
                      <div className="flex items-center justify-between mb-2">
                        <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700">
                          <Coins className="w-3.5 h-3.5 text-rose-500" />
                          <span>Khoảng Giá (VNĐ)</span> <span className="text-red-500">*</span>
                        </label>
                        {form.minPrice && form.maxPrice && Number(form.maxPrice) >= Number(form.minPrice) && (
                          <span className="text-[11px] font-semibold text-rose-600 bg-rose-50 px-2 py-0.5 rounded-full border border-rose-100">
                            {Number(form.minPrice).toLocaleString('vi-VN')} – {Number(form.maxPrice).toLocaleString('vi-VN')} ₫
                          </span>
                        )}
                      </div>
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <span className="block text-[11px] font-medium text-slate-500 mb-1">Giá Tối Thiểu</span>
                          <MoneyInput
                            aria-label="Giá tối thiểu (VNĐ)"
                            value={form.minPrice === '' ? null : Number(form.minPrice)}
                            onValueChange={value => setForm(previous => ({ ...previous, minPrice: value == null ? '' : String(value) }))}
                            placeholder="12500000"
                            min={0}
                            className={`w-full px-3 py-2.5 border rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition ${errors.minPrice ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
                          />
                          {errors.minPrice && <FieldError msg={errors.minPrice} />}
                        </div>
                        <div>
                          <span className="block text-[11px] font-medium text-slate-500 mb-1">Giá Tối Đa</span>
                          <MoneyInput
                            aria-label="Giá tối đa (VNĐ)"
                            value={form.maxPrice === '' ? null : Number(form.maxPrice)}
                            onValueChange={value => setForm(previous => ({ ...previous, maxPrice: value == null ? '' : String(value) }))}
                            placeholder="25000000"
                            min={0}
                            className={`w-full px-3 py-2.5 border rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition ${errors.maxPrice ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
                          />
                          {errors.maxPrice && <FieldError msg={errors.maxPrice} />}
                        </div>
                      </div>
                    </div>

                    {/* Ô 4: Địa Điểm / Khu Vực Hoạt Động */}
                    <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all flex flex-col justify-between">
                      <div>
                        <div className="flex items-center justify-between mb-1.5">
                          <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700">
                            <MapPin className="w-3.5 h-3.5 text-rose-500" />
                            <span>Địa Điểm / Khu Vực Hoạt Động</span> <span className="text-red-500">*</span>
                          </label>
                        </div>
                        <input
                          type="text"
                          value={form.location}
                          onChange={set('location')}
                          maxLength={200}
                          placeholder="Vd: Quận 1, TP. Hồ Chí Minh"
                          className={`w-full px-3 py-2.5 border rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition ${errors.location ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
                        />
                        {errors.location && <FieldError msg={errors.location} />}
                      </div>
                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        <span className="text-[10px] text-slate-400">Gợi ý nhanh:</span>
                        {['Toàn quốc', 'TP. Hồ Chí Minh', 'Hà Nội', 'Đà Nẵng'].map(area => (
                          <button
                            key={area}
                            type="button"
                            onClick={() => setForm(prev => ({ ...prev, location: area }))}
                            className="px-2 py-0.5 rounded-md text-[11px] bg-slate-100 hover:bg-rose-50 hover:text-rose-600 text-slate-600 transition"
                          >
                            {area}
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Policy summary (full width if selected) */}
                    {selectedCategory && (
                      <div className="md:col-span-2">
                        <ServiceCategoryPolicySummary category={selectedCategory} policy={policyQuery.data} pending={policyQuery.isPending} failed={policyQuery.isError} />
                      </div>
                    )}

                    {/* Ô 5: Mô Tả Chi Tiết (full width across both columns) */}
                    <div className="md:col-span-2 bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all">
                      <div className="flex items-center justify-between mb-1.5">
                        <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700">
                          <FileText className="w-3.5 h-3.5 text-rose-500" />
                          <span>Mô Tả Chi Tiết</span> <span className="text-red-500">*</span>
                        </label>
                        <span className={`text-[11px] ${form.description.length > 3800 ? 'text-amber-600 font-semibold' : 'text-slate-400'}`}>
                          {form.description.length}/4000 ký tự
                        </span>
                      </div>
                      <textarea
                        value={form.description}
                        onChange={set('description')}
                        placeholder="Mô tả chi tiết về gói dịch vụ, điểm nổi bật, tiện ích đi kèm..."
                        rows={4}
                        maxLength={4000}
                        className={`w-full px-3 py-2.5 border rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition resize-none ${errors.description ? 'border-red-400 bg-red-50' : 'border-slate-200'}`}
                      />
                      {errors.description && <FieldError msg={errors.description} />}
                    </div>
                  </div>
                </details>

          {uploadFeedback}
          {errors["serviceDetails.collections"] && <p role="alert" className="text-xs text-red-700">{errors["serviceDetails.collections"]}</p>}
          {errors["serviceDetails.latestUpdate"] && <p role="alert" className="text-xs text-red-700">{errors["serviceDetails.latestUpdate"]}</p>}
          <ServiceDetailsEditor value={serviceDetails} onChange={setServiceDetails} fullServiceEnabled={fullServiceEnabled} disabled={isLoading} errors={errors} />
          <ServiceItemManager value={pricing} onChange={value => { setPricing(value); setPricingChanged(!initial?.pricingSelectionRequired || !!value.serviceItemId); }} options={initial?.pricingOptions} selectionRequired={initial?.pricingSelectionRequired} disabled={isLoading} error={errors.pricing} open={!editing} />

          <details className="rounded-2xl border border-[#dedccf] bg-[#fcf6ea] p-4">
            <summary className="cursor-pointer text-sm font-semibold text-[#233a42]">Chính sách hủy hiện hành · chỉ đọc</summary>
            <p className="mt-3 whitespace-pre-wrap break-words text-xs leading-relaxed text-slate-600">{initial?.cancellationPolicy || 'Dịch vụ mới giữ cơ chế chính sách hủy hiện có của hệ thống. Nội dung bổ sung không tự thay thế chính sách đó.'}</p>
            <p className="mt-2 text-xs leading-relaxed text-slate-500">Biểu mẫu này không sửa chính sách hủy hoặc snapshot đã chốt trên giao dịch. Thay đổi có ảnh hưởng tài chính cần quy trình phê duyệt riêng.</p>
          </details>

          <details open={!!errors.images || images.length === 0} className="rounded-2xl border border-[#dedccf] bg-white p-4">
          <summary className="cursor-pointer text-sm font-semibold text-[#233a42]">3. Hình ảnh dịch vụ ({images.length}/10)</summary>
          <div className="mt-4">
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
                      src={item.preview || undefined}
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
                    {idx > 0 && <button type="button" disabled={isLoading} onClick={() => setImages(current => moveListingCover(current, idx))} className="absolute bottom-0 inset-x-0 py-1.5 bg-white/90 text-slate-800 text-[10px] font-semibold hover:bg-white" aria-label={`Đặt ảnh ${idx + 1} làm ảnh bìa`}>Đặt làm ảnh bìa</button>}
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

          </div>
          </details>

          {/* ─── 4. BỘ SƯU TẬP HÌNH ẢNH (TÙY CHỌN) ─────────────────────────── */}
          <details className="rounded-2xl border border-[#dedccf] bg-white p-4">
            <summary className="cursor-pointer text-sm font-semibold text-[#233a42] flex items-center justify-between">
              <span className="flex items-center gap-2">
                <LayoutGrid className="w-4 h-4 text-[#5e7c3b]" />
                <span>4. Bộ sưu tập hình ảnh ({collections.length})</span>
              </span>
              <span className="text-xs font-normal text-slate-500">Tùy chọn</span>
            </summary>
            <div className="mt-3 space-y-3">
              <p className="text-xs text-slate-600 leading-relaxed">
                Tạo các album ảnh thực tế theo chủ đề để khách hàng dễ dàng tham khảo trên trang chi tiết.
              </p>

              {collections.length === 0 ? (
                <div className="p-4 rounded-xl border border-dashed border-slate-200 text-center bg-slate-50/50 space-y-2">
                  <p className="text-xs text-slate-500">Chưa có bộ sưu tập nào được tạo.</p>
                  <button
                    type="button"
                    onClick={handleOpenCreateCollection}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[#5e7c3b] text-white text-xs font-semibold hover:bg-[#4d692e] transition"
                  >
                    <Plus className="w-3.5 h-3.5" />
                    <span>Tạo mới 1 bộ sưu tập</span>
                  </button>
                </div>
              ) : (
                <div className="space-y-2">
                  {collections.map(col => (
                    <div key={col.id} className="p-3 rounded-xl border border-slate-200 bg-slate-50 flex items-center justify-between">
                      <div>
                        <h4 className="text-xs font-bold text-slate-800">{col.name}</h4>
                        <p className="text-[11px] text-slate-500">{col.images.length} ảnh trong album</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => handleDeleteCollection(col.id)}
                        className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                        title="Xóa bộ sưu tập này"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  ))}
                  <button
                    type="button"
                    onClick={handleOpenCreateCollection}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-dashed border-slate-300 text-slate-700 text-xs font-semibold hover:border-[#5e7c3b] hover:text-[#5e7c3b] transition"
                  >
                    <Plus className="w-3.5 h-3.5" />
                    <span>Thêm bộ sưu tập khác</span>
                  </button>
                </div>
              )}
            </div>
          </details>

          {/* ─── 5. CẬP NHẬT MỚI NHẤT CỦA DỊCH VỤ (TÙY CHỌN) ────────────────── */}
          <details className="rounded-2xl border border-[#dedccf] bg-white p-4">
            <summary className="cursor-pointer text-sm font-semibold text-[#233a42] flex items-center justify-between">
              <span className="flex items-center gap-2">
                <Sparkles className="w-4 h-4 text-amber-500" />
                <span>5. Cập nhật mới nhất của dịch vụ</span>
              </span>
              <span className="text-xs font-normal text-slate-500">
                {latestUpdate.content || latestUpdate.imageUrl ? 'Đã có bài' : 'Tùy chọn'}
              </span>
            </summary>
            <div className="mt-3 space-y-3">
              <p className="text-xs text-slate-600 leading-relaxed">
                Bạn có thể <b>chỉ viết bài</b>, <b>chỉ thêm ảnh</b>, hoặc <b>kết hợp cả hai</b> để đăng tin tức hoặc ưu đãi mới nhất.
              </p>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Nội dung cập nhật (viết bài / thông báo / ưu đãi)
                </label>
                <textarea
                  rows={3}
                  className="w-full px-3 py-2 border border-slate-200 rounded-xl text-xs bg-slate-50/50 focus:bg-white focus:outline-none transition leading-relaxed"
                  placeholder="Ví dụ: Vừa hoàn thành không gian trang trí hoa tươi mới nhất..."
                  aria-label="Nội dung cập nhật dịch vụ"
                  maxLength={4000}
                  value={latestUpdate.content || ''}
                  onChange={e => setLatestUpdate(prev => ({ ...prev, content: e.target.value }))}
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                  Hình ảnh cập nhật / Poster (tùy chọn)
                </label>
                <div className="flex flex-wrap items-center gap-2">
                  <label className="cursor-pointer inline-flex items-center gap-2 px-3.5 py-2 rounded-xl bg-amber-50 text-amber-900 border border-amber-200 hover:bg-amber-100 font-semibold text-xs transition">
                    <UploadCloud className="w-4 h-4 text-amber-600" />
                    <span>{isUploadingAux ? 'Đang tải ảnh...' : 'Tải ảnh từ máy / điện thoại'}</span>
                    <input
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      disabled={isLoading}
                      onChange={handleUpdateFileUploadForm}
                      className="hidden"
                    />
                  </label>
                  {latestUpdate.imageUrl && (
                    <button
                      type="button"
                      onClick={() => setLatestUpdate(prev => ({ ...prev, imageUrl: '' }))}
                      className="text-xs text-red-600 hover:underline flex items-center gap-1"
                    >
                      <Trash2 className="w-3.5 h-3.5" /> Xóa ảnh cập nhật
                    </button>
                  )}
                </div>

                {latestUpdate.imageUrl && (
                  <div className="mt-2.5 relative w-36 h-24 rounded-xl overflow-hidden border border-slate-200 shadow-2xs">
                    <img src={serviceStudioImageSrc(latestUpdate.imageUrl, import.meta.env.VITE_API_URL || '')} alt="" className="w-full h-full object-cover" />
                  </div>
                )}

                {images.length > 0 && (
                  <div className="mt-2.5">
                    <span className="text-[11px] text-slate-500 block mb-1">Hoặc chọn nhanh từ ảnh dịch vụ đã tải lên:</span>
                    <div className="flex gap-2 overflow-x-auto pb-1 no-scrollbar">
                      {images.map((img, i) => {
                        const url = img.url;
                        if (!isServiceStudioImage(url)) return null;
                        const isSelected = latestUpdate.imageUrl === url;
                        return (
                          <button
                            key={i}
                            type="button"
                            onClick={() => setLatestUpdate(prev => ({ ...prev, imageUrl: isSelected ? '' : url }))}
                            className={`relative shrink-0 w-12 h-12 rounded-lg overflow-hidden border-2 transition ${
                              isSelected ? 'border-[#5e7c3b] ring-2 ring-[#5e7c3b]/30' : 'border-slate-200 opacity-70 hover:opacity-100'
                            }`}
                          >
                            <img src={serviceStudioImageSrc(url, import.meta.env.VITE_API_URL || '')} alt="" className="w-full h-full object-cover" />
                            {isSelected && (
                              <div className="absolute inset-0 bg-[#5e7c3b]/30 flex items-center justify-center text-white">
                                <Check className="w-4 h-4" />
                              </div>
                            )}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>

              {(latestUpdate.content || latestUpdate.imageUrl) && (
                <div className="pt-2 border-t border-slate-100 flex items-center justify-between">
                  <span className="text-[11px] text-emerald-700 font-medium flex items-center gap-1">
                    <CheckCircle2 className="w-3.5 h-3.5" /> Đã đồng bộ trực tiếp lên trang xem trước
                  </span>
                  <button
                    type="button"
                    onClick={() => setLatestUpdate({ content: '', imageUrl: '' })}
                    className="text-[11px] text-red-600 hover:underline"
                  >
                    Xóa cập nhật
                  </button>
                </div>
              )}
            </div>
          </details>

          {/* Inline submit button on form (visible on mobile form view) */}
          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 lg:hidden">
            <button
              type="button"
              onClick={closeEditor}
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
              {published ? 'Lưu bản sửa nháp' : editing ? 'Lưu Thay Đổi' : 'Tạo Bài Đăng'}
            </button>
          </div>
        </form>
      </div>

      {/* Cột 2: Màn hình xem trước (Live Preview) - kéo lên kéo xuống mượt mà */}
      <div className={`${formCollapsed ? 'lg:col-span-12' : 'lg:col-span-7 xl:col-span-7'} h-full overflow-y-auto bg-slate-100/70 p-4 sm:p-7 ${mobileTab === 'form' ? 'hidden lg:block' : 'block'}`}>
        {formCollapsed && (
          <div className="mb-4 max-w-4xl mx-auto flex items-center justify-between p-3.5 rounded-xl bg-white border border-rose-200 shadow-xs">
            <span className="text-xs text-slate-700 flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-rose-500" />
              <span>Đang xem trước toàn màn hình. Bạn có thể mở lại biểu mẫu bất kỳ lúc nào để tiếp tục chỉnh sửa.</span>
            </span>
            <button
              type="button"
              onClick={() => setFormCollapsed(false)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-rose-500 text-white text-xs font-semibold hover:bg-rose-600 shadow-xs transition"
            >
              <PanelLeftOpen className="w-4 h-4" />
              <span>Mở lại form điền</span>
            </button>
          </div>
        )}
        <div className="max-w-4xl mx-auto">
          <ListingLivePreview
            title={form.title}
            categoryName={selectedCategory?.name || (editing ? editing.categoryName : undefined)}
            minPrice={form.minPrice}
            maxPrice={form.maxPrice}
            location={form.location}
            description={form.description}
            images={images}
            vendorBrandName={editing?.vendorBrandName || currentUser?.vendorBrandName || currentUser?.fullName}
            serviceDetails={currentDetails()}
            cancellationPolicy={initial?.cancellationPolicy}
            pricing={pricing}
            pricingOptions={initial?.pricingOptions}
            collections={collections}
            latestUpdate={latestUpdate}
            onOpenCreateCollection={handleOpenCreateCollection}
            onDeleteCollection={handleDeleteCollection}
            onOpenUpdateEditor={handleOpenUpdateEditor}
          />
        </div>
      </div>
    </div>

    {/* Modal Tạo mới bộ sưu tập */}
    {isCollectionModalOpen && (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4" role="dialog" aria-modal="true" aria-label="Tạo mới bộ sưu tập">
        <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg p-5 space-y-4 max-h-[90vh] overflow-y-auto">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h3 className="font-bold text-slate-900 text-sm flex items-center gap-2">
              <FolderPlus className="w-4 h-4 text-[#5e7c3b]" />
              <span>Tạo mới bộ sưu tập ảnh</span>
            </h3>
            <button
              type="button"
              disabled={isLoading} onClick={() => setIsCollectionModalOpen(false)}
              className="p-1 rounded-lg hover:bg-slate-100 text-slate-400 hover:text-slate-700"
            >
              <X className="w-4 h-4" />
            </button>
          </div>

          {uploadFeedback}
          <div className="space-y-3 text-xs">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">
                Tên bộ sưu tập <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                className="w-full px-3 py-2 border border-slate-200 rounded-xl text-xs bg-slate-50/50 focus:bg-white focus:outline-none"
                placeholder="Ví dụ: Ảnh cưới thực tế 2026, Trang trí gia tiên..."
                aria-label="Tên bộ sưu tập"
                maxLength={100}
                value={collectionNameInput}
                onChange={e => setCollectionNameInput(e.target.value)}
                autoFocus
              />
            </div>

            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">
                Thêm ảnh vào bộ sưu tập:
              </label>

              {/* 1. Nút tải ảnh trực tiếp từ máy tính/điện thoại */}
              <div className="mb-3">
                <label className="cursor-pointer inline-flex items-center gap-2 px-4 py-2.5 rounded-xl bg-emerald-50 text-[#5e7c3b] border-2 border-dashed border-emerald-300 hover:bg-emerald-100 font-bold text-xs transition">
                  <UploadCloud className="w-4 h-4" />
                  <span>{isUploadingAux ? 'Đang tải ảnh...' : 'Tải ảnh từ máy tính / điện thoại'}</span>
                  <input
                    type="file"
                    multiple
                    accept="image/jpeg,image/png,image/webp"
                    disabled={isLoading}
                    onChange={handleCollectionFileUpload}
                    className="hidden"
                  />
                </label>
                <p className="text-[11px] text-slate-500 mt-1">
                  Chọn một hoặc nhiều ảnh từ máy của bạn để thêm vào album ngay lập tức.
                </p>
              </div>

              {/* 2. Danh sách ảnh đã chọn / tải lên cho bộ sưu tập này */}
              {selectedCollectionImages.length > 0 && (
                <div className="mb-3">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-[11px] font-bold text-slate-700">
                      Ảnh trong bộ sưu tập ({selectedCollectionImages.length} ảnh):
                    </span>
                    <button
                      type="button"
                      onClick={() => setSelectedCollectionImages([])}
                      className="text-[10px] text-red-600 hover:underline"
                    >
                      Bỏ chọn tất cả
                    </button>
                  </div>
                  <div className="grid grid-cols-4 sm:grid-cols-5 gap-2 max-h-36 overflow-y-auto p-2 border border-emerald-200 rounded-xl bg-emerald-50/30">
                    {selectedCollectionImages.map((url, i) => (
                      <div key={i} className="relative aspect-square rounded-lg overflow-hidden border border-slate-200 group">
                        <img src={serviceStudioImageSrc(url, import.meta.env.VITE_API_URL || '')} alt="" className="w-full h-full object-cover" />
                        <button
                          type="button"
                          onClick={() => setSelectedCollectionImages(prev => prev.filter((_, idx) => idx !== i))}
                          className="absolute top-1 right-1 w-5 h-5 rounded-full bg-red-600 text-white flex items-center justify-center opacity-80 hover:opacity-100 transition shadow-xs"
                          title="Bỏ ảnh này"
                        >
                          <X className="w-3 h-3" />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* 3. Hoặc chọn thêm từ ảnh dịch vụ đã tải lên ở Mục 3 */}
              {images.length > 0 && (
                <div>
                  <span className="text-[11px] text-slate-600 font-medium block mb-1">
                    Hoặc chọn thêm từ ảnh dịch vụ đã tải lên:
                  </span>
                  <div className="grid grid-cols-4 sm:grid-cols-5 gap-2 max-h-36 overflow-y-auto p-2 border border-slate-200 rounded-xl bg-slate-50/50">
                    {images.map((img, i) => {
                      const url = img.url;
                      if (!isServiceStudioImage(url)) return null;
                      const isSelected = selectedCollectionImages.includes(url);
                      return (
                        <button
                          key={i}
                          type="button"
                          onClick={() => handleToggleCollectionImage(url)}
                          className={`relative aspect-square rounded-lg overflow-hidden border-2 transition ${
                            isSelected ? 'border-[#5e7c3b] ring-2 ring-[#5e7c3b]/30 scale-95' : 'border-slate-200 opacity-70 hover:opacity-100'
                          }`}
                        >
                          <img src={serviceStudioImageSrc(url, import.meta.env.VITE_API_URL || '')} alt="" className="w-full h-full object-cover" />
                          {isSelected && (
                            <div className="absolute inset-0 bg-[#5e7c3b]/40 flex items-center justify-center text-white font-bold">
                              <Check className="w-4 h-4" />
                            </div>
                          )}
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              disabled={isLoading} onClick={() => setIsCollectionModalOpen(false)}
              className="px-3.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-lg transition"
            >
              Hủy
            </button>
            <button
              type="button"
              disabled={!collectionNameInput.trim() || !selectedCollectionImages.length || isLoading || collections.length >= 10}
              onClick={handleCreateCollection}
              className="px-4 py-2 rounded-xl bg-[#5e7c3b] hover:bg-[#4d692e] text-white text-xs font-bold transition disabled:opacity-50"
            >
              Tạo bộ sưu tập
            </button>
          </div>
        </div>
      </div>
    )}

    {/* Modal Cập nhật mới nhất */}
    {isUpdateModalOpen && (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4" role="dialog" aria-modal="true" aria-label="Cập nhật mới nhất">
        <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg p-5 space-y-4 max-h-[90vh] overflow-y-auto">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h3 className="font-bold text-slate-900 text-sm flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-amber-500" />
              <span>Cập nhật mới nhất của dịch vụ</span>
            </h3>
            <button
              type="button"
              disabled={isLoading} onClick={() => setIsUpdateModalOpen(false)}
              className="p-1 rounded-lg hover:bg-slate-100 text-slate-400 hover:text-slate-700"
            >
              <X className="w-4 h-4" />
            </button>
          </div>

          {uploadFeedback}
          <div className="space-y-3 text-xs">
            <p className="text-slate-600 leading-relaxed">
              Bạn có thể <b>chỉ viết bài</b>, <b>chỉ thêm ảnh</b>, hoặc <b>kết hợp cả hai</b> đều được.
            </p>

            <div>
              <label className="block font-semibold text-slate-700 mb-1">
                Nội dung bài viết / Thông báo (tùy chọn)
              </label>
              <textarea
                rows={4}
                className="w-full px-3 py-2 border border-slate-200 rounded-xl text-xs bg-slate-50/50 focus:bg-white focus:outline-none transition leading-relaxed"
                placeholder="Viết bài cập nhật, thông báo ưu đãi hoặc câu chuyện về dịch vụ..."
                aria-label="Nội dung cập nhật mới nhất"
                maxLength={4000}
                value={updateContentInput}
                onChange={e => setUpdateContentInput(e.target.value)}
              />
            </div>

            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">
                Hình ảnh cập nhật / Poster (tùy chọn)
              </label>

              {/* Tải ảnh từ thiết bị */}
              <div className="flex flex-wrap items-center gap-2 mb-2">
                <label className="cursor-pointer inline-flex items-center gap-2 px-3.5 py-2 rounded-xl bg-amber-50 text-amber-900 border border-amber-200 hover:bg-amber-100 font-semibold text-xs transition">
                  <UploadCloud className="w-4 h-4 text-amber-600" />
                  <span>{isUploadingAux ? 'Đang tải ảnh...' : 'Tải ảnh từ máy / điện thoại'}</span>
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    disabled={isLoading}
                    onChange={handleUpdateFileUploadModal}
                    className="hidden"
                  />
                </label>
                {updateImageUrlInput && (
                  <button
                    type="button"
                    onClick={() => setUpdateImageUrlInput('')}
                    className="text-xs text-red-600 hover:underline flex items-center gap-1"
                  >
                    <Trash2 className="w-3.5 h-3.5" /> Xóa ảnh đã chọn
                  </button>
                )}
              </div>

              {updateImageUrlInput && (
                <div className="relative w-36 h-24 rounded-xl overflow-hidden border border-slate-200 shadow-2xs mb-2">
                  <img src={serviceStudioImageSrc(updateImageUrlInput, import.meta.env.VITE_API_URL || '')} alt="" className="w-full h-full object-cover" />
                </div>
              )}

              {images.length > 0 && (
                <div className="mt-2">
                  <span className="text-[11px] text-slate-500 block mb-1">Hoặc chọn nhanh từ ảnh dịch vụ đã tải lên:</span>
                  <div className="flex gap-2 overflow-x-auto pb-1 no-scrollbar">
                    {images.map((img, i) => {
                      const url = img.url;
                      if (!isServiceStudioImage(url)) return null;
                      const isSelected = updateImageUrlInput === url;
                      return (
                        <button
                          key={i}
                          type="button"
                          onClick={() => setUpdateImageUrlInput(isSelected ? '' : url)}
                          className={`relative shrink-0 w-12 h-12 rounded-lg overflow-hidden border-2 transition ${
                            isSelected ? 'border-[#5e7c3b] ring-2 ring-[#5e7c3b]/30' : 'border-slate-200 opacity-70 hover:opacity-100'
                          }`}
                        >
                          <img src={serviceStudioImageSrc(url, import.meta.env.VITE_API_URL || '')} alt="" className="w-full h-full object-cover" />
                          {isSelected && (
                            <div className="absolute inset-0 bg-[#5e7c3b]/30 flex items-center justify-center text-white">
                              <Check className="w-4 h-4" />
                            </div>
                          )}
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              disabled={isLoading} onClick={() => setIsUpdateModalOpen(false)}
              className="px-3.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-lg transition"
            >
              Hủy
            </button>
            <button
              type="button"
              disabled={isLoading}
              onClick={handleSaveUpdate}
              className="px-4 py-2 rounded-xl bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold transition"
            >
              Lưu cập nhật
            </button>
          </div>
        </div>
      </div>
    )}

    {/* Footer actions bar */}
    <div className="flex items-center justify-between px-6 py-3 bg-white border-t border-slate-200 shrink-0">
      <div className="text-xs text-slate-500 hidden sm:block">
        {editing ? 'Chỉnh sửa dịch vụ đã có' : 'Dịch vụ sẽ được lưu nháp hoặc gửi kiểm duyệt đến Admin'}
      </div>
      <div className="flex items-center gap-3 ml-auto">
        <button
          type="button"
          onClick={closeEditor}
          className="px-4 py-2 text-sm font-medium text-slate-600 rounded-xl hover:bg-slate-100 transition"
        >
          Hủy
        </button>
        <button
          type="submit"
          form="vendor-listing-form"
          disabled={isLoading}
          className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-rose-500 hover:bg-rose-600 text-white text-sm font-semibold shadow-sm transition disabled:opacity-60"
        >
          {isLoading ? (
            <Loader2 className="w-4 h-4 animate-spin" />
          ) : (
            <PlusCircle className="w-4 h-4" />
          )}
          {published ? 'Lưu bản sửa nháp' : editing ? 'Lưu Thay Đổi' : 'Tạo Bài Đăng'}
        </button>
      </div>
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
