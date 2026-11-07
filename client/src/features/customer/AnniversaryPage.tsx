import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import {
  Calendar,
  Heart,
  Sparkles,
  Gift,
  Clock,
  BookOpen,
  Plus,
  Trash2,
  Award,
  ChevronRight,
  Flame,
  Wine,
} from "lucide-react";
import { useAuthStore } from "../../store/authStore";

interface AnniversaryMilestone {
  year: number;
  name: string;
  vietnameseName: string;
  symbol: string;
  meaning: string;
  giftIdeas: string[];
  romanticIdeas: string[];
  color: string;
}

const MILESTONES: AnniversaryMilestone[] = [
  {
    year: 1,
    name: "Paper Wedding",
    vietnameseName: "Đám Cưới Giấy",
    symbol: "📜",
    meaning: "Năm đầu tiên với tình yêu trong sáng, thuần khiết như trang giấy trắng, sẵn sàng cùng nhau viết nên trang sử gia đình.",
    giftIdeas: ["Cuốn sổ tay kỷ niệm viết tay", "Cặp vé máy bay du lịch đôi", "Album ảnh in năm đầu chung sống"],
    romanticIdeas: ["Ăn tối nến và xem lại video ngày cưới", "Mở bức thư tình viết cho nhau trong ngày cưới"],
    color: "from-amber-500/10 to-orange-500/10 text-amber-700 border-amber-200",
  },
  {
    year: 2,
    name: "Cotton Wedding",
    vietnameseName: "Đám Cưới Bông",
    symbol: "☁️",
    meaning: "Sợi bông mềm mại đan xen chặt chẽ, thể hiện sự thấu hiểu, gắn bó và cùng nhau hòa hợp qua từng thói quen nhỏ.",
    giftIdeas: ["Bộ drap gối lụa cotton cao cấp", "Áo đôi thiết kế thêu tên hai bạn", "Khăn choàng ấm áp"],
    romanticIdeas: ["Cùng nhau nấu một bữa ăn ấm cúng tại nhà", "Massage thư giãn đôi tại spa"],
    color: "from-sky-500/10 to-blue-500/10 text-sky-700 border-sky-200",
  },
  {
    year: 3,
    name: "Leather Wedding",
    vietnameseName: "Đám Cưới Da",
    symbol: "👞",
    meaning: "Chất liệu da bền bỉ, dẻo dai theo năm tháng — tượng trưng cho sự vững vàng sau những thử thách ban đầu của hôn nhân.",
    giftIdeas: ["Ví da đôi khắc tên hoặc ngày cưới", "Dây da đồng hồ cao cấp", "Túi xách du lịch da vintage"],
    romanticIdeas: ["Chuyến dã ngoại ngắn ngày ngắm hoàng hôn", "Học cùng nhau một workshop làm đồ da thủ công"],
    color: "from-amber-700/10 to-yellow-800/10 text-amber-900 border-amber-300",
  },
  {
    year: 5,
    name: "Wooden Wedding",
    vietnameseName: "Đám Cưới Gỗ",
    symbol: "🌳",
    meaning: "Vững chãi như cây cổ thụ đâm sâu rễ vào lòng đất. Hôn nhân 5 năm đã có nền tảng gia đình kiên cố và ấm áp.",
    giftIdeas: ["Khung ảnh gỗ khắc lời chúc đôi", "Bộ bàn ăn gỗ mới cho tổ ấm", "Chuyến du lịch cabin gỗ trên núi"],
    romanticIdeas: ["Cùng trồng một cái cây tình yêu trong sân nhà", "Bữa tối bên lò sưởi lãng mạn"],
    color: "from-emerald-500/10 to-green-600/10 text-emerald-800 border-emerald-200",
  },
  {
    year: 10,
    name: "Tin / Aluminum Wedding",
    vietnameseName: "Đám Cưới Thiếc",
    symbol: "🥈",
    meaning: "Sáng bóng, kiên cường và không bao giờ hoen gỉ. Một thập kỷ trọn vẹn bên nhau qua bao thăng trầm.",
    giftIdeas: ["Trang sức bạc / bạch kim tinh xảo", "Đồng hồ đôi sang trọng", "Chuyến trăng mật thứ hai (Second Honeymoon)"],
    romanticIdeas: ["Tổ chức một buổi tiệc ấm cúng cùng những người bạn thân nhất", "Cùng nhau viết lại lời thề nguyện hôn nhân (Vow Renewal)"],
    color: "from-slate-400/10 to-zinc-500/10 text-slate-800 border-slate-300",
  },
  {
    year: 15,
    name: "Crystal Wedding",
    vietnameseName: "Đám Cưới Thủy Tinh",
    symbol: "🔮",
    meaning: "Trong suốt, lấp lánh và đáng nâng niu. 15 năm gắn bó chứng minh sự chân thành và thấu hiểu đến từng hơi thở.",
    giftIdeas: ["Bộ ly pha lê uống rượu vang cao cấp", "Bình hoa pha lê nghệ thuật", "Dây chuyền đá quý"],
    romanticIdeas: ["Bữa tối nến sang trọng trên du thuyền ven sông", "Đi du lịch biển đêm ngắm sao trời"],
    color: "from-cyan-500/10 to-blue-500/10 text-cyan-800 border-cyan-200",
  },
  {
    year: 20,
    name: "China Wedding",
    vietnameseName: "Đám Cưới Sứ",
    symbol: "🏺",
    meaning: "Thanh nhã, quý giá và trường tồn. Trải qua 20 năm, tình yêu trở thành một tác phẩm nghệ thuật gia đình hoàn hảo.",
    giftIdeas: ["Bộ ấm chén gốm sứ hoàng gia", "Tượng nghệ thuật gia đình", "Kỳ nghỉ dưỡng dài ngày tại resort cao cấp"],
    romanticIdeas: ["Thăm lại nơi hai bạn lần đầu gặp nhau cách đây 20 năm", "Bộ ảnh gia đình hoành tráng đầy đủ con cái"],
    color: "from-rose-500/10 to-pink-500/10 text-rose-800 border-rose-200",
  },
  {
    year: 25,
    name: "Silver Jubilee",
    vietnameseName: "Đám Cưới Bạc",
    symbol: "✨",
    meaning: "Một phần tư thế kỷ son sắt thủy chung. Cột mốc vinh quang của một tình yêu vượt qua mọi giông bão cuộc đời.",
    giftIdeas: ["Cặp nhẫn bạc/bạch kim khắc kỷ niệm 25 năm", "Chuyến du lịch châu Âu mơ ước", "Album 25 năm hành trình cuộc đời"],
    romanticIdeas: ["Tổ chức Lễ Kỷ Niệm Bạc với sự chúc phúc của gia đình hai họ", "Khiêu vũ điệu waltz dưới ánh đèn"],
    color: "from-gray-300/20 to-slate-400/20 text-slate-900 border-slate-400",
  },
  {
    year: 50,
    name: "Golden Jubilee",
    vietnameseName: "Đám Cưới Vàng",
    symbol: "👑",
    meaning: "Nửa thế kỷ yêu thương trường tồn bất diệt. Biểu tượng tối cao của lòng chung thủy và phúc đức gia đình muôn đời.",
    giftIdeas: ["Cặp nhẫn vàng nguyên chất", "Bức tranh gia phả thêu chữ vàng", "Tiệc đại thọ chúc phúc từ con cháu"],
    romanticIdeas: ["Cùng nhau nhìn lại đàn con cháu sum vầy", "Cầm tay nhau đi dạo buổi sớm mai"],
    color: "from-amber-400/20 to-yellow-500/20 text-amber-900 border-amber-400",
  },
];

interface MemoryEntry {
  id: string;
  yearMilestone: number;
  date: string;
  title: string;
  story: string;
  location?: string;
}

export default function AnniversaryPage() {
  const { user } = useAuthStore();

  // Local state for couple details
  const [partnerNames, setPartnerNames] = useState(() => {
    return localStorage.getItem("wm_anniversary_names") || (user?.fullName ? `${user.fullName} & Bạn đời` : "Chú rể & Cô dâu");
  });
  const [weddingDateStr, setWeddingDateStr] = useState(() => {
    return localStorage.getItem("wm_anniversary_wedding_date") || "2024-11-04";
  });
  const [datingDateStr, setDatingDateStr] = useState(() => {
    return localStorage.getItem("wm_anniversary_dating_date") || "2022-02-14";
  });

  const [memories, setMemories] = useState<MemoryEntry[]>(() => {
    const saved = localStorage.getItem("wm_anniversary_memories");
    if (saved) {
      try {
        return JSON.parse(saved);
      } catch {
        return [];
      }
    }
    return [
      {
        id: "demo-1",
        yearMilestone: 1,
        date: "2025-11-04",
        title: "Kỷ niệm Đám Cưới Giấy - Bữa tối lãng mạn dưới ánh nến",
        story: "Một năm trôi qua nhanh như một cái chớp mắt. Chúng mình đã cùng nhau trang hoàng tổ ấm đầu tiên và xem lại video cưới trong xúc động.",
        location: "Hà Nội",
      },
    ];
  });

  // Modal new memory
  const [isAddingMemory, setIsAddingMemory] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [newYear, setNewYear] = useState(1);
  const [newDate, setNewDate] = useState(new Date().toISOString().slice(0, 10));
  const [newStory, setNewStory] = useState("");
  const [newLocation, setNewLocation] = useState("");

  const [activeTab, setActiveTab] = useState<"counter" | "milestones" | "ideas" | "journal">("counter");

  // Save changes
  useEffect(() => {
    localStorage.setItem("wm_anniversary_names", partnerNames);
    localStorage.setItem("wm_anniversary_wedding_date", weddingDateStr);
    localStorage.setItem("wm_anniversary_dating_date", datingDateStr);
  }, [partnerNames, weddingDateStr, datingDateStr]);

  useEffect(() => {
    localStorage.setItem("wm_anniversary_memories", JSON.stringify(memories));
  }, [memories]);

  // Calculations
  const today = new Date();
  const weddingDate = new Date(weddingDateStr);
  const datingDate = new Date(datingDateStr);

  // Love days calculation
  const diffLoveMs = today.getTime() - datingDate.getTime();
  const daysDating = Math.max(0, Math.floor(diffLoveMs / (1000 * 60 * 60 * 24)));

  // Wedding days calculation
  const diffWeddingMs = today.getTime() - weddingDate.getTime();
  const daysMarried = Math.floor(diffWeddingMs / (1000 * 60 * 60 * 24));
  const isMarried = daysMarried >= 0;

  // Years married
  const yearsMarried = isMarried ? Math.floor(daysMarried / 365) : 0;

  // Next anniversary calculation
  let nextAnniversary = new Date(today.getFullYear(), weddingDate.getMonth(), weddingDate.getDate());
  if (nextAnniversary.getTime() < today.getTime()) {
    nextAnniversary = new Date(today.getFullYear() + 1, weddingDate.getMonth(), weddingDate.getDate());
  }
  const diffNextMs = nextAnniversary.getTime() - today.getTime();
  const daysUntilNext = Math.ceil(diffNextMs / (1000 * 60 * 60 * 24));
  const nextMilestoneNumber = isMarried ? yearsMarried + 1 : 1;

  // Closest traditional milestone
  const currentMilestone = MILESTONES.find((m) => m.year === (yearsMarried || 1)) || MILESTONES[0];

  const handleAddMemory = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim()) return;

    const entry: MemoryEntry = {
      id: Date.now().toString(),
      yearMilestone: newYear,
      date: newDate,
      title: newTitle.trim(),
      story: newStory.trim(),
      location: newLocation.trim() || undefined,
    };

    setMemories([entry, ...memories]);
    setNewTitle("");
    setNewStory("");
    setNewLocation("");
    setIsAddingMemory(false);
  };

  const handleDeleteMemory = (id: string) => {
    setMemories(memories.filter((m) => m.id !== id));
  };

  return (
    <main className="min-h-screen bg-[#faf8f5] py-8 sm:py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-5xl mx-auto space-y-8">
        {/* Navigation Breadcrumb */}
        <div className="flex items-center justify-between">
          <Link
            to="/customer/dashboard"
            className="text-xs sm:text-sm font-semibold text-slate-600 hover:text-rose-600 transition flex items-center gap-1.5"
          >
            ← Về không gian kế hoạch
          </Link>
          <div className="flex items-center gap-2">
            <Link
              to="/chibi-wedding"
              className="text-xs font-bold text-rose-600 bg-rose-50 hover:bg-rose-100 px-3 py-1.5 rounded-xl border border-rose-200 transition flex items-center gap-1.5"
            >
              ✨ Chibi Wedding 3D
            </Link>
          </div>
        </div>

        {/* Hero Banner */}
        <div className="relative overflow-hidden rounded-3xl bg-gradient-to-br from-rose-500 via-pink-500 to-amber-500 p-8 sm:p-12 text-white shadow-xl">
          <div className="relative z-10 max-w-2xl space-y-4">
            <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-white/20 backdrop-blur-md text-xs font-bold uppercase tracking-wider">
              <Sparkles size={14} className="text-amber-200" />
              <span>Gìn giữ ngọn lửa tình yêu theo năm tháng</span>
            </div>
            <h1
              className="text-3xl sm:text-5xl font-bold leading-tight drop-shadow-xs"
              style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
            >
              Kỷ Niệm Ngày Cưới
            </h1>
            <p className="text-white/90 text-sm sm:text-base leading-relaxed">
              Mỗi năm trôi qua là một cột mốc quý báu ghi dấu sự đồng hành son sắt của{" "}
              <strong className="underline underline-offset-4 decoration-white/40">{partnerNames}</strong>.
              Cùng đếm ngược ngày kỷ niệm, khám phá ý nghĩa các mốc đám cưới và lưu giữ khoảnh khắc ngọt ngào.
            </p>
          </div>

          <div className="absolute right-[-40px] bottom-[-40px] opacity-15 pointer-events-none">
            <Heart size={320} fill="currentColor" />
          </div>
        </div>

        {/* Tab Navigation */}
        <div className="flex flex-wrap gap-2 border-b border-slate-200 pb-2">
          {[
            { id: "counter", label: "Đếm Ngày Yêu & Cưới", icon: Flame },
            { id: "milestones", label: "Cột Mốc Truyền Thống (1-50 Năm)", icon: Award },
            { id: "ideas", label: "Gợi Ý Kỷ Niệm Lãng Mạn", icon: Wine },
            { id: "journal", label: "Nhật Ký Tình Yêu", icon: BookOpen },
          ].map((tab) => {
            const Icon = tab.icon;
            const isCurrent = activeTab === tab.id;
            return (
              <button
                type="button"
                key={tab.id}
                onClick={() => setActiveTab(tab.id as any)}
                className={`px-4 py-2.5 rounded-2xl text-xs sm:text-sm font-bold transition flex items-center gap-2 cursor-pointer ${
                  isCurrent
                    ? "bg-rose-500 text-white shadow-md shadow-rose-200"
                    : "bg-white text-slate-600 hover:bg-slate-100 border border-slate-200/80"
                }`}
              >
                <Icon size={16} />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* TAB 1: LOVE & WEDDING COUNTER */}
        {activeTab === "counter" && (
          <div className="space-y-6">
            {/* Quick config couple details */}
            <div className="bg-white rounded-3xl p-6 border border-slate-200/80 shadow-xs space-y-4">
              <h3 className="text-base font-bold text-slate-800 flex items-center gap-2">
                <Calendar size={18} className="text-rose-500" />
                <span>Thiết lập thông tin ngày yêu &amp; ngày cưới</span>
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs">
                <div>
                  <label className="block font-semibold text-slate-600 mb-1">Tên hai bạn:</label>
                  <input
                    type="text"
                    value={partnerNames}
                    onChange={(e) => setPartnerNames(e.target.value)}
                    placeholder="VD: Minh Nhật & Khánh Linh"
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-rose-400"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-600 mb-1">Ngày bắt đầu yêu:</label>
                  <input
                    type="date"
                    value={datingDateStr}
                    onChange={(e) => setDatingDateStr(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-rose-400"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-600 mb-1">Ngày cưới chính thức:</label>
                  <input
                    type="date"
                    value={weddingDateStr}
                    onChange={(e) => setWeddingDateStr(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-rose-400 font-bold text-rose-600"
                  />
                </div>
              </div>
            </div>

            {/* Counters Grid */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {/* Box 1: Days Married */}
              <div className="bg-white rounded-3xl p-6 border border-rose-100 shadow-sm text-center relative overflow-hidden flex flex-col justify-between">
                <div className="w-12 h-12 rounded-2xl bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
                  <Heart size={24} className="fill-rose-500" />
                </div>
                <div>
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                    Thời Gian Chung Một Nhà
                  </span>
                  <div
                    className="text-4xl sm:text-5xl font-black text-rose-600 my-2"
                    style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                  >
                    {isMarried ? daysMarried : 0}
                  </div>
                  <p className="text-xs text-slate-500 font-semibold">
                    {isMarried
                      ? `${yearsMarried} năm ${Math.floor((daysMarried % 365) / 30)} tháng bên nhau`
                      : `Sắp tới ngày cưới chính thức!`}
                  </p>
                </div>
                <div className="mt-4 pt-4 border-t border-slate-100 text-[11px] text-slate-400">
                  Ngày cưới: {weddingDate.toLocaleDateString("vi-VN")}
                </div>
              </div>

              {/* Box 2: Countdown to Next Anniversary */}
              <div className="bg-gradient-to-br from-amber-500 via-rose-500 to-pink-500 rounded-3xl p-6 text-white shadow-md text-center flex flex-col justify-between">
                <div className="w-12 h-12 rounded-2xl bg-white/20 text-white flex items-center justify-center mx-auto mb-3">
                  <Clock size={24} />
                </div>
                <div>
                  <span className="text-xs font-bold uppercase tracking-wider text-amber-200">
                    Đếm Ngược Kỷ Niệm Lần Thứ {nextMilestoneNumber}
                  </span>
                  <div
                    className="text-4xl sm:text-5xl font-black text-white my-2"
                    style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                  >
                    {daysUntilNext}
                  </div>
                  <p className="text-xs text-white/90 font-semibold">
                    ngày nữa là tới ngày kỷ niệm!
                  </p>
                </div>
                <div className="mt-4 pt-4 border-t border-white/20 text-[11px] text-white/80">
                  {nextAnniversary.toLocaleDateString("vi-VN")}
                </div>
              </div>

              {/* Box 3: Love Days */}
              <div className="bg-white rounded-3xl p-6 border border-sky-100 shadow-sm text-center flex flex-col justify-between">
                <div className="w-12 h-12 rounded-2xl bg-sky-50 text-sky-500 flex items-center justify-center mx-auto mb-3">
                  <Flame size={24} />
                </div>
                <div>
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                    Tổng Số Ngày Yêu Nhau
                  </span>
                  <div
                    className="text-4xl sm:text-5xl font-black text-sky-600 my-2"
                    style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                  >
                    {daysDating}
                  </div>
                  <p className="text-xs text-slate-500 font-semibold">
                    từ ngày đầu tiên hai bạn hẹn hò
                  </p>
                </div>
                <div className="mt-4 pt-4 border-t border-slate-100 text-[11px] text-slate-400">
                  Từ ngày {datingDate.toLocaleDateString("vi-VN")}
                </div>
              </div>
            </div>

            {/* Current Milestone Spotlight Card */}
            <div className="bg-white rounded-3xl p-7 border border-slate-200/80 shadow-xs space-y-4">
              <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 pb-3">
                <div className="flex items-center gap-2">
                  <span className="text-2xl">{currentMilestone.symbol}</span>
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-rose-500">
                      Cột Mốc Hiện Tại · Năm Thứ {currentMilestone.year}
                    </span>
                    <h3
                      className="text-xl font-bold text-slate-900"
                      style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                    >
                      {currentMilestone.vietnameseName} ({currentMilestone.name})
                    </h3>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setActiveTab("milestones")}
                  className="text-xs font-bold text-rose-600 hover:text-rose-700 flex items-center gap-1 cursor-pointer"
                >
                  Xem tất cả các mốc 1-50 năm <ChevronRight size={14} />
                </button>
              </div>

              <p className="text-sm text-slate-600 leading-relaxed">
                {currentMilestone.meaning}
              </p>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                <div className="p-4 rounded-2xl bg-amber-50/60 border border-amber-200/70">
                  <p className="text-xs font-bold text-amber-900 mb-2 flex items-center gap-1.5">
                    <Gift size={14} /> Quà tặng ý nghĩa cho năm này:
                  </p>
                  <ul className="text-xs text-amber-800 space-y-1">
                    {currentMilestone.giftIdeas.map((idea, idx) => (
                      <li key={idx}>• {idea}</li>
                    ))}
                  </ul>
                </div>

                <div className="p-4 rounded-2xl bg-rose-50/60 border border-rose-200/70">
                  <p className="text-xs font-bold text-rose-900 mb-2 flex items-center gap-1.5">
                    <Wine size={14} /> Ý tưởng hẹn hò kỷ niệm:
                  </p>
                  <ul className="text-xs text-rose-800 space-y-1">
                    {currentMilestone.romanticIdeas.map((idea, idx) => (
                      <li key={idx}>• {idea}</li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 2: MILESTONES GUIDE */}
        {activeTab === "milestones" && (
          <div className="space-y-6">
            <div className="bg-white rounded-3xl p-6 border border-slate-200/80 shadow-xs">
              <h3
                className="text-2xl font-bold text-slate-900 mb-2"
                style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
              >
                Cẩm Nang Ý Nghĩa Kỷ Niệm Ngày Cưới Truyền Thống
              </h3>
              <p className="text-xs sm:text-sm text-slate-500 leading-relaxed">
                Theo phong tục quốc tế và văn hóa cưới hiện đại, mỗi năm kỷ niệm đều gắn liền với một chất liệu biểu trưng — từ mộc mạc tinh khôi như giấy, bông đến quý giá trường tồn như bạc, vàng và kim cương.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {MILESTONES.map((m) => (
                <div
                  key={m.year}
                  className={`bg-white rounded-3xl p-5 border shadow-2xs hover:shadow-xs transition space-y-3 ${
                    yearsMarried === m.year ? "ring-2 ring-rose-400 border-rose-400" : "border-slate-200/80"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <span className="text-3xl p-2 rounded-2xl bg-slate-50 border border-slate-100">
                        {m.symbol}
                      </span>
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="px-2 py-0.5 rounded-full bg-rose-50 text-rose-600 text-[10px] font-bold">
                            Năm thứ {m.year}
                          </span>
                          {yearsMarried === m.year && (
                            <span className="px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-600 text-[10px] font-bold">
                              ★ Cột mốc hiện tại
                            </span>
                          )}
                        </div>
                        <h4 className="font-bold text-slate-900 text-base">{m.vietnameseName}</h4>
                        <span className="text-[11px] text-slate-400 font-medium">{m.name}</span>
                      </div>
                    </div>
                  </div>

                  <p className="text-xs text-slate-600 leading-relaxed">{m.meaning}</p>

                  <div className="pt-2 border-t border-slate-100 text-[11px] space-y-1">
                    <p className="text-rose-700 font-semibold flex items-center gap-1">
                      <Gift size={12} /> Quà gợi ý: {m.giftIdeas.join(", ")}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* TAB 3: ROMANTIC IDEAS */}
        {activeTab === "ideas" && (
          <div className="space-y-6">
            <div className="bg-white rounded-3xl p-6 border border-slate-200/80 shadow-xs">
              <h3
                className="text-2xl font-bold text-slate-900 mb-2"
                style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
              >
                Gợi Ý Hoạt Động Hẹn Hò &amp; Kỷ Niệm Thăng Hoa
              </h3>
              <p className="text-xs sm:text-sm text-slate-500 leading-relaxed">
                Đừng để ngày kỷ niệm trôi qua như một ngày bình thường. Dưới đây là những ý tưởng lãng mạn giúp hâm nóng tình cảm vợ chồng:
              </p>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
              {[
                {
                  title: "1. Tái hiện lại buổi hẹn hò đầu tiên",
                  desc: "Quay lại quán cà phê hoặc nhà hàng nơi hai bạn lần đầu gặp gỡ. Mặc bộ trang phục tương tự và ôn lại những rung động bỡ ngỡ ban đầu.",
                  tag: "Hoài niệm ngọt ngào",
                  icon: "☕",
                },
                {
                  title: "2. Chụp bộ ảnh kỷ niệm thường niên",
                  desc: "Mỗi năm một bức ảnh chung ở cùng một góc quen hoặc một studio nhỏ. Sau 10-20 năm, hai bạn sẽ có cả một thước phim thời gian tuyệt đẹp.",
                  tag: "Lưu giữ thanh xuân",
                  icon: "📸",
                },
                {
                  title: "3. 'Bức thư tình gửi tương lai'",
                  desc: "Mỗi người tự viết một lá thư bày tỏ lòng biết ơn và cảm xúc sau một năm chung sống. Niêm phong lại và hẹn mở ra vào ngày kỷ niệm năm sau.",
                  tag: "Gắn kết tâm hồn",
                  icon: "💌",
                },
                {
                  title: "4. Chuyến du lịch 'Chỉ hai chúng mình'",
                  desc: "Gửi con cho ông bà trong 2 ngày cuối tuần để cùng nhau đi trốn ở một resort yên bình, tận hưởng không gian riêng tư trọn vẹn.",
                  tag: "F5 tình yêu",
                  icon: "✈️",
                },
                {
                  title: "5. Nấu ăn dưới ánh nến tại nhà",
                  desc: "Cùng nhau vào bếp chuẩn bị món ăn cả hai yêu thích, bật bản nhạc không lời du dương và nâng ly chúc mừng chặng đường đã qua.",
                  tag: "Ấm cúng & Gần gũi",
                  icon: "🍷",
                },
                {
                  title: "6. Trao nhau một món quà bất ngờ",
                  desc: "Không cần quá đắt đỏ, một món đồ người ấy từng vô tình khen thích hay một món đồ thủ công tự tay làm đều mang ý nghĩa vô giá.",
                  tag: "Bất ngờ tinh tế",
                  icon: "🎁",
                },
              ].map((item, idx) => (
                <div
                  key={idx}
                  className="bg-white rounded-3xl p-6 border border-slate-200/80 shadow-2xs hover:shadow-xs transition space-y-3"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-3xl">{item.icon}</span>
                    <span className="px-2.5 py-1 rounded-full bg-rose-50 text-rose-600 text-[10px] font-bold">
                      {item.tag}
                    </span>
                  </div>
                  <h4 className="font-bold text-slate-900 text-base">{item.title}</h4>
                  <p className="text-xs text-slate-600 leading-relaxed">{item.desc}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* TAB 4: MEMORY JOURNAL */}
        {activeTab === "journal" && (
          <div className="space-y-6">
            <div className="flex flex-wrap items-center justify-between gap-4 bg-white rounded-3xl p-6 border border-slate-200/80 shadow-xs">
              <div>
                <h3
                  className="text-2xl font-bold text-slate-900"
                  style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                >
                  Nhật Ký Kỷ Niệm Tình Yêu
                </h3>
                <p className="text-xs sm:text-sm text-slate-500 mt-1">
                  Lưu lại những kỷ niệm và cảm xúc của từng năm kỷ niệm ngày cưới để ngắm nhìn lại hành trình trưởng thành bên nhau.
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsAddingMemory(true)}
                className="py-2.5 px-4 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-bold text-xs shadow-md shadow-rose-200 transition cursor-pointer flex items-center gap-1.5"
              >
                <Plus size={16} /> Thêm kỷ niệm mới
              </button>
            </div>

            {/* Modal Add Memory */}
            {isAddingMemory && (
              <form
                onSubmit={handleAddMemory}
                className="bg-white rounded-3xl p-6 sm:p-7 border border-rose-200 shadow-md space-y-4 animate-in fade-in"
              >
                <div className="flex justify-between items-center border-b border-slate-100 pb-3">
                  <h4 className="font-bold text-slate-900 text-base">Thêm Kỷ Niệm Mới</h4>
                  <button
                    type="button"
                    onClick={() => setIsAddingMemory(false)}
                    className="text-slate-400 hover:text-slate-600 cursor-pointer"
                  >
                    Đóng
                  </button>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
                  <div>
                    <label className="block font-semibold text-slate-600 mb-1">Mốc kỷ niệm:</label>
                    <select
                      value={newYear}
                      onChange={(e) => setNewYear(parseInt(e.target.value) || 1)}
                      className="w-full px-3 py-2 rounded-xl border border-slate-200 bg-white"
                    >
                      {MILESTONES.map((m) => (
                        <option key={m.year} value={m.year}>
                          Năm thứ {m.year} - {m.vietnameseName}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block font-semibold text-slate-600 mb-1">Ngày kỷ niệm:</label>
                    <input
                      type="date"
                      value={newDate}
                      onChange={(e) => setNewDate(e.target.value)}
                      className="w-full px-3 py-2 rounded-xl border border-slate-200"
                    />
                  </div>

                  <div>
                    <label className="block font-semibold text-slate-600 mb-1">Địa điểm kỷ niệm:</label>
                    <input
                      type="text"
                      placeholder="VD: Đà Lạt / Nhà hàng The Log..."
                      value={newLocation}
                      onChange={(e) => setNewLocation(e.target.value)}
                      className="w-full px-3 py-2 rounded-xl border border-slate-200"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-600 mb-1">Tiêu đề khoảnh khắc:</label>
                  <input
                    type="text"
                    placeholder="VD: Kỷ niệm Đám cưới Bông tại Đà Lạt..."
                    value={newTitle}
                    onChange={(e) => setNewTitle(e.target.value)}
                    required
                    className="w-full px-3.5 py-2 text-sm rounded-xl border border-slate-200 font-bold"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-600 mb-1">Câu chuyện &amp; Cảm xúc:</label>
                  <textarea
                    rows={3}
                    placeholder="Chia sẻ những điều hai bạn đã cùng nhau trải qua trong ngày kỷ niệm này..."
                    value={newStory}
                    onChange={(e) => setNewStory(e.target.value)}
                    className="w-full px-3.5 py-2 text-xs rounded-xl border border-slate-200 resize-none"
                  />
                </div>

                <div className="flex justify-end gap-2 pt-2">
                  <button
                    type="button"
                    onClick={() => setIsAddingMemory(false)}
                    className="py-2 px-4 rounded-xl border border-slate-200 text-xs font-bold text-slate-600 hover:bg-slate-50 cursor-pointer"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    className="py-2 px-5 rounded-xl bg-rose-500 hover:bg-rose-600 text-white font-bold text-xs shadow-md transition cursor-pointer"
                  >
                    Lưu vào nhật ký
                  </button>
                </div>
              </form>
            )}

            {/* List memories */}
            {memories.length === 0 ? (
              <div className="bg-white rounded-3xl p-12 text-center border border-slate-200/80">
                <BookOpen size={40} className="text-slate-300 mx-auto mb-3" />
                <h4 className="font-bold text-slate-800 text-base">Chưa có nhật ký kỷ niệm nào</h4>
                <p className="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
                  Hãy bấm "Thêm kỷ niệm mới" để lưu lại khoảnh khắc ngọt ngào của hai bạn mỗi năm.
                </p>
              </div>
            ) : (
              <div className="space-y-4">
                {memories.map((entry) => (
                  <article
                    key={entry.id}
                    className="bg-white rounded-3xl p-6 border border-slate-200/80 shadow-2xs hover:shadow-xs transition space-y-3"
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span className="px-2.5 py-1 rounded-full bg-rose-50 text-rose-600 text-xs font-bold">
                          Kỷ niệm Năm thứ {entry.yearMilestone}
                        </span>
                        <span className="text-xs text-slate-400">· {entry.date}</span>
                        {entry.location && (
                          <span className="text-xs text-slate-500 font-medium">📍 {entry.location}</span>
                        )}
                      </div>

                      <button
                        type="button"
                        onClick={() => handleDeleteMemory(entry.id)}
                        className="text-slate-300 hover:text-rose-500 transition cursor-pointer p-1"
                        title="Xóa kỷ niệm"
                      >
                        <Trash2 size={16} />
                      </button>
                    </div>

                    <h4 className="text-base font-bold text-slate-900">{entry.title}</h4>
                    <p className="text-xs sm:text-sm text-slate-600 leading-relaxed whitespace-pre-line">
                      {entry.story}
                    </p>
                  </article>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </main>
  );
}
