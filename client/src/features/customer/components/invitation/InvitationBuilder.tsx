import { useState, useEffect } from 'react';
import {
  Save, Copy, ExternalLink, Sparkles, Calendar,
  MapPin, CheckCircle2, XCircle, Users, Heart,
  Loader2, Check
} from 'lucide-react';
import { useMyInvitation, useInvitationRsvps } from '../../hooks/useInvitations';
import type { SaveInvitationRequest } from '../../../../types/invitation.types';

const THEMES = [
  { id: 'rustic', name: 'Rustic Greenery', icon: '🌿', bg: 'from-emerald-50 to-amber-50', primary: '#2a9d8f' },
  { id: 'minimalist', name: 'Minimalist Cream', icon: '🕊️', bg: 'from-stone-50 to-orange-50', primary: '#c97a7e' },
  { id: 'luxury', name: 'Modern Luxury Gold', icon: '✨', bg: 'from-amber-50 to-yellow-100', primary: '#d97706' },
];

export function InvitationBuilder() {
  const { invitation, isLoading, saveInvitation, isSaving } = useMyInvitation();
  const { data: rsvps = [] } = useInvitationRsvps();

  const [formData, setFormData] = useState<SaveInvitationRequest>({
    slug: 'long-thu-2026',
    groomName: 'Hoàng Long',
    brideName: 'Minh Thư',
    eventDate: '2026-11-15T18:00',
    venueName: 'White Palace - Sảnh Grand Ballroom',
    venueAddress: '194 Hoàng Văn Thụ, Q. Phú Nhuận, TP.HCM',
    mapUrl: 'https://maps.google.com',
    loveStory: 'Hạnh phúc không phải là điểm đến, mà là hành trình chúng mình cùng nhau đi qua. Rất mong được đón tiếp bạn trong ngày vui trọng đại của chúng mình!',
    coverImageUrl: 'https://images.unsplash.com/photo-1519741497674-611481863552?w=800',
    templateStyle: 'rustic',
    bankInfo: 'MBBank - 0338889999 - HOANG LONG (Mừng cưới)',
  });

  const [isCopied, setIsCopied] = useState(false);
  const [successToast, setSuccessToast] = useState<string | null>(null);

  useEffect(() => {
    if (invitation) {
      setFormData({
        slug: invitation.slug || 'long-thu-2026',
        groomName: invitation.groomName || 'Hoàng Long',
        brideName: invitation.brideName || 'Minh Thư',
        eventDate: invitation.eventDate ? invitation.eventDate.substring(0, 16) : '2026-11-15T18:00',
        venueName: invitation.venueName || 'White Palace - Sảnh Grand Ballroom',
        venueAddress: invitation.venueAddress || '194 Hoàng Văn Thụ, Q. Phú Nhuận, TP.HCM',
        mapUrl: invitation.mapUrl || '',
        loveStory: invitation.loveStory || '',
        coverImageUrl: invitation.coverImageUrl || 'https://images.unsplash.com/photo-1519741497674-611481863552?w=800',
        templateStyle: invitation.templateStyle || 'rustic',
        bankInfo: invitation.bankInfo || '',
      });
    }
  }, [invitation]);

  const handleSave = async () => {
    try {
      const res = await saveInvitation(formData);
      setSuccessToast(`Đã xuất bản thiệp cưới thành công! Đường dẫn: /invitation/${res.slug}`);
      setTimeout(() => setSuccessToast(null), 5000);
    } catch (err: unknown) {
      const e = err as Error;
      alert(e.message || 'Lỗi lưu thiệp');
    }
  };

  const invitationUrl = `${window.location.origin}/invitation/${formData.slug || 'long-thu-2026'}`;

  const copyLink = () => {
    navigator.clipboard.writeText(invitationUrl);
    setIsCopied(true);
    setTimeout(() => setIsCopied(false), 2500);
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[300px] text-slate-500">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-semibold">Đang tải cấu hình thiệp cưới...</p>
      </div>
    );
  }

  // Thống kê RSVP
  const totalGuests = rsvps.length;
  const attendingGuests = rsvps.filter(r => r.status === 'Attending');
  const attendingCount = attendingGuests.length;
  const notAttendingCount = rsvps.filter(r => r.status === 'NotAttending').length;
  const totalAccompanying = attendingGuests.reduce((sum, r) => sum + r.companionCount, 0);
  const totalAttendingSeats = attendingCount + totalAccompanying;
  const estimatedTables = Math.max(1, Math.ceil(totalAttendingSeats / 10));

  return (
    <div className="space-y-6">
      {/* Top Banner Action Bar */}
      <div className="bg-white p-6 rounded-3xl border border-slate-200 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-0.5 rounded-full bg-rose-100 text-rose-700 font-extrabold text-[11px] uppercase tracking-wider">
              Epic 7 • E-Invitation & RSVP 1-Chạm
            </span>
            <span className="text-xs text-slate-400">•</span>
            <span className="text-xs font-semibold text-slate-600">
              Cặp đôi: <strong className="text-slate-900">{formData.groomName} & {formData.brideName}</strong>
            </span>
          </div>
          <h2 className="text-2xl font-black text-slate-900 mt-1">Trình Tạo Thiệp Mời Cưới Online</h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Tự động tạo website thiệp cưới sang trọng, hỗ trợ bản đồ chỉ đường & khách mời RSVP 1-chạm không cần đăng nhập.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            onClick={copyLink}
            className="px-4 py-2.5 rounded-xl border border-slate-200 hover:bg-slate-100 text-slate-700 font-bold text-xs flex items-center gap-1.5 transition shadow-xs"
          >
            {isCopied ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4 text-slate-500" />}
            <span>{isCopied ? 'Đã chép link thiệp' : 'Copy Link Thiệp'}</span>
          </button>

          <a
            href={`/invitation/${formData.slug}`}
            target="_blank"
            rel="noreferrer"
            className="px-4 py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-800 font-bold text-xs flex items-center gap-1.5 transition"
          >
            <ExternalLink className="w-4 h-4 text-slate-500" />
            <span>Xem Trang Thiệp</span>
          </a>

          <button
            onClick={handleSave}
            disabled={isSaving}
            className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white font-bold text-xs shadow-md shadow-rose-600/20 flex items-center gap-1.5 transition disabled:opacity-50"
          >
            {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            <span>Lưu & Xuất Bản</span>
          </button>
        </div>
      </div>

      {successToast && (
        <div className="p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-2xl flex items-center gap-2 text-sm shadow-xs animate-in fade-in">
          <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
          <span className="font-semibold">{successToast}</span>
        </div>
      )}

      {/* Grid Editor: Left Form & Right Simulator */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
        {/* LEFT COLUMN: BUILDER SETTINGS & RSVP GUEST LIST (col-span-7) */}
        <div className="lg:col-span-7 space-y-6">
          {/* Card Form Cấu Hình */}
          <div className="bg-white rounded-3xl border border-slate-200 shadow-xs p-6 space-y-5">
            <h3 className="font-extrabold text-base text-slate-900 pb-3 border-b border-slate-100 flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-rose-500" />
              Tùy Chỉnh Nội Dung Thiệp Mời
            </h3>

            {/* Theme Picker */}
            <div>
              <label className="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-2">
                Chọn Phong Cách Giao Diện (Theme):
              </label>
              <div className="grid grid-cols-3 gap-3">
                {THEMES.map(theme => (
                  <button
                    key={theme.id}
                    type="button"
                    onClick={() => setFormData(f => ({ ...f, templateStyle: theme.id }))}
                    className={`p-3 rounded-2xl border text-center transition-all duration-200 ${
                      formData.templateStyle === theme.id
                        ? 'border-rose-500 bg-rose-50/70 shadow-xs ring-2 ring-rose-200'
                        : 'border-slate-200 hover:border-slate-300 bg-slate-50/50'
                    }`}
                  >
                    <div className="text-2xl mb-1">{theme.icon}</div>
                    <div className="font-bold text-xs text-slate-800">{theme.name}</div>
                  </button>
                ))}
              </div>
            </div>

            {/* Slug URL */}
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Đường dẫn liên kết thiệp (Slug):
              </label>
              <div className="flex items-center rounded-xl border border-slate-300 bg-slate-50 px-3 py-2 text-xs">
                <span className="text-slate-400 font-mono">exe.wedding/invitation/</span>
                <input
                  type="text"
                  value={formData.slug}
                  onChange={(e) => setFormData(f => ({ ...f, slug: e.target.value }))}
                  className="bg-transparent font-bold text-slate-900 focus:outline-none flex-1 font-mono ml-0.5"
                  placeholder="long-thu-2026"
                />
              </div>
            </div>

            {/* Groom & Bride Names */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Tên Chú Rể:</label>
                <input
                  type="text"
                  value={formData.groomName}
                  onChange={(e) => setFormData(f => ({ ...f, groomName: e.target.value }))}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 font-semibold"
                  placeholder="Hoàng Long"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Tên Cô Dâu:</label>
                <input
                  type="text"
                  value={formData.brideName}
                  onChange={(e) => setFormData(f => ({ ...f, brideName: e.target.value }))}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 font-semibold"
                  placeholder="Minh Thư"
                />
              </div>
            </div>

            {/* Date & Time */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Thời Gian Tổ Chức:</label>
                <input
                  type="datetime-local"
                  value={formData.eventDate}
                  onChange={(e) => setFormData(f => ({ ...f, eventDate: e.target.value }))}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Tên Địa Điểm Tiệc Cưới:</label>
                <input
                  type="text"
                  value={formData.venueName}
                  onChange={(e) => setFormData(f => ({ ...f, venueName: e.target.value }))}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
                  placeholder="Trung tâm White Palace - Sảnh Grand"
                />
              </div>
            </div>

            {/* Venue Address & Maps */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Địa Chỉ Chi Tiết:</label>
                <input
                  type="text"
                  value={formData.venueAddress}
                  onChange={(e) => setFormData(f => ({ ...f, venueAddress: e.target.value }))}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
                  placeholder="194 Hoàng Văn Thụ, Q. Phú Nhuận, TP.HCM"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Link Google Maps:</label>
                <input
                  type="text"
                  value={formData.mapUrl || ''}
                  onChange={(e) => setFormData(f => ({ ...f, mapUrl: e.target.value }))}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
                  placeholder="https://maps.google.com/..."
                />
              </div>
            </div>

            {/* Cover Image URL */}
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Link Ảnh Cưới Đại Diện:</label>
              <input
                type="text"
                value={formData.coverImageUrl || ''}
                onChange={(e) => setFormData(f => ({ ...f, coverImageUrl: e.target.value }))}
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
                placeholder="https://images.unsplash.com/..."
              />
            </div>

            {/* Love Story */}
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Lời Ngỏ Tình Yêu (Love Story):</label>
              <textarea
                value={formData.loveStory || ''}
                onChange={(e) => setFormData(f => ({ ...f, loveStory: e.target.value }))}
                rows={3}
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
                placeholder="Hạnh phúc không phải là điểm đến..."
              />
            </div>

            {/* Bank Info for Online Gift Box */}
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">Hộp Mừng Cưới Online (Tài khoản / VietQR):</label>
              <input
                type="text"
                value={formData.bankInfo || ''}
                onChange={(e) => setFormData(f => ({ ...f, bankInfo: e.target.value }))}
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
                placeholder="MBBank - 0338889999 - HOANG LONG (Mừng cưới)"
              />
            </div>
          </div>

          {/* Card Thống Kê Khách Mời & RSVP Tracking */}
          <div className="bg-white rounded-3xl border border-slate-200 shadow-xs p-6 space-y-5">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div>
                <h3 className="font-extrabold text-base text-slate-900 flex items-center gap-2">
                  <Users className="w-4 h-4 text-emerald-600" />
                  Thống Kê Khách Mời & Phản Hồi RSVP 1-Chạm
                </h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Tự động cập nhật thời gian thực khi khách bấm xác nhận trên link thiệp cưới
                </p>
              </div>
            </div>

            {/* 4 Thẻ chỉ số RSVP */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <div className="bg-slate-50 p-4 rounded-2xl border border-slate-200 text-center">
                <div className="text-[10px] font-extrabold uppercase tracking-wider text-slate-500">TỔNG KHÁCH</div>
                <div className="text-xl font-black text-slate-900 mt-1">{totalGuests}</div>
              </div>

              <div className="bg-emerald-50 p-4 rounded-2xl border border-emerald-200 text-center">
                <div className="text-[10px] font-extrabold uppercase tracking-wider text-emerald-700">SẼ THAM DỰ</div>
                <div className="text-xl font-black text-emerald-700 mt-1">{attendingCount} (+{totalAccompanying})</div>
              </div>

              <div className="bg-rose-50 p-4 rounded-2xl border border-rose-200 text-center">
                <div className="text-[10px] font-extrabold uppercase tracking-wider text-rose-700">BẬN VIỆC</div>
                <div className="text-xl font-black text-rose-700 mt-1">{notAttendingCount}</div>
              </div>

              <div className="bg-amber-50 p-4 rounded-2xl border border-amber-200 text-center">
                <div className="text-[10px] font-extrabold uppercase tracking-wider text-amber-800">DỰ KIẾN BÀN</div>
                <div className="text-xl font-black text-amber-800 mt-1">{estimatedTables} Bàn</div>
              </div>
            </div>

            {/* Danh sách phản hồi khách */}
            <div className="overflow-x-auto">
              {rsvps.length === 0 ? (
                <div className="text-center py-8 text-slate-400 text-xs">
                  Chưa có khách mời nào phản hồi RSVP. Hãy copy link thiệp gửi cho bạn bè nhé!
                </div>
              ) : (
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="bg-slate-50 text-slate-500 font-bold uppercase tracking-wider border-b border-slate-200">
                      <th className="py-2.5 px-3">Khách Mời</th>
                      <th className="py-2.5 px-3">Trạng Thái</th>
                      <th className="py-2.5 px-3 text-center">Đi Cùng</th>
                      <th className="py-2.5 px-3">Lời Chúc</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {rsvps.map(rsvp => (
                      <tr key={rsvp.id} className="hover:bg-slate-50/50">
                        <td className="py-2.5 px-3 font-bold text-slate-800">
                          {rsvp.guestName}
                          {rsvp.phoneNumber && <span className="block text-[10px] text-slate-400 font-normal">{rsvp.phoneNumber}</span>}
                        </td>
                        <td className="py-2.5 px-3">
                          {rsvp.status === 'Attending' ? (
                            <span className="inline-flex items-center gap-1 text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded-full font-bold">
                              <CheckCircle2 className="w-3 h-3" /> Tham dự
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 text-rose-700 bg-rose-100 px-2 py-0.5 rounded-full font-bold">
                              <XCircle className="w-3 h-3" /> Bận việc
                            </span>
                          )}
                        </td>
                        <td className="py-2.5 px-3 text-center font-bold text-slate-700">
                          {rsvp.companionCount > 0 ? `+${rsvp.companionCount}` : 'Đi 1 mình'}
                        </td>
                        <td className="py-2.5 px-3 text-slate-600 max-w-xs italic">
                          "{rsvp.wishes || 'Chúc hai bạn trăm năm hạnh phúc!'}"
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>

        {/* RIGHT COLUMN: LIVE PHONE SIMULATOR (col-span-5) */}
        <div className="lg:col-span-5 sticky top-20">
          <div className="text-center mb-3">
            <span className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center justify-center gap-1">
              📱 Mô Phỏng Giao Diện Khách Mở Thiệp (Guest View)
            </span>
          </div>

          {/* Phone Frame Simulator */}
          <div className="w-[340px] sm:w-[370px] h-[680px] mx-auto border-[10px] border-slate-900 rounded-[44px] shadow-2xl bg-white overflow-hidden flex flex-col relative ring-1 ring-slate-800">
            {/* Phone Notch */}
            <div className="w-32 h-4 bg-slate-900 mx-auto rounded-b-xl shrink-0 z-20" />

            {/* Phone Screen Scrollable Content */}
            <div className="flex-1 overflow-y-auto px-4 py-6 text-center bg-[#fffbf9]" style={{ fontFamily: "'Playfair Display', serif" }}>
              <div className="text-[10px] font-bold text-slate-400 tracking-[3px] uppercase">
                Save The Date
              </div>

              <div className="text-2xl text-rose-500 font-serif italic mt-1 font-bold">
                Happy Wedding
              </div>

              <h4 className="text-lg font-black text-slate-900 mt-1">
                {formData.groomName} & {formData.brideName}
              </h4>

              <div className="text-xs text-slate-500 font-sans tracking-widest mt-1 uppercase font-semibold">
                {new Date(formData.eventDate).toLocaleDateString('vi-VN')}
              </div>

              {/* Cover Image */}
              <div className="my-4 rounded-2xl overflow-hidden shadow-md border border-rose-100 h-44 bg-rose-50">
                <img
                  src={formData.coverImageUrl || 'https://images.unsplash.com/photo-1519741497674-611481863552?w=800'}
                  alt="Couple Cover"
                  className="w-full h-full object-cover"
                />
              </div>

              {/* Ceremony Box */}
              <div className="bg-white p-4 rounded-2xl shadow-xs border border-rose-100 text-left font-sans mb-3 text-xs">
                <div className="font-bold text-rose-600 uppercase text-[10px] tracking-wider mb-1">
                  Lễ Thành Hôn & Tiệc Cưới
                </div>
                <div className="font-bold text-slate-800 text-xs flex items-center gap-1">
                  <Calendar className="w-3.5 h-3.5 text-rose-500" />
                  {new Date(formData.eventDate).toLocaleString('vi-VN', {
                    hour: '2-digit', minute: '2-digit', weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric'
                  })}
                </div>
                <div className="font-bold text-slate-800 text-xs mt-1.5 flex items-center gap-1">
                  <MapPin className="w-3.5 h-3.5 text-rose-500" />
                  {formData.venueName}
                </div>
                <div className="text-[11px] text-slate-500 ml-4.5 mt-0.5">
                  {formData.venueAddress}
                </div>
              </div>

              {/* Love Story snippet */}
              {formData.loveStory && (
                <div className="bg-rose-50/60 p-3 rounded-2xl border border-rose-100/80 text-xs italic text-slate-700 font-serif mb-3 leading-relaxed">
                  "{formData.loveStory}"
                </div>
              )}

              {/* Simulated RSVP 1-Touch Box */}
              <div className="bg-white p-4 rounded-2xl shadow-xs border border-rose-200 text-left font-sans text-xs">
                <div className="font-black text-slate-900 text-xs text-center uppercase tracking-wide">
                  Xác Nhận Tham Dự (RSVP 1-Chạm)
                </div>
                <p className="text-[10px] text-slate-500 text-center mt-0.5">
                  Phản hồi để cô dâu & chú rể chuẩn bị chu đáo nhất
                </p>

                <div className="mt-2.5 space-y-1.5">
                  <input
                    disabled
                    placeholder="Họ và tên của bạn..."
                    className="w-full px-2.5 py-1.5 text-xs rounded-lg border border-slate-200 bg-slate-50"
                  />
                  <select disabled className="w-full px-2.5 py-1.5 text-xs rounded-lg border border-slate-200 bg-slate-50">
                    <option>Tôi sẽ tham dự (+0 người đi cùng)</option>
                  </select>
                </div>

                <div className="grid grid-cols-2 gap-2 mt-2.5">
                  <button disabled className="py-2 bg-emerald-600 text-white font-bold rounded-lg text-[11px] text-center">
                    ✓ Sẽ Tham Dự
                  </button>
                  <button disabled className="py-2 bg-slate-100 text-slate-600 font-semibold rounded-lg text-[11px] text-center">
                    Bận Việc
                  </button>
                </div>
              </div>

              <div className="mt-4 text-[10px] text-slate-400 font-sans flex items-center justify-center gap-1">
                <Heart className="w-3 h-3 text-rose-400 fill-current" />
                Được tạo bởi Wedding Platform
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default InvitationBuilder;
