import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import {
  Heart, Calendar, MapPin, ExternalLink,
  CheckCircle2, Sparkles, Send, Copy,
  Check, AlertCircle, Loader2
} from 'lucide-react';
import { usePublicInvitation, useSubmitRsvp } from '../../hooks/useInvitations';
import type { SubmitRsvpRequest } from '../../../../types/invitation.types';

export default function PublicInvitationPage() {
  const { slug = '' } = useParams<{ slug: string }>();
  const { data: invitation, isLoading, isError, error } = usePublicInvitation(slug);
  const submitRsvpMutation = useSubmitRsvp(slug);

  const [rsvpForm, setRsvpForm] = useState<SubmitRsvpRequest>({
    guestName: '',
    phoneNumber: '',
    status: 'Attending',
    companionCount: 0,
    wishes: '',
    dietaryPreference: '',
  });

  const [rsvpSuccessMessage, setRsvpSuccessMessage] = useState<string | null>(null);
  const [rsvpError, setRsvpError] = useState<string | null>(null);
  const [copiedBank, setCopiedBank] = useState(false);

  // Countdown timer
  const [timeLeft, setTimeLeft] = useState<{ days: number; hours: number; minutes: number; seconds: number }>({
    days: 0, hours: 0, minutes: 0, seconds: 0
  });

  useEffect(() => {
    if (!invitation?.eventDate) return;
    const calculateTime = () => {
      const diff = new Date(invitation.eventDate).getTime() - new Date().getTime();
      if (diff <= 0) {
        setTimeLeft({ days: 0, hours: 0, minutes: 0, seconds: 0 });
        return;
      }
      setTimeLeft({
        days: Math.floor(diff / (1000 * 60 * 60 * 24)),
        hours: Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60)),
        minutes: Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60)),
        seconds: Math.floor((diff % (1000 * 60)) / 1000)
      });
    };
    calculateTime();
    const timer = setInterval(calculateTime, 1000);
    return () => clearInterval(timer);
  }, [invitation?.eventDate]);

  const handleRsvpSubmit = async (status: 'Attending' | 'NotAttending') => {
    if (!rsvpForm.guestName.trim()) {
      setRsvpError('Vui lòng nhập họ và tên của bạn để cô dâu & chú rể ghi nhận nhé!');
      return;
    }

    setRsvpError(null);
    try {
      const res = await submitRsvpMutation.mutateAsync({
        ...rsvpForm,
        status,
      });
      setRsvpSuccessMessage(res.message);
    } catch (err: unknown) {
      const e = err as Error;
      setRsvpError(e.message || 'Không thể gửi phản hồi RSVP. Vui lòng thử lại sau.');
    }
  };

  const copyBankInfo = () => {
    if (invitation?.bankInfo) {
      navigator.clipboard.writeText(invitation.bankInfo);
      setCopiedBank(true);
      setTimeout(() => setCopiedBank(false), 2500);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf7f5] flex flex-col items-center justify-center p-6 text-slate-500">
        <Loader2 className="w-10 h-10 animate-spin text-rose-500 mb-3" />
        <p className="font-semibold text-sm font-serif">Đang mở thiệp cưới...</p>
      </div>
    );
  }

  if (isError || !invitation) {
    return (
      <div className="min-h-screen bg-[#faf7f5] flex flex-col items-center justify-center p-6 text-center">
        <div className="w-16 h-16 rounded-full bg-rose-100 text-rose-600 flex items-center justify-center mb-4">
          <Heart className="w-8 h-8" />
        </div>
        <h2 className="text-xl font-bold text-slate-800 font-serif">Thiệp cưới không tồn tại</h2>
        <p className="text-xs text-slate-500 mt-2 max-w-sm">
          {(error as Error)?.message || 'Liên kết thiệp mời có thể đã hết hạn hoặc bị thay đổi.'}
        </p>
        <Link to="/" className="mt-6 px-5 py-2.5 rounded-xl bg-rose-500 text-white text-xs font-bold hover:bg-rose-600">
          Về Trang Chủ
        </Link>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#fcfaf8] text-slate-800 flex flex-col items-center">
      {/* Container Mobile-First View (Tối ưu tuyệt đẹp trên mọi thiết bị di động & desktop) */}
      <div className="w-full max-w-md bg-white shadow-2xl min-h-screen border-x border-rose-100/60 pb-16 flex flex-col">
        {/* Top Floating Music / Brand Bar */}
        <div className="bg-white/80 backdrop-blur-md sticky top-0 z-30 px-5 py-3 border-b border-rose-100 flex items-center justify-between text-xs">
          <div className="flex items-center gap-1.5 font-bold text-rose-600">
            <Heart className="w-4 h-4 fill-current animate-pulse" />
            <span style={{ fontFamily: "'Playfair Display', serif" }}>Wedding Invitation</span>
          </div>
          <span className="text-[11px] text-slate-400">
            {new Date(invitation.eventDate).toLocaleDateString('vi-VN')}
          </span>
        </div>

        {/* Hero Section */}
        <div className="px-6 pt-8 pb-4 text-center">
          <span className="text-[11px] font-bold tracking-[4px] text-slate-400 uppercase">
            Save The Date
          </span>
          <div className="text-3xl text-rose-500 font-serif italic mt-1 font-bold">
            Happy Wedding
          </div>
          <h1 className="text-2xl font-black text-slate-900 mt-2 tracking-tight" style={{ fontFamily: "'Playfair Display', serif" }}>
            {invitation.groomName} <span className="text-rose-400 font-normal">&</span> {invitation.brideName}
          </h1>

          {/* Countdown Clock */}
          <div className="grid grid-cols-4 gap-2 my-5 max-w-xs mx-auto">
            <div className="bg-rose-50/70 p-2 rounded-xl border border-rose-100 text-center">
              <span className="block font-black text-lg text-rose-600 leading-none">{timeLeft.days}</span>
              <span className="text-[9px] font-bold text-slate-400 uppercase tracking-wider">Ngày</span>
            </div>
            <div className="bg-rose-50/70 p-2 rounded-xl border border-rose-100 text-center">
              <span className="block font-black text-lg text-rose-600 leading-none">{timeLeft.hours}</span>
              <span className="text-[9px] font-bold text-slate-400 uppercase tracking-wider">Giờ</span>
            </div>
            <div className="bg-rose-50/70 p-2 rounded-xl border border-rose-100 text-center">
              <span className="block font-black text-lg text-rose-600 leading-none">{timeLeft.minutes}</span>
              <span className="text-[9px] font-bold text-slate-400 uppercase tracking-wider">Phút</span>
            </div>
            <div className="bg-rose-50/70 p-2 rounded-xl border border-rose-100 text-center">
              <span className="block font-black text-lg text-rose-600 leading-none">{timeLeft.seconds}</span>
              <span className="text-[9px] font-bold text-slate-400 uppercase tracking-wider">Giây</span>
            </div>
          </div>
        </div>

        {/* Cover Photo */}
        <div className="px-6 mb-6">
          <div className="relative rounded-3xl overflow-hidden shadow-lg border-2 border-rose-100 aspect-[4/3] bg-rose-50">
            <img
              src={invitation.coverImageUrl || 'https://images.unsplash.com/photo-1519741497674-611481863552?w=800'}
              alt="Ảnh cưới"
              className="w-full h-full object-cover"
            />
            <div className="absolute inset-0 bg-gradient-to-t from-slate-900/60 via-transparent to-transparent flex items-end p-5">
              <p className="text-white text-xs italic font-serif leading-relaxed">
                "{invitation.loveStory || 'Hạnh phúc là hành trình chúng mình cùng nhau đi qua...'}"
              </p>
            </div>
          </div>
        </div>

        {/* Wedding Ceremony & Reception Info */}
        <div className="px-6 mb-6">
          <div className="bg-gradient-to-br from-rose-50/60 to-amber-50/40 rounded-3xl p-6 border border-rose-100 text-center space-y-3">
            <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white text-rose-600 text-[10px] font-extrabold uppercase tracking-wider shadow-xs">
              <Sparkles className="w-3 h-3" />
              Lễ Thành Hôn & Tiệc Cưới
            </div>

            <div className="font-bold text-base text-slate-900 flex items-center justify-center gap-1.5 pt-1">
              <Calendar className="w-4 h-4 text-rose-500" />
              <span>
                {new Date(invitation.eventDate).toLocaleDateString('vi-VN', {
                  weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric'
                })}
              </span>
            </div>

            <div className="text-xs font-bold text-rose-700">
              Vào lúc: {new Date(invitation.eventDate).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
            </div>

            <div className="pt-2 border-t border-rose-200/50">
              <div className="font-extrabold text-sm text-slate-900 flex items-center justify-center gap-1">
                <MapPin className="w-4 h-4 text-rose-500 shrink-0" />
                <span>{invitation.venueName}</span>
              </div>
              <p className="text-xs text-slate-600 mt-1 max-w-xs mx-auto">
                {invitation.venueAddress}
              </p>

              {invitation.mapUrl && (
                <a
                  href={invitation.mapUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-3 inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-white text-rose-600 border border-rose-200 text-xs font-bold hover:bg-rose-50 transition shadow-xs"
                >
                  <ExternalLink className="w-3.5 h-3.5" />
                  Mở Bản Đồ Chỉ Đường (Google Maps)
                </a>
              )}
            </div>
          </div>
        </div>

        {/* Online Gift Box (Hộp Mừng Cưới) */}
        {invitation.bankInfo && (
          <div className="px-6 mb-6">
            <div className="bg-white rounded-3xl p-5 border border-slate-200 shadow-xs text-center space-y-2">
              <div className="text-[10px] font-extrabold uppercase tracking-wider text-slate-400">
                Hộp Mừng Cưới Online
              </div>
              <div className="font-mono text-xs font-bold text-slate-800 bg-slate-50 p-2.5 rounded-xl border border-slate-200 flex items-center justify-between">
                <span className="truncate">{invitation.bankInfo}</span>
                <button
                  onClick={copyBankInfo}
                  className="ml-2 text-rose-600 hover:text-rose-700 p-1 shrink-0"
                  title="Sao chép"
                >
                  {copiedBank ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4" />}
                </button>
              </div>
              <p className="text-[10px] text-slate-400">
                Dành cho bạn bè, người thân ở xa muốn gửi lời chúc & quà mừng
              </p>
            </div>
          </div>
        )}

        {/* RSVP 1-Touch Form */}
        <div className="px-6 mb-6">
          <div className="bg-white rounded-3xl p-6 border-2 border-rose-200 shadow-md">
            <div className="text-center mb-4">
              <h3 className="font-extrabold text-base text-slate-900 tracking-tight" style={{ fontFamily: "'Playfair Display', serif" }}>
                Xác Nhận Tham Dự (RSVP 1-Chạm)
              </h3>
              <p className="text-[11px] text-slate-500 mt-1">
                Sự hiện diện của bạn là niềm vinh hạnh to lớn cho chúng mình!
              </p>
            </div>

            {rsvpSuccessMessage ? (
              <div className="py-6 text-center animate-in zoom-in-95">
                <div className="w-14 h-14 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mx-auto mb-3">
                  <CheckCircle2 className="w-8 h-8" />
                </div>
                <h4 className="font-bold text-sm text-slate-900">Gửi Phản Hồi Thành Công!</h4>
                <p className="text-xs text-slate-600 mt-1 leading-relaxed">
                  {rsvpSuccessMessage}
                </p>
                <button
                  onClick={() => setRsvpSuccessMessage(null)}
                  className="mt-4 text-xs font-bold text-rose-600 hover:underline"
                >
                  Gửi thêm phản hồi khác
                </button>
              </div>
            ) : (
              <div className="space-y-3 text-xs">
                {rsvpError && (
                  <div className="p-2.5 bg-red-50 text-red-700 rounded-xl flex items-center gap-1.5 border border-red-200 text-[11px]">
                    <AlertCircle className="w-4 h-4 shrink-0" />
                    <span>{rsvpError}</span>
                  </div>
                )}

                <div>
                  <label className="block font-bold text-slate-700 mb-1">
                    Họ và tên của bạn: <span className="text-rose-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={rsvpForm.guestName}
                    onChange={(e) => setRsvpForm(f => ({ ...f, guestName: e.target.value }))}
                    placeholder="Nhập tên của bạn..."
                    className="w-full px-3 py-2 rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500 text-xs"
                  />
                </div>

                <div>
                  <label className="block font-bold text-slate-700 mb-1">Số lượng người tham dự:</label>
                  <select
                    value={rsvpForm.companionCount}
                    onChange={(e) => setRsvpForm(f => ({ ...f, companionCount: Number(e.target.value) }))}
                    className="w-full px-3 py-2 rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500 text-xs bg-white"
                  >
                    <option value={0}>Đi 1 mình (+0 người đi cùng)</option>
                    <option value={1}>Đi cùng 1 người (+1)</option>
                    <option value={2}>Đi cùng gia đình (+2 người)</option>
                    <option value={3}>Đi cùng gia đình (+3 người trở lên)</option>
                  </select>
                </div>

                <div>
                  <label className="block font-bold text-slate-700 mb-1">Lời chúc gửi Dâu - Rể:</label>
                  <textarea
                    value={rsvpForm.wishes}
                    onChange={(e) => setRsvpForm(f => ({ ...f, wishes: e.target.value }))}
                    placeholder="Gửi lời chúc trăm năm hạnh phúc..."
                    rows={2}
                    className="w-full px-3 py-2 rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500 text-xs"
                  />
                </div>

                <div className="grid grid-cols-2 gap-2 pt-2">
                  <button
                    type="button"
                    onClick={() => handleRsvpSubmit('Attending')}
                    disabled={submitRsvpMutation.isPending}
                    className="py-2.5 px-3 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-bold rounded-xl shadow-sm text-xs flex items-center justify-center gap-1 transition disabled:opacity-50"
                  >
                    {submitRsvpMutation.isPending ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />}
                    <span>✓ Sẽ Tham Dự</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => handleRsvpSubmit('NotAttending')}
                    disabled={submitRsvpMutation.isPending}
                    className="py-2.5 px-3 bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold rounded-xl text-xs flex items-center justify-center gap-1 transition disabled:opacity-50"
                  >
                    <span>Rất Tiếc Bận Việc</span>
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Recent Wishes Wall */}
        {invitation.recentWishes && invitation.recentWishes.length > 0 && (
          <div className="px-6 mb-6">
            <div className="bg-slate-50/80 rounded-3xl p-5 border border-slate-200/80 space-y-3">
              <h4 className="font-extrabold text-xs text-slate-800 uppercase tracking-wider flex items-center gap-1.5">
                <Send className="w-3.5 h-3.5 text-rose-500" />
                Sổ Lưu Bút Lời Chúc ({invitation.recentWishes.length})
              </h4>
              <div className="space-y-2.5 max-h-60 overflow-y-auto pr-1">
                {invitation.recentWishes.map((w, idx) => (
                  <div key={idx} className="bg-white p-3 rounded-2xl border border-slate-100 shadow-xs text-xs space-y-1">
                    <div className="flex items-center justify-between">
                      <strong className="text-slate-900">{w.guestName}</strong>
                      <span className="text-[10px] text-slate-400">
                        {new Date(w.createdAt).toLocaleDateString('vi-VN')}
                      </span>
                    </div>
                    <p className="text-slate-600 italic">"{w.wishes}"</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Footer Credit */}
        <div className="mt-auto px-6 text-center text-xs text-slate-400 space-y-1">
          <p className="flex items-center justify-center gap-1">
            Thiết kế bằng <Heart className="w-3.5 h-3.5 text-rose-500 fill-current" /> trên Wedding Platform
          </p>
        </div>
      </div>
    </div>
  );
}
