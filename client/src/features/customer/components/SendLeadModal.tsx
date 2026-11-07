import React, { useState } from 'react';
import {
  X,
  Calendar,
  Users,
  DollarSign,
  Phone,
  FileText,
  Gift,
  CheckCircle,
  Sparkles,
  Copy,
  Check,
  Shirt,
  Camera,
  Palette,
  Mail,
  Building2,
  Clock,
  Sparkle,
} from 'lucide-react';
import type { SendLeadRequest, SendLeadResponse } from '../../../types/lead.types';
import { MoneyInput } from '../../../components/MoneyInput';

interface SendLeadModalProps {
  isOpen: boolean;
  onClose: () => void;
  listingId?: string;
  vendorId?: string;
  vendorName: string;
  listingTitle?: string;
  categoryName?: string;
  subServices?: string[];
  initialNotes?: string;
  defaultPhone?: string;
  onSuccess?: (res: SendLeadResponse) => void;
}

export function SendLeadModal({
  isOpen,
  onClose,
  listingId,
  vendorId,
  vendorName,
  listingTitle,
  categoryName = '',
  subServices = [],
  initialNotes = '',
  defaultPhone = '',
  onSuccess,
}: SendLeadModalProps) {
  // Category detection
  const cat = categoryName.toLowerCase();
  const isDressOrSuit = cat.includes('váy') || cat.includes('vest') || cat.includes('trang phục');
  const isVenueOrBanquet = cat.includes('tiệc') || cat.includes('địa điểm') || cat.includes('sảnh') || cat.includes('nhà hàng');
  const isPhotoOrVideo = cat.includes('quay') || cat.includes('chụp') || cat.includes('ảnh') || cat.includes('studio') || cat.includes('phóng sự');
  const isMakeup = cat.includes('trang điểm') || cat.includes('makeup');
  const isDecor = cat.includes('trang trí') || cat.includes('decor');
  const isInvitation = cat.includes('thiệp') || cat.includes('quà');
  const isCombo = cat.includes('combo') || (subServices && subServices.length > 0);

  // Common fields
  const [phoneNumber, setPhoneNumber] = useState(defaultPhone);
  const [weddingDate, setWeddingDate] = useState('');
  const [estimatedGuests, setEstimatedGuests] = useState<number | undefined>(
    isVenueOrBanquet || isCombo ? 300 : undefined
  );
  const [estimatedBudget, setEstimatedBudget] = useState<number | undefined>(undefined);
  const [userNotes, setUserNotes] = useState(initialNotes);

  // Category specific state
  // 1. Váy & Vest
  const [selectedOutfits, setSelectedOutfits] = useState<string[]>([
    'Váy cưới chính lễ đường',
    'Vest chú rể',
  ]);
  const [fittingDate, setFittingDate] = useState('');
  const [bodyMeasurements, setBodyMeasurements] = useState('');
  const [outfitStyle, setOutfitStyle] = useState('Công chúa lộng lẫy bồng xòe');

  // 2. Tiệc cưới & Sảnh
  const [tableCount, setTableCount] = useState<number | undefined>(30);
  const [banquetTimeSlot, setBanquetTimeSlot] = useState('Tiệc tối (17:30 - 21:00)');
  const [banquetSpaceType, setBanquetSpaceType] = useState('Ballroom sảnh tiệc trong nhà');

  // 3. Quay phim & Chụp ảnh
  const [photoPackages, setPhotoPackages] = useState<string[]>([
    'Chụp Pre-wedding studio',
    'Phóng sự cưới trọn ngày',
  ]);
  const [shootLocation, setShootLocation] = useState('');
  const [photoColorGrade, setPhotoColorGrade] = useState('Hàn Quốc trong trẻo, tự nhiên');

  // 4. Trang điểm
  const [makeupClients, setMakeupClients] = useState<string[]>([
    'Cô dâu (Tiệc cưới chính)',
    '2 Mẹ (Mẹ dâu & Mẹ rể)',
  ]);
  const [makeupLocation, setMakeupLocation] = useState('Tại nhà riêng / Khách sạn (Chuyên viên đến tận nơi)');
  const [makeupFinishTime, setMakeupFinishTime] = useState('06:30');

  // 5. Trang trí
  const [decorAreas, setDecorAreas] = useState<string[]>([
    'Lễ gia tiên tại tư gia',
    'Cổng hoa & Backdrop đón khách',
  ]);
  const [decorColorConcept, setDecorColorConcept] = useState('Trắng & Pastel lãng mạn');

  // 6. Thiệp cưới
  const [invitationQuantity, setInvitationQuantity] = useState<number | undefined>(300);
  const [invitationType, setInvitationType] = useState('Cả thiệp giấy in cao cấp & Thiệp online QR');
  const [invitationDueDate, setInvitationDueDate] = useState('');

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successResult, setSuccessResult] = useState<SendLeadResponse | null>(null);
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const toggleArrayItem = (arr: string[], setArr: (val: string[]) => void, item: string) => {
    if (arr.includes(item)) {
      setArr(arr.filter((x) => x !== item));
    } else {
      setArr([...arr, item]);
    }
  };

  const buildStructuredNotes = () => {
    const lines: string[] = [];

    if (isDressOrSuit) {
      lines.push(`[Yêu cầu Trang phục & Váy cưới/Vest]`);
      if (selectedOutfits.length > 0) lines.push(`- Hạng mục: ${selectedOutfits.join(', ')}`);
      if (fittingDate) lines.push(`- Ngày hẹn thử đồ dự kiến: ${fittingDate}`);
      if (bodyMeasurements) lines.push(`- Chiều cao & cân nặng: ${bodyMeasurements}`);
      if (outfitStyle) lines.push(`- Phong cách gu thẩm mỹ: ${outfitStyle}`);
    } else if (isVenueOrBanquet) {
      lines.push(`[Yêu cầu Trung tâm tiệc cưới & Sảnh]`);
      if (estimatedGuests) lines.push(`- Số lượng khách dự kiến: ${estimatedGuests} khách`);
      if (tableCount) lines.push(`- Số bàn tiệc dự kiến: ${tableCount} bàn`);
      if (banquetTimeSlot) lines.push(`- Khung giờ tiệc: ${banquetTimeSlot}`);
      if (banquetSpaceType) lines.push(`- Không gian mong muốn: ${banquetSpaceType}`);
    } else if (isPhotoOrVideo) {
      lines.push(`[Yêu cầu Quay phim & Chụp ảnh cưới]`);
      if (photoPackages.length > 0) lines.push(`- Gói quan tâm: ${photoPackages.join(', ')}`);
      if (shootLocation) lines.push(`- Địa điểm chụp mong muốn: ${shootLocation}`);
      if (photoColorGrade) lines.push(`- Tone màu yêu thích: ${photoColorGrade}`);
    } else if (isMakeup) {
      lines.push(`[Yêu cầu Chuyên viên Trang điểm & Làm tóc]`);
      if (makeupClients.length > 0) lines.push(`- Suất makeup: ${makeupClients.join(', ')}`);
      if (makeupLocation) lines.push(`- Địa điểm makeup: ${makeupLocation}`);
      if (makeupFinishTime) lines.push(`- Giờ cần hoàn thành: ${makeupFinishTime}`);
    } else if (isDecor) {
      lines.push(`[Yêu cầu Trang trí tiệc & Không gian]`);
      if (decorAreas.length > 0) lines.push(`- Khu vực trang trí: ${decorAreas.join(', ')}`);
      if (decorColorConcept) lines.push(`- Concept & Tone màu: ${decorColorConcept}`);
    } else if (isInvitation) {
      lines.push(`[Yêu cầu Thiệp cưới & Quà tặng]`);
      if (invitationQuantity) lines.push(`- Số lượng thiệp: ${invitationQuantity} bộ`);
      if (invitationType) lines.push(`- Hình thức thiệp: ${invitationType}`);
      if (invitationDueDate) lines.push(`- Ngày cần nhận thiệp: ${invitationDueDate}`);
    } else if (isCombo) {
      lines.push(`[Yêu cầu Gói Combo Trọn Gói]`);
      if (subServices.length > 0) lines.push(`- Dịch vụ bao gồm: ${subServices.join(', ')}`);
      if (estimatedGuests) lines.push(`- Quy mô khách: ${estimatedGuests} khách`);
    }

    if (userNotes.trim()) {
      lines.push(`---`);
      lines.push(`Ghi chú thêm: ${userNotes.trim()}`);
    }

    return lines.join('\n');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    const fullNotes = buildStructuredNotes();

    const payload: SendLeadRequest = {
      listingId,
      vendorId,
      phoneNumber,
      weddingDate: weddingDate ? new Date(weddingDate).toISOString() : undefined,
      estimatedGuests: isVenueOrBanquet || isCombo ? estimatedGuests : undefined,
      estimatedBudget,
      notes: fullNotes,
    };

    try {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/leads', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const errorData = await res.json().catch(() => ({}));
        throw new Error(errorData.detail || errorData.message || 'Không thể gửi yêu cầu tư vấn.');
      }

      const data: SendLeadResponse = await res.json();
      setSuccessResult(data);
      if (onSuccess) onSuccess(data);
    } catch (err: any) {
      setError(err.message || 'Đã có lỗi xảy ra.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleCopyCode = () => {
    if (successResult?.voucher?.code) {
      navigator.clipboard.writeText(successResult.voucher.code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-white rounded-3xl max-w-lg w-full max-h-[90vh] overflow-y-auto shadow-2xl border border-rose-100 relative">
        {/* Nút đóng */}
        <button
          onClick={onClose}
          className="absolute top-4 right-4 w-9 h-9 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 hover:text-slate-700 transition z-10 cursor-pointer"
        >
          <X className="w-5 h-5" />
        </button>

        {/* Trạng thái thành công */}
        {successResult ? (
          <div className="p-8 text-center space-y-6">
            <div className="w-16 h-16 rounded-3xl bg-gradient-to-tr from-rose-500 to-pink-500 flex items-center justify-center text-white mx-auto shadow-lg shadow-rose-200 animate-bounce">
              <Gift className="w-8 h-8" />
            </div>

            <div>
              <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-50 text-emerald-600 text-xs font-bold mb-2">
                <CheckCircle className="w-3.5 h-3.5" /> Gửi tư vấn thành công!
              </div>
              <h3 className="text-2xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
                Chúc mừng bạn đã nhận ưu đãi!
              </h3>
              <p className="text-xs text-slate-500 mt-1 max-w-xs mx-auto">
                Nhà cung cấp <strong className="text-slate-700">{vendorName}</strong> đã nhận được yêu cầu tư vấn chi tiết và cam kết phản hồi trong 2 giờ.
              </p>
            </div>

            {/* Voucher Card BR-004 */}
            <div className="p-5 rounded-2xl bg-gradient-to-br from-amber-50 via-rose-50 to-pink-50 border-2 border-dashed border-rose-200 relative overflow-hidden">
              <div className="flex justify-between items-center mb-2">
                <span className="text-[11px] font-bold uppercase tracking-wider text-rose-600 flex items-center gap-1">
                  <Sparkles className="w-3.5 h-3.5" /> Mã Ưu Đãi 8 Ký Tự
                </span>
                <span className="text-[10px] font-semibold text-slate-400">
                  Hiệu lực 30 ngày (BR-004)
                </span>
              </div>

              <div className="flex items-center justify-center gap-3 my-3">
                <span className="font-mono text-3xl font-extrabold tracking-widest text-slate-900 bg-white px-4 py-2 rounded-xl shadow-sm border border-rose-100">
                  {successResult.voucher.code}
                </span>
                <button
                  type="button"
                  onClick={handleCopyCode}
                  className="p-3 rounded-xl bg-rose-500 hover:bg-rose-600 text-white font-bold text-xs shadow-md transition flex items-center gap-1 cursor-pointer"
                  title="Sao chép mã"
                >
                  {copied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                </button>
              </div>

              <p className="text-xs font-semibold text-rose-700">
                🎁 Trị giá {successResult.voucher.discountValue?.toLocaleString('vi-VN')}đ khấu trừ trực tiếp khi ký hợp đồng
              </p>
            </div>

            <p className="text-[11px] text-slate-400">
              🔒 <strong>Smart Privacy (BR-002):</strong> Số điện thoại của bạn đang được ẩn an toàn dạng <code className="bg-slate-100 px-1 py-0.5 rounded">0987***123</code>.
            </p>

            <button
              onClick={onClose}
              className="w-full py-3 rounded-xl bg-slate-900 hover:bg-slate-800 text-white font-bold text-sm shadow-md transition cursor-pointer"
            >
              Đã hiểu &amp; Hoàn tất
            </button>
          </div>
        ) : (
          /* Form nhập thông tin chi tiết */
          <form onSubmit={handleSubmit} className="p-6 sm:p-7 space-y-4">
            <div>
              <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-rose-50 text-rose-600 text-xs font-bold mb-2">
                <Gift className="w-3.5 h-3.5" /> Tặng Voucher 500.000đ khi gửi tư vấn
              </div>
              <h3 className="text-xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
                Tư Vấn &amp; Báo Giá {categoryName ? `(${categoryName})` : 'Trực Tiếp'}
              </h3>
              <p className="text-xs text-slate-500 mt-0.5">
                Gửi tới <strong>{vendorName}</strong> {listingTitle && `— ${listingTitle}`}
              </p>
            </div>

            {error && (
              <div className="p-3 rounded-xl bg-rose-50 border border-rose-100 text-rose-600 text-xs font-medium">
                {error}
              </div>
            )}

            <div className="space-y-3.5">
              {/* SĐT */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <Phone className="w-3.5 h-3.5 text-rose-500" />
                  Số Điện Thoại Nhận Báo Giá <span className="text-rose-500">*</span>
                </label>
                <input
                  type="tel"
                  placeholder="VD: 0987654321"
                  value={phoneNumber}
                  onChange={(e) => setPhoneNumber(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                  required
                />
                <p className="text-[10px] text-slate-400 mt-1">
                  🔒 Smart Privacy: NCC sẽ chỉ nhìn thấy dạng <code className="bg-slate-100 px-1 rounded">0987***123</code>
                </p>
              </div>

              {/* Ngày cưới (không bắt buộc) */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <Calendar className="w-3.5 h-3.5 text-rose-500" />
                  Ngày Cưới Dự Kiến (Có thể bàn bạc thêm qua tin nhắn)
                </label>
                <input
                  type="date"
                  value={weddingDate}
                  onChange={(e) => setWeddingDate(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                />
              </div>

              {/* DYNAMIC FORM SECTION BASED ON CATEGORY */}

              {/* 1. VÁY CƯỚI & VEST */}
              {isDressOrSuit && (
                <div className="p-4 rounded-2xl bg-rose-50/50 border border-rose-100 space-y-3">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-rose-800">
                    <Shirt size={14} />
                    <span>Thông tin thử trang phục &amp; may đo riêng</span>
                  </div>

                  {/* Outfit options */}
                  <div>
                    <label className="block text-[11px] font-semibold text-slate-600 mb-1.5">
                      Trang phục hai bạn cần:
                    </label>
                    <div className="grid grid-cols-2 gap-1.5 text-xs">
                      {[
                        'Váy cưới chính lễ đường',
                        'Váy dạ hội đi bàn',
                        'Vest chú rể',
                        'Áo dài cặp lễ gia tiên',
                        'Áo dài 2 mẹ (bà sui)',
                      ].map((item) => {
                        const checked = selectedOutfits.includes(item);
                        return (
                          <button
                            type="button"
                            key={item}
                            onClick={() => toggleArrayItem(selectedOutfits, setSelectedOutfits, item)}
                            className={`p-2 rounded-lg text-left border transition text-[11px] font-medium flex items-center gap-1.5 cursor-pointer ${
                              checked
                                ? 'bg-rose-100/70 border-rose-300 text-rose-900 font-bold'
                                : 'bg-white border-slate-200 text-slate-600'
                            }`}
                          >
                            <span
                              className={`w-3.5 h-3.5 rounded border flex items-center justify-center text-[10px] ${
                                checked ? 'bg-rose-500 border-rose-500 text-white' : 'border-slate-300'
                              }`}
                            >
                              {checked && '✓'}
                            </span>
                            <span className="truncate">{item}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  {/* Fitting Date */}
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="block text-[11px] font-semibold text-slate-600 mb-1">
                        Ngày hẹn thử đồ dự kiến:
                      </label>
                      <input
                        type="date"
                        value={fittingDate}
                        onChange={(e) => setFittingDate(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      />
                    </div>
                    <div>
                      <label className="block text-[11px] font-semibold text-slate-600 mb-1">
                        Chiều cao &amp; Cân nặng (dâu rể):
                      </label>
                      <input
                        type="text"
                        placeholder="VD: Dâu 1m62/48kg, Rể 1m75/68kg"
                        value={bodyMeasurements}
                        onChange={(e) => setBodyMeasurements(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      />
                    </div>
                  </div>

                  {/* Style */}
                  <div>
                    <label className="block text-[11px] font-semibold text-slate-600 mb-1">
                      Phong cách trang phục mong muốn:
                    </label>
                    <select
                      value={outfitStyle}
                      onChange={(e) => setOutfitStyle(e.target.value)}
                      className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                    >
                      <option value="Công chúa lộng lẫy bồng xòe">Công chúa lộng lẫy bồng xòe</option>
                      <option value="Đuôi cá quyến rũ gợi cảm">Đuôi cá quyến rũ gợi cảm</option>
                      <option value="Lụa satin tối giản tinh tế (Minimal)">Lụa satin tối giản tinh tế (Minimal)</option>
                      <option value="Tuxedo cổ điển quý phái">Tuxedo cổ điển quý phái</option>
                      <option value="Áo dài truyền thống gấm thêu tay">Áo dài truyền thống gấm thêu tay</option>
                    </select>
                  </div>
                </div>
              )}

              {/* 2. ĐỊA ĐIỂM & TIỆC CƯỚI */}
              {isVenueOrBanquet && (
                <div className="p-4 rounded-2xl bg-amber-50/50 border border-amber-100 space-y-3">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-amber-800">
                    <Building2 size={14} />
                    <span>Thông tin sảnh tiệc &amp; lượng khách</span>
                  </div>

                  <div className="grid grid-cols-2 gap-2.5">
                    <div>
                      <label className="block text-[11px] font-semibold text-slate-700 mb-1 flex items-center gap-1">
                        <Users size={12} className="text-amber-600" />
                        Số lượng khách dự kiến:
                      </label>
                      <input
                        type="number"
                        min="1"
                        placeholder="VD: 300 khách"
                        value={estimatedGuests || ''}
                        onChange={(e) => setEstimatedGuests(e.target.value ? parseInt(e.target.value) : undefined)}
                        className="w-full px-3 py-1.5 rounded-lg border border-slate-200 text-xs bg-white font-bold text-slate-800"
                      />
                    </div>

                    <div>
                      <label className="block text-[11px] font-semibold text-slate-700 mb-1">
                        Số bàn tiệc dự trù:
                      </label>
                      <input
                        type="number"
                        min="1"
                        placeholder="VD: 30 bàn"
                        value={tableCount || ''}
                        onChange={(e) => setTableCount(e.target.value ? parseInt(e.target.value) : undefined)}
                        className="w-full px-3 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2.5">
                    <div>
                      <label className="block text-[11px] font-semibold text-slate-700 mb-1">
                        Khung giờ tiệc:
                      </label>
                      <select
                        value={banquetTimeSlot}
                        onChange={(e) => setBanquetTimeSlot(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      >
                        <option value="Tiệc trưa (11:00 - 13:30)">Tiệc trưa (11:00 - 13:30)</option>
                        <option value="Tiệc tối (17:30 - 21:00)">Tiệc tối (17:30 - 21:00)</option>
                        <option value="Cả ngày">Cả ngày</option>
                      </select>
                    </div>

                    <div>
                      <label className="block text-[11px] font-semibold text-slate-700 mb-1">
                        Không gian tiệc:
                      </label>
                      <select
                        value={banquetSpaceType}
                        onChange={(e) => setBanquetSpaceType(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      >
                        <option value="Ballroom sảnh tiệc trong nhà">Ballroom trong nhà</option>
                        <option value="Sân vườn ngoài trời (Garden)">Sân vườn ngoài trời</option>
                        <option value="Ven sông / Hồ bơi">Ven sông / Hồ bơi</option>
                        <option value="Rooftop thoáng đãng">Rooftop thoáng đãng</option>
                      </select>
                    </div>
                  </div>
                </div>
              )}

              {/* 3. QUAY PHIM & CHỤP ẢNH */}
              {isPhotoOrVideo && (
                <div className="p-4 rounded-2xl bg-sky-50/50 border border-sky-100 space-y-3">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-sky-800">
                    <Camera size={14} />
                    <span>Nhu cầu gói quay chụp &amp; phong cách</span>
                  </div>

                  <div>
                    <label className="block text-[11px] font-semibold text-slate-600 mb-1.5">
                      Gói dịch vụ bạn quan tâm:
                    </label>
                    <div className="grid grid-cols-2 gap-1.5 text-xs">
                      {[
                        'Chụp Pre-wedding studio',
                        'Chụp ngoại cảnh / Phim trường',
                        'Phóng sự cưới trọn ngày',
                        'Quay Highlight Cinematic',
                        'Flycam trên cao',
                      ].map((pkg) => {
                        const checked = photoPackages.includes(pkg);
                        return (
                          <button
                            type="button"
                            key={pkg}
                            onClick={() => toggleArrayItem(photoPackages, setPhotoPackages, pkg)}
                            className={`p-2 rounded-lg text-left border transition text-[11px] font-medium flex items-center gap-1.5 cursor-pointer ${
                              checked
                                ? 'bg-sky-100/70 border-sky-300 text-sky-900 font-bold'
                                : 'bg-white border-slate-200 text-slate-600'
                            }`}
                          >
                            <span
                              className={`w-3.5 h-3.5 rounded border flex items-center justify-center text-[10px] ${
                                checked ? 'bg-sky-500 border-sky-500 text-white' : 'border-slate-300'
                              }`}
                            >
                              {checked && '✓'}
                            </span>
                            <span className="truncate">{pkg}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="block text-[11px] font-semibold text-slate-600 mb-1">
                        Địa điểm chụp mong muốn:
                      </label>
                      <input
                        type="text"
                        placeholder="VD: Studio / Đà Lạt / Biển..."
                        value={shootLocation}
                        onChange={(e) => setShootLocation(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      />
                    </div>

                    <div>
                      <label className="block text-[11px] font-semibold text-slate-600 mb-1">
                        Tone màu ảnh yêu thích:
                      </label>
                      <select
                        value={photoColorGrade}
                        onChange={(e) => setPhotoColorGrade(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      >
                        <option value="Hàn Quốc trong trẻo, tự nhiên">Hàn Quốc trong trẻo</option>
                        <option value="Vintage ấm áp hoài niệm">Vintage ấm áp</option>
                        <option value="Màu film điện ảnh Cinematic">Màu film Cinematic</option>
                        <option value="Tone Tây tương phản cao">Tone Tây sang trọng</option>
                      </select>
                    </div>
                  </div>
                </div>
              )}

              {/* 4. TRANG ĐIỂM (MAKEUP) */}
              {isMakeup && (
                <div className="p-4 rounded-2xl bg-fuchsia-50/50 border border-fuchsia-100 space-y-3">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-fuchsia-800">
                    <Sparkle size={14} />
                    <span>Lịch trình makeup &amp; số người</span>
                  </div>

                  <div>
                    <label className="block text-[11px] font-semibold text-slate-600 mb-1.5">
                      Ai cần makeup &amp; làm tóc:
                    </label>
                    <div className="grid grid-cols-2 gap-1.5 text-xs">
                      {[
                        'Cô dâu (Tiệc cưới chính)',
                        'Cô dâu (Lễ gia tiên)',
                        '2 Mẹ (Mẹ dâu & Mẹ rể)',
                        'Đội phù dâu / Bưng quả (4-8 người)',
                      ].map((item) => {
                        const checked = makeupClients.includes(item);
                        return (
                          <button
                            type="button"
                            key={item}
                            onClick={() => toggleArrayItem(makeupClients, setMakeupClients, item)}
                            className={`p-2 rounded-lg text-left border transition text-[11px] font-medium flex items-center gap-1.5 cursor-pointer ${
                              checked
                                ? 'bg-fuchsia-100/70 border-fuchsia-300 text-fuchsia-900 font-bold'
                                : 'bg-white border-slate-200 text-slate-600'
                            }`}
                          >
                            <span
                              className={`w-3.5 h-3.5 rounded border flex items-center justify-center text-[10px] ${
                                checked ? 'bg-fuchsia-500 border-fuchsia-500 text-white' : 'border-slate-300'
                              }`}
                            >
                              {checked && '✓'}
                            </span>
                            <span className="truncate">{item}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="block text-[11px] font-semibold text-slate-600 mb-1">
                        Địa điểm làm đẹp:
                      </label>
                      <select
                        value={makeupLocation}
                        onChange={(e) => setMakeupLocation(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      >
                        <option value="Tại nhà riêng / Khách sạn (Chuyên viên đến tận nơi)">Tại nhà / Khách sạn</option>
                        <option value="Tại Studio của chuyên viên">Tại Studio của chuyên viên</option>
                      </select>
                    </div>

                    <div>
                      <label className="block text-[11px] font-semibold text-slate-600 mb-1 flex items-center gap-1">
                        <Clock size={11} className="text-fuchsia-600" />
                        Giờ cần hoàn thành:
                      </label>
                      <input
                        type="time"
                        value={makeupFinishTime}
                        onChange={(e) => setMakeupFinishTime(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white font-bold text-slate-800"
                      />
                    </div>
                  </div>
                </div>
              )}

              {/* 5. TRANG TRÍ (DECOR) */}
              {isDecor && (
                <div className="p-4 rounded-2xl bg-emerald-50/50 border border-emerald-100 space-y-3">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-emerald-800">
                    <Palette size={14} />
                    <span>Hạng mục không gian &amp; concept trang trí</span>
                  </div>

                  <div>
                    <label className="block text-[11px] font-semibold text-slate-600 mb-1.5">
                      Khu vực cần trang trí:
                    </label>
                    <div className="grid grid-cols-2 gap-1.5 text-xs">
                      {[
                        'Lễ gia tiên tại tư gia',
                        'Cổng hoa & Backdrop đón khách',
                        'Lối đi lễ đường & Sân khấu tiệc',
                        'Bàn Gallery & Tháp bánh cưới',
                      ].map((area) => {
                        const checked = decorAreas.includes(area);
                        return (
                          <button
                            type="button"
                            key={area}
                            onClick={() => toggleArrayItem(decorAreas, setDecorAreas, area)}
                            className={`p-2 rounded-lg text-left border transition text-[11px] font-medium flex items-center gap-1.5 cursor-pointer ${
                              checked
                                ? 'bg-emerald-100/70 border-emerald-300 text-emerald-900 font-bold'
                                : 'bg-white border-slate-200 text-slate-600'
                            }`}
                          >
                            <span
                              className={`w-3.5 h-3.5 rounded border flex items-center justify-center text-[10px] ${
                                checked ? 'bg-emerald-500 border-emerald-500 text-white' : 'border-slate-300'
                              }`}
                            >
                              {checked && '✓'}
                            </span>
                            <span className="truncate">{area}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <div>
                    <label className="block text-[11px] font-semibold text-slate-600 mb-1">
                      Tone màu &amp; Concept chủ đạo:
                    </label>
                    <input
                      type="text"
                      placeholder="VD: Trắng pastel lãng mạn, Đỏ truyền thống, Xanh olive tự nhiên..."
                      value={decorColorConcept}
                      onChange={(e) => setDecorColorConcept(e.target.value)}
                      className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                    />
                  </div>
                </div>
              )}

              {/* 6. THIỆP CƯỚI & QUÀ */}
              {isInvitation && (
                <div className="p-4 rounded-2xl bg-indigo-50/50 border border-indigo-100 space-y-3">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-indigo-800">
                    <Mail size={14} />
                    <span>Số lượng thiệp &amp; thời gian nhận</span>
                  </div>

                  <div className="grid grid-cols-2 gap-2.5">
                    <div>
                      <label className="block text-[11px] font-semibold text-slate-700 mb-1">
                        Số lượng thiệp dự kiến:
                      </label>
                      <input
                        type="number"
                        min="10"
                        placeholder="VD: 300 bộ"
                        value={invitationQuantity || ''}
                        onChange={(e) => setInvitationQuantity(e.target.value ? parseInt(e.target.value) : undefined)}
                        className="w-full px-3 py-1.5 rounded-lg border border-slate-200 text-xs bg-white font-bold text-slate-800"
                      />
                    </div>

                    <div>
                      <label className="block text-[11px] font-semibold text-slate-700 mb-1">
                        Ngày cần nhận thiệp:
                      </label>
                      <input
                        type="date"
                        value={invitationDueDate}
                        onChange={(e) => setInvitationDueDate(e.target.value)}
                        className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                      />
                    </div>
                  </div>

                  <div>
                    <label className="block text-[11px] font-semibold text-slate-700 mb-1">
                      Hình thức thiệp:
                    </label>
                    <select
                      value={invitationType}
                      onChange={(e) => setInvitationType(e.target.value)}
                      className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 text-xs bg-white"
                    >
                      <option value="Thiệp giấy in cao cấp ép kim / sáp niêm">Thiệp giấy in cao cấp ép kim / sáp niêm</option>
                      <option value="Thiệp cưới điện tử (Online website / Video QR)">Thiệp online QR điện tử</option>
                      <option value="Cả thiệp giấy in cao cấp & Thiệp online QR">Cả thiệp giấy in &amp; Thiệp online</option>
                    </select>
                  </div>
                </div>
              )}

              {/* Ngân sách */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <DollarSign className="w-3.5 h-3.5 text-rose-500" />
                  Ngân Sách Dự Trù Cho Dịch Vụ Này (VNĐ)
                </label>
                <MoneyInput
                  placeholder="VD: 15.000.000"
                  fractionDigits={0}
                  value={estimatedBudget ?? null}
                  onValueChange={(val) => setEstimatedBudget(val ?? undefined)}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 font-semibold"
                />
              </div>

              {/* Ghi chú */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <FileText className="w-3.5 h-3.5 text-rose-500" />
                  Ghi Chú Yêu Cầu Chi Tiết Khác
                </label>
                <textarea
                  rows={2}
                  placeholder="VD: Hai bạn có mong muốn riêng gì cần trao đổi trước với NCC..."
                  value={userNotes}
                  onChange={(e) => setUserNotes(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 resize-none text-slate-700"
                />
              </div>
            </div>

            {/* SLA Badge */}
            <div className="p-3 rounded-xl bg-amber-50 border border-amber-100 text-amber-800 text-[11px] flex items-center gap-2">
              <span className="font-bold">⚡ Cam kết SLA:</span>
              <span>NCC phản hồi trong vòng 2 giờ. Tự động nhận Voucher 8 ký tự ngay khi gửi.</span>
            </div>

            <div className="flex gap-2 pt-2">
              <button
                type="button"
                onClick={onClose}
                className="w-1/3 py-2.5 px-4 rounded-xl border border-slate-200 text-slate-600 font-bold text-sm hover:bg-slate-50 transition cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={isLoading}
                className="w-2/3 py-2.5 px-4 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-bold text-sm shadow-md transition disabled:opacity-50 cursor-pointer flex items-center justify-center gap-1.5"
              >
                {isLoading ? 'Đang gửi...' : '💌 Gửi Báo Giá & Nhận Voucher'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
