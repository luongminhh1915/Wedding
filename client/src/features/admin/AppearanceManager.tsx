import { useState, useEffect, useRef } from "react";
import {
  Music,
  Sparkles,
  UploadCloud,
  Trash2,
  Play,
  Pause,
  Loader2,
  CheckCircle2,
  AlertCircle,
  Volume2,
  Flower2,
  RotateCcw,
  Eye,
  EyeOff,
  Pencil,
  ArrowUp,
  ArrowDown,
  Check,
  X,
} from "lucide-react";
import axiosClient from "../../services/axiosClient";
import { useAuthStore } from "../../store/authStore";
import {
  DEFAULT_NAVBAR_TRACKS,
  navbarMusicUrl,
  normalizeNavbarPlaylist,
  navbarMusicActor,
  navbarMusicActorIsCurrent,
  saveNavbarPlaylist,
  type NavbarMusicActor,
  type NavbarMusicTrack,
} from "../../components/navbarMusicModel";
import type { PetalStyle } from "../home/components/FallingPetals";

interface AppearanceSettings {
  petalsEnabled: boolean;
  petalsStyle: PetalStyle;
  petalsSpeed: "slow" | "normal" | "fast";
  petalsDensity: number;
  defaultMusicVolume: number;
  musicAutoPlay: boolean;
}

const DEFAULT_SETTINGS: AppearanceSettings = {
  petalsEnabled: true,
  petalsStyle: "blush",
  petalsSpeed: "normal",
  petalsDensity: 14,
  defaultMusicVolume: 0.35,
  musicAutoPlay: false,
};

function validatedSettings(value: unknown): AppearanceSettings {
  if (!value || typeof value !== "object") throw new Error("Cấu hình giao diện từ máy chủ không hợp lệ.");
  const settings = value as Record<string, unknown>;
  if (typeof settings.petalsEnabled !== "boolean" || typeof settings.musicAutoPlay !== "boolean"
    || !["blush", "red", "white", "mixed"].includes(String(settings.petalsStyle))
    || !["slow", "normal", "fast"].includes(String(settings.petalsSpeed))
    || typeof settings.petalsDensity !== "number" || !Number.isInteger(settings.petalsDensity)
    || settings.petalsDensity < 6 || settings.petalsDensity > 24 || settings.petalsDensity % 2 !== 0
    || typeof settings.defaultMusicVolume !== "number" || !Number.isFinite(settings.defaultMusicVolume)
    || settings.defaultMusicVolume < 0 || settings.defaultMusicVolume > 1)
    throw new Error("Cấu hình cần mật độ 6–24 cánh (số chẵn), âm lượng 0–100% và kiểu hiệu ứng hợp lệ.");
  return {
    petalsEnabled: settings.petalsEnabled, petalsStyle: settings.petalsStyle as PetalStyle,
    petalsSpeed: settings.petalsSpeed as AppearanceSettings["petalsSpeed"], petalsDensity: settings.petalsDensity,
    defaultMusicVolume: settings.defaultMusicVolume, musicAutoPlay: settings.musicAutoPlay,
  };
}

function requestMessage(error: unknown, fallback: string): string {
  const candidate = error as { response?: { status?: number; data?: { message?: unknown } }; message?: unknown };
  if (candidate?.response?.status === 404) return "Máy chủ chưa hỗ trợ chức năng này. Cần cập nhật backend trước khi lưu cấu hình.";
  const message = candidate?.response?.data?.message ?? candidate?.message;
  return typeof message === "string" && message ? message.slice(0, 400) : fallback;
}

type AppearanceOperation = { actor: NavbarMusicActor; controller: AbortController; generation: number };

export default function AppearanceManager() {
  const { user, token, sessionRevision, validated } = useAuthStore();
  const isAdmin = validated && !!token && (user?.role === "SuperAdmin" || user?.role === "Moderator");
  const [settings, setSettings] = useState<AppearanceSettings>(DEFAULT_SETTINGS);
  const [settingsLoading, setSettingsLoading] = useState(true);
  const [settingsLoaded, setSettingsLoaded] = useState(false);
  const [savingSettings, setSavingSettings] = useState(false);
  const [settingsSuccess, setSettingsSuccess] = useState<string | null>(null);
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [tracks, setTracks] = useState<NavbarMusicTrack[]>([]);
  const [playlistLoading, setPlaylistLoading] = useState(true);
  const [playlistLoaded, setPlaylistLoaded] = useState(false);
  const [savingPlaylist, setSavingPlaylist] = useState(false);
  const [playlistSuccess, setPlaylistSuccess] = useState<string | null>(null);
  const [playlistError, setPlaylistError] = useState<string | null>(null);
  const [isUploadingMp3, setIsUploadingMp3] = useState(false);
  const [reloadVersion, setReloadVersion] = useState(0);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const replaceFileInputRef = useRef<HTMLInputElement | null>(null);
  const [editingTrackId, setEditingTrackId] = useState<string | null>(null);
  const [editingTrackTitle, setEditingTrackTitle] = useState("");
  const [replacingTrackId, setReplacingTrackId] = useState<string | null>(null);
  const [previewTrackId, setPreviewTrackId] = useState<string | null>(null);
  const audioPreviewRef = useRef<HTMLAudioElement | null>(null);
  const previewGeneration = useRef(0);
  const lifecycle = useRef({ mounted: false, generation: 0 });
  const controllers = useRef(new Set<AbortController>());
  const writeFlight = useRef<AbortController | null>(null);
  const settingsOwner = useRef<NavbarMusicActor | null>(null);
  const playlistOwner = useRef<NavbarMusicActor | null>(null);
  const writeBusy = savingSettings || savingPlaylist || isUploadingMp3;

  const isCurrent = (operation: AppearanceOperation) => lifecycle.current.mounted
    && lifecycle.current.generation === operation.generation && !operation.controller.signal.aborted
    && navbarMusicActorIsCurrent(operation.actor, useAuthStore.getState());

  useEffect(() => {
    const actor = navbarMusicActor(useAuthStore.getState());
    const controller = new AbortController();
    lifecycle.current = { mounted: true, generation: lifecycle.current.generation + 1 };
    const generation = lifecycle.current.generation;
    const current = () => lifecycle.current.mounted && lifecycle.current.generation === generation
      && !controller.signal.aborted && !!actor && navbarMusicActorIsCurrent(actor, useAuthStore.getState());
    controllers.current.add(controller);
    settingsOwner.current = null;
    playlistOwner.current = null;
    writeFlight.current = null;
    setSettingsLoaded(false); setPlaylistLoaded(false);
    setSettingsSuccess(null); setPlaylistSuccess(null);
    setSavingSettings(false); setSavingPlaylist(false); setIsUploadingMp3(false);
    setPreviewTrackId(null); setTracks([]); setSettings(DEFAULT_SETTINGS);
    setSettingsLoading(!!actor); setPlaylistLoading(!!actor);
    setSettingsError(actor ? null : "Cần phiên quản trị đã được xác nhận để quản lý giao diện.");
    setPlaylistError(actor ? null : "Cần phiên quản trị đã được xác nhận để quản lý nhạc.");
    if (actor) {
      const options = { signal: controller.signal, headers: { Authorization: `Bearer ${actor.token}` } };
      void axiosClient.get<unknown>("/api/upload/appearance/settings", options).then(({ data }) => {
        const loaded = validatedSettings(data);
        if (current()) { settingsOwner.current = actor; setSettings(loaded); setSettingsLoaded(true); }
      }).catch(error => { if (current()) setSettingsError(requestMessage(error, "Chưa tải được cấu hình; các giá trị hiển thị chưa được xác nhận từ máy chủ.")); })
        .finally(() => { if (current()) setSettingsLoading(false); });
      void axiosClient.get<unknown>("/api/upload/music/playlist", options).then(({ data }) => {
        const loaded = normalizeNavbarPlaylist(data);
        if (current()) { playlistOwner.current = actor; setTracks(loaded); setPlaylistLoaded(true); }
      }).catch(error => { if (current()) setPlaylistError(requestMessage(error, "Chưa tải được danh sách nhạc. Hãy tải lại trước khi thay đổi.")); })
        .finally(() => { if (current()) setPlaylistLoading(false); });
    }
    return () => {
      lifecycle.current = { mounted: false, generation: lifecycle.current.generation + 1 };
      for (const pending of controllers.current) pending.abort();
      controllers.current.clear(); writeFlight.current = null;
      settingsOwner.current = null; playlistOwner.current = null;
      previewGeneration.current += 1;
      if (audioPreviewRef.current) { audioPreviewRef.current.pause(); audioPreviewRef.current.src = ""; }
    };
  }, [user?.id, user?.role, token, sessionRevision, validated, reloadVersion]);

  const beginWrite = (kind: "settings" | "playlist"): AppearanceOperation | null => {
    const actor = navbarMusicActor(useAuthStore.getState());
    const owner = kind === "settings" ? settingsOwner.current : playlistOwner.current;
    if (writeFlight.current || !lifecycle.current.mounted || !actor || !owner
      || !navbarMusicActorIsCurrent(owner, useAuthStore.getState())) return null;
    const controller = new AbortController();
    writeFlight.current = controller; controllers.current.add(controller);
    return { actor, controller, generation: lifecycle.current.generation };
  };
  const finishWrite = (operation: AppearanceOperation) => {
    controllers.current.delete(operation.controller);
    if (writeFlight.current === operation.controller) writeFlight.current = null;
    if (isCurrent(operation)) { setSavingSettings(false); setSavingPlaylist(false); setIsUploadingMp3(false); }
  };

  const handleSaveSettings = async () => {
    const operation = beginWrite("settings");
    if (!operation) return;
    setSavingSettings(true); setSettingsSuccess(null); setSettingsError(null);
    try {
      const proposed = validatedSettings(settings);
      if (!isCurrent(operation)) return;
      const response = await axiosClient.put<{ success?: boolean; settings?: unknown }>("/api/upload/appearance/settings", proposed, {
        signal: operation.controller.signal, headers: { Authorization: `Bearer ${operation.actor.token}` },
      });
      if (!isCurrent(operation)) return;
      if (response.data?.success !== true) throw new Error("Máy chủ chưa xác nhận lưu cấu hình giao diện.");
      const confirmed = response.data.settings ? validatedSettings(response.data.settings) : proposed;
      setSettings(confirmed);
      // Local visitor preferences are not overwritten by editing site defaults.
      setSettingsSuccess("Đã lưu cấu hình hiệu ứng cánh hoa và âm thanh thành công!");
    } catch (error) {
      if (isCurrent(operation)) setSettingsError(requestMessage(error, "Không thể lưu cấu hình."));
    } finally { finishWrite(operation); }
  };

  const persistPlaylist = async (nextTracks: NavbarMusicTrack[], operation: AppearanceOperation) => {
    const confirmed = await saveNavbarPlaylist(nextTracks, {
      signal: operation.controller.signal, isCurrent: () => isCurrent(operation),
      send: async (normalized, signal) => (await axiosClient.put("/api/upload/music/playlist", normalized, {
        signal, headers: { Authorization: `Bearer ${operation.actor.token}` },
      })).data,
    });
    if (!isCurrent(operation)) return;
    setTracks(confirmed); setPlaylistSuccess("Đã cập nhật danh sách nhạc nền toàn trang!");
  };
  const savePlaylistToServer = async (nextTracks: NavbarMusicTrack[]) => {
    const operation = beginWrite("playlist");
    if (!operation) return;
    setSavingPlaylist(true); setPlaylistSuccess(null); setPlaylistError(null);
    try { await persistPlaylist(nextTracks, operation); }
    catch (error) { if (isCurrent(operation)) setPlaylistError(requestMessage(error, "Không thể lưu danh sách nhạc.")); }
    finally { finishWrite(operation); }
  };

  const handleUploadMp3 = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]; event.target.value = "";
    if (!file || !isAdmin || !playlistOwner.current || !navbarMusicActorIsCurrent(playlistOwner.current, useAuthStore.getState())) return;
    if (!/\.mp3$/i.test(file.name) || file.size <= 0 || file.size > 12 * 1024 * 1024) {
      setPlaylistError("Vui lòng chọn tệp MP3 có dung lượng tối đa 12 MB."); return;
    }
    if (tracks.length >= 20) { setPlaylistError("Danh sách nhạc chỉ được có tối đa 20 bài."); return; }
    const operation = beginWrite("playlist");
    if (!operation) return;
    setIsUploadingMp3(true); setPlaylistError(null); setPlaylistSuccess(null);
    let uploaded = false;
    try {
      const formData = new FormData(); formData.append("file", file);
      const response = await axiosClient.post<{ url: string }>("/api/upload/music", formData, {
        signal: operation.controller.signal,
        headers: { "Content-Type": "multipart/form-data", Authorization: `Bearer ${operation.actor.token}` },
      });
      if (!isCurrent(operation)) return;
      const url = navbarMusicUrl(response.data.url);
      uploaded = true;
      const newTrack: NavbarMusicTrack = {
        id: crypto.randomUUID(), title: file.name.replace(/\.[^/.]+$/, "").slice(0, 150) || "Bản nhạc MP3",
        type: "audio", url, isCustom: true,
      };
      await persistPlaylist([...tracks, newTrack], operation);
    } catch (error) {
      if (isCurrent(operation)) setPlaylistError((uploaded ? "Tệp đã tải lên nhưng danh sách phát chưa được xác nhận lưu. " : "")
        + requestMessage(error, "Không thể tải tệp MP3 lên máy chủ."));
    } finally { finishWrite(operation); }
  };

  const stopPreview = () => {
    previewGeneration.current += 1;
    if (audioPreviewRef.current) { audioPreviewRef.current.pause(); audioPreviewRef.current.src = ""; }
    setPreviewTrackId(null);
  };
  const handleDeleteTrack = async (id: string) => {
    if (writeFlight.current || !isAdmin || !playlistOwner.current || !navbarMusicActorIsCurrent(playlistOwner.current, useAuthStore.getState())) return;
    if (tracks.length <= 1) { setPlaylistError("Danh sách nhạc cần giữ lại ít nhất 1 bài hát."); return; }
    if (previewTrackId === id) stopPreview();
    await savePlaylistToServer(tracks.filter(track => track.id !== id));
  };
  const handleToggleHidden = async (id: string) => {
    if (writeFlight.current || !isAdmin || !playlistOwner.current || !navbarMusicActorIsCurrent(playlistOwner.current, useAuthStore.getState())) return;
    const target = tracks.find(t => t.id === id);
    if (!target) return;
    const nextHidden = !target.hidden;
    const remainingVisible = tracks.filter(t => t.id !== id && !t.hidden);
    if (nextHidden && remainingVisible.length === 0) {
      setPlaylistError("Cần giữ ít nhất 1 bài hát hiển thị để khách có thể nghe nhạc.");
      return;
    }
    if (previewTrackId === id && nextHidden) stopPreview();
    const updated = tracks.map(t => t.id === id ? { ...t, hidden: nextHidden } : t);
    await savePlaylistToServer(updated);
  };

  const handleStartEdit = (track: NavbarMusicTrack) => {
    setEditingTrackId(track.id);
    setEditingTrackTitle(track.title);
  };

  const handleCancelEdit = () => {
    setEditingTrackId(null);
    setEditingTrackTitle("");
  };

  const handleSaveTrackTitle = async (id: string) => {
    if (writeFlight.current || !isAdmin || !playlistOwner.current || !navbarMusicActorIsCurrent(playlistOwner.current, useAuthStore.getState())) return;
    const trimmed = editingTrackTitle.trim();
    if (!trimmed || trimmed.length > 150) {
      setPlaylistError("Tên bài hát cần từ 1 đến 150 ký tự.");
      return;
    }
    const updated = tracks.map(t => t.id === id ? { ...t, title: trimmed } : t);
    setEditingTrackId(null);
    setEditingTrackTitle("");
    await savePlaylistToServer(updated);
  };

  const handleMoveTrack = async (id: string, direction: "up" | "down") => {
    if (writeFlight.current || !isAdmin || !playlistOwner.current || !navbarMusicActorIsCurrent(playlistOwner.current, useAuthStore.getState())) return;
    const idx = tracks.findIndex(t => t.id === id);
    if (idx < 0) return;
    const targetIdx = direction === "up" ? idx - 1 : idx + 1;
    if (targetIdx < 0 || targetIdx >= tracks.length) return;
    const updated = [...tracks];
    const temp = updated[idx];
    updated[idx] = updated[targetIdx];
    updated[targetIdx] = temp;
    await savePlaylistToServer(updated);
  };

  const handleTriggerReplaceMp3 = (id: string) => {
    setReplacingTrackId(id);
    replaceFileInputRef.current?.click();
  };

  const handleReplaceMp3 = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    const targetId = replacingTrackId;
    setReplacingTrackId(null);
    if (!file || !targetId || !isAdmin || !playlistOwner.current || !navbarMusicActorIsCurrent(playlistOwner.current, useAuthStore.getState())) return;
    if (!/\.mp3$/i.test(file.name) || file.size <= 0 || file.size > 12 * 1024 * 1024) {
      setPlaylistError("Vui lòng chọn tệp MP3 có dung lượng tối đa 12 MB.");
      return;
    }
    const operation = beginWrite("playlist");
    if (!operation) return;
    setIsUploadingMp3(true);
    setPlaylistError(null);
    setPlaylistSuccess(null);
    let uploaded = false;
    try {
      const formData = new FormData();
      formData.append("file", file);
      const response = await axiosClient.post<{ url: string }>("/api/upload/music", formData, {
        signal: operation.controller.signal,
        headers: { "Content-Type": "multipart/form-data", Authorization: `Bearer ${operation.actor.token}` },
      });
      if (!isCurrent(operation)) return;
      const url = navbarMusicUrl(response.data.url);
      uploaded = true;
      const updated = tracks.map(t => t.id === targetId ? { ...t, url, type: "audio" as const, isCustom: true } : t);
      await persistPlaylist(updated, operation);
    } catch (error) {
      if (isCurrent(operation)) {
        setPlaylistError((uploaded ? "Tệp đã tải lên nhưng danh sách phát chưa được xác nhận lưu. " : "")
          + requestMessage(error, "Không thể tải tệp MP3 mới lên máy chủ."));
      }
    } finally {
      finishWrite(operation);
    }
  };

  const handleResetDefaults = async () => {
    if (writeFlight.current) return;
    stopPreview(); await savePlaylistToServer(DEFAULT_NAVBAR_TRACKS);
  };
  const handleTogglePreview = (track: NavbarMusicTrack) => {
    if (!isAdmin || !playlistOwner.current || !navbarMusicActorIsCurrent(playlistOwner.current, useAuthStore.getState())) return;
    if (previewTrackId === track.id) { stopPreview(); return; }
    stopPreview();
    if (!track.url || !audioPreviewRef.current) return;
    const actor = playlistOwner.current;
    const generation = previewGeneration.current;
    try {
      const url = navbarMusicUrl(track.url);
      audioPreviewRef.current.src = url.startsWith("https://") ? url : `${(import.meta.env.VITE_API_URL || "").replace(/\/$/, "")}${url}`;
      audioPreviewRef.current.volume = validatedSettings(settings).defaultMusicVolume;
      setPreviewTrackId(track.id);
      void audioPreviewRef.current.play().catch(() => {
        if (lifecycle.current.mounted && previewGeneration.current === generation && actor && navbarMusicActorIsCurrent(actor, useAuthStore.getState())) {
          setPreviewTrackId(null); setPlaylistError("Trình duyệt chưa phát được bài này. Bạn có thể thử lại.");
        }
      });
    } catch (error) { setPlaylistError(requestMessage(error, "Không thể nghe thử bài hát này.")); }
  };

  return (
    <div className="space-y-8 max-w-6xl">
      {(settingsError || playlistError) && <button type="button" onClick={() => setReloadVersion(value => value + 1)} disabled={writeBusy || !isAdmin} className="text-sm font-semibold text-rose-700 underline disabled:opacity-50">Tải lại cấu hình và danh sách nhạc</button>}
      {/* Hidden Audio Element for Preview */}
      <audio ref={audioPreviewRef} onEnded={stopPreview} />

      {/* Hidden File Input for Replace MP3 */}
      <input
        ref={replaceFileInputRef}
        type="file"
        accept="audio/mpeg,.mp3"
        className="hidden"
        onChange={handleReplaceMp3}
        disabled={writeBusy || playlistLoading || !playlistLoaded || !isAdmin}
      />

      {/* Hidden File Input for MP3 Upload */}
      <input
        ref={fileInputRef}
        type="file"
        accept="audio/mpeg,.mp3"
        className="hidden"
        onChange={handleUploadMp3}
        disabled={writeBusy || playlistLoading || !playlistLoaded || !isAdmin}
      />

      {/* SECTION 1: Wedding Falling Petals Effect */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200/80 shadow-xs space-y-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 pb-4">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-rose-50 text-rose-600 border border-rose-100">
              <Flower2 size={24} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-800">Hiệu ứng Cánh Hoa Rơi (Falling Petals)</h3>
              <p className="text-xs text-slate-500">
                Hiệu ứng cánh hoa rơi nhẹ nhàng, mang lại không gian lãng mạn cho ngày cưới trên trang chủ.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleSaveSettings}
            disabled={writeBusy || settingsLoading || !settingsLoaded || !isAdmin}
            className="inline-flex items-center justify-center gap-2 px-5 py-2.5 text-xs font-semibold rounded-xl bg-rose-600 hover:bg-rose-700 text-white shadow-xs transition disabled:opacity-50"
          >
            {savingSettings ? <Loader2 size={14} className="animate-spin" /> : <Sparkles size={14} />}
            <span>Lưu cấu hình giao diện</span>
          </button>
        </div>

        {settingsSuccess && (
          <div className="flex items-center gap-2 p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-xl text-xs">
            <CheckCircle2 size={16} className="text-emerald-600 shrink-0" />
            <span>{settingsSuccess}</span>
          </div>
        )}
        {settingsError && (
          <div className="flex items-center gap-2 p-3 bg-rose-50 border border-rose-200 text-rose-800 rounded-xl text-xs">
            <AlertCircle size={16} className="text-rose-600 shrink-0" />
            <span>{settingsError}</span>
          </div>
        )}

        {settingsLoading ? (
          <div className="py-8 flex items-center justify-center text-slate-400 gap-2 text-xs">
            <Loader2 size={16} className="animate-spin" />
            <span>Đang tải cấu hình giao diện...</span>
          </div>
        ) : (
          <fieldset disabled={!isAdmin || !settingsLoaded || writeBusy} className="grid grid-cols-1 md:grid-cols-2 gap-6 disabled:opacity-60">
            {/* Auto-Enable Switch */}
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200/70 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-slate-800">Bật hiệu ứng cánh hoa</span>
                <label className="relative inline-flex items-center cursor-pointer">
                  <input
                    type="checkbox"
                    checked={settings.petalsEnabled}
                    onChange={(e) => setSettings({ ...settings, petalsEnabled: e.target.checked })}
                    className="sr-only peer"
                  />
                  <div className="w-11 h-6 bg-slate-200 peer-focus:outline-hidden rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-rose-500"></div>
                </label>
              </div>
              <p className="text-[11px] text-slate-500 leading-relaxed">
                Mặc định bật tự động cho mọi khách truy cập để tạo không khí lễ cưới ấm cúng ngay khi vào trang chủ.
              </p>
            </div>

            {/* Density control */}
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200/70 space-y-2">
              <div className="flex items-center justify-between text-xs font-semibold text-slate-700">
                <span>Mật độ cánh hoa rơi</span>
                <span className="px-2 py-0.5 rounded-full bg-white border border-slate-200 text-rose-600 font-bold">
                  {settings.petalsDensity} cánh (Thưa & êm dịu)
                </span>
              </div>
              <input
                type="range"
                min="6"
                max="24"
                step="2"
                value={settings.petalsDensity}
                onChange={(e) => setSettings({ ...settings, petalsDensity: Number(e.target.value) })}
                className="w-full h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer accent-rose-500"
              />
              <div className="flex justify-between text-[10px] text-slate-400">
                <span>Rất ít (6 cánh)</span>
                <span>Vừa vặn (14 cánh - Khuyên dùng)</span>
                <span>Thêm chút (24 cánh)</span>
              </div>
            </div>
          </fieldset>
        )}
      </div>

      {/* SECTION 2: Initial Audio Volume Setting on First Page Load */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200/80 shadow-xs space-y-4">
        <div className="flex items-center gap-3 border-b border-slate-100 pb-4">
          <div className="p-2.5 rounded-xl bg-rose-50 text-rose-600 border border-rose-100">
            <Volume2 size={24} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-800">Âm Lượng Ban Đầu Khi Khách Mới Vào Web</h3>
            <p className="text-xs text-slate-500">
              Điều chỉnh mức âm lượng mặc định khi khách truy cập lần đầu mở trang web (tránh âm thanh đột ngột).
            </p>
          </div>
        </div>

        <fieldset disabled={!isAdmin || !settingsLoaded || writeBusy} className="p-4 rounded-xl bg-slate-50 border border-slate-200/70 space-y-3 disabled:opacity-60">
          <div className="flex items-center justify-between text-xs font-semibold text-slate-700">
            <span>Mức âm lượng mặc định:</span>
            <span className="px-2.5 py-0.5 rounded-full bg-white border border-slate-200 text-rose-600 font-bold">
              {Math.round(settings.defaultMusicVolume * 100)}%
            </span>
          </div>
          <input
            type="range"
            min="0"
            max="1"
            step="0.05"
            value={settings.defaultMusicVolume}
            onChange={(e) => setSettings({ ...settings, defaultMusicVolume: Number(e.target.value) })}
            className="w-full h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer accent-rose-500"
          />
          <div className="flex flex-wrap gap-2 pt-1">
            {[
              { label: "🔇 Tắt tiếng ban đầu (0%)", val: 0 },
              { label: "🔉 Khẽ khàng (15%)", val: 0.15 },
              { label: "🎵 Vừa vặn (35% - Khuyên dùng)", val: 0.35 },
              { label: "🔊 Rõ ràng (50%)", val: 0.5 },
            ].map((p) => (
              <button
                key={p.val}
                type="button"
                onClick={() => setSettings({ ...settings, defaultMusicVolume: p.val })}
                className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition ${
                  Math.abs(settings.defaultMusicVolume - p.val) < 0.01
                    ? "bg-rose-500 text-white border-rose-600 shadow-xs"
                    : "bg-white text-slate-700 border-slate-200 hover:bg-slate-100"
                }`}
              >
                {p.label}
              </button>
            ))}
          </div>
          <p className="text-[11px] text-slate-400">
            Lưu ý: Bấm <strong>"Lưu cấu hình giao diện"</strong> ở trên để cập nhật âm lượng mặc định này cho toàn bộ khách mới.
          </p>
        </fieldset>
      </div>

      {/* SECTION 3: Background Music (MP3 Only) */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200/80 shadow-xs space-y-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 pb-4">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-amber-50 text-amber-600 border border-amber-100">
              <Music size={24} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-800">Quản Lý Nhạc Nền Toàn Trang (MP3)</h3>
              <p className="text-xs text-slate-500">
                Admin tải file MP3 trực tiếp từ máy tính lên hệ thống hoặc xóa bớt bài hát để quản lý danh sách phát.
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={handleResetDefaults}
              disabled={writeBusy || playlistLoading || !playlistLoaded || !isAdmin}
              className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 transition disabled:opacity-50"
              title="Khôi phục danh sách nhạc mẫu mặc định"
            >
              <RotateCcw size={13} />
              <span>Khôi phục mẫu</span>
            </button>
          </div>
        </div>

        {playlistSuccess && (
          <div className="flex items-center gap-2 p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-xl text-xs">
            <CheckCircle2 size={16} className="text-emerald-600 shrink-0" />
            <span>{playlistSuccess}</span>
          </div>
        )}
        {playlistError && (
          <div className="flex items-center gap-2 p-3 bg-rose-50 border border-rose-200 text-rose-800 rounded-xl text-xs">
            <AlertCircle size={16} className="text-rose-600 shrink-0" />
            <span>{playlistError}</span>
          </div>
        )}

        {/* MP3 Upload Card */}
        <button
          type="button"
          disabled={writeBusy || playlistLoading || !playlistLoaded || !isAdmin || tracks.length >= 20}
          onClick={() => fileInputRef.current?.click()}
          className="w-full disabled:cursor-not-allowed disabled:opacity-50 p-6 border-2 border-dashed border-rose-200 hover:border-rose-400 bg-rose-50/40 hover:bg-rose-50/70 rounded-2xl cursor-pointer transition flex flex-col items-center justify-center gap-2 text-center"
        >
          <div className="p-3 bg-white text-rose-600 rounded-full shadow-xs">
            {isUploadingMp3 ? <Loader2 size={24} className="animate-spin" /> : <UploadCloud size={24} />}
          </div>
          <div>
            <span className="text-xs font-bold text-slate-800 block">
              {isUploadingMp3 ? "Đang tải tệp MP3 lên máy chủ..." : "Bấm vào đây để tải tệp nhạc MP3 từ máy tính"}
            </span>
            <p className="text-[11px] text-slate-500 mt-1">
              Chỉ cần chọn file .mp3 (tối đa 12 MB), bài hát sẽ tự động được thêm vào danh sách phát và lưu trên máy chủ.
            </p>
          </div>
        </button>

        {/* Tracks Table */}
        <div className="border border-slate-200 rounded-xl overflow-hidden">
          <div className="bg-slate-50 px-4 py-2.5 border-b border-slate-200 text-xs font-bold text-slate-700 flex justify-between items-center">
            <span>Danh sách phát hiện tại ({tracks.length} / 20 bài)</span>
            {previewTrackId && (
              <button
                type="button"
                onClick={stopPreview}
                className="text-[11px] font-semibold text-rose-600 hover:text-rose-700 flex items-center gap-1"
              >
                <Pause size={12} />
                <span>Dừng nghe thử</span>
              </button>
            )}
          </div>

          {playlistLoading ? (
            <div className="py-8 flex items-center justify-center text-slate-400 gap-2 text-xs">
              <Loader2 size={16} className="animate-spin" />
              <span>Đang tải danh sách bài hát...</span>
            </div>
          ) : tracks.length === 0 ? (
            <div className="py-8 text-center text-xs text-slate-400">
              {playlistLoaded ? "Chưa có bài hát nào trong danh sách." : "Chưa tải được danh sách. Hãy tải lại trước khi chỉnh sửa."}
            </div>
          ) : (
            <div className="divide-y divide-slate-100">
              {tracks.map((track, idx) => {
                const isThisPlaying = previewTrackId === track.id;
                const isEditing = editingTrackId === track.id;
                const isHidden = track.hidden === true;
                return (
                  <div
                    key={track.id}
                    className={`px-4 py-3 flex flex-col sm:flex-row sm:items-center justify-between hover:bg-slate-50/80 transition text-xs gap-3 ${
                      isHidden ? "bg-slate-50/60 opacity-80" : ""
                    }`}
                  >
                    <div className="flex items-center gap-3 min-w-0 flex-1">
                      {/* Sort arrows: Move Up / Move Down */}
                      {isAdmin && (
                        <div className="flex flex-col gap-0.5 shrink-0">
                          <button
                            type="button"
                            disabled={writeBusy || idx === 0}
                            onClick={() => handleMoveTrack(track.id, "up")}
                            className="p-0.5 text-slate-400 hover:text-slate-700 disabled:opacity-20 rounded hover:bg-slate-200 transition"
                            title="Đẩy lên trước"
                            aria-label="Đẩy bài hát lên trước"
                          >
                            <ArrowUp size={12} />
                          </button>
                          <button
                            type="button"
                            disabled={writeBusy || idx === tracks.length - 1}
                            onClick={() => handleMoveTrack(track.id, "down")}
                            className="p-0.5 text-slate-400 hover:text-slate-700 disabled:opacity-20 rounded hover:bg-slate-200 transition"
                            title="Đẩy xuống sau"
                            aria-label="Đẩy bài hát xuống sau"
                          >
                            <ArrowDown size={12} />
                          </button>
                        </div>
                      )}

                      <span className="text-slate-400 font-mono text-[11px] w-4 text-center">{idx + 1}</span>

                      {/* Play / Preview button */}
                      <button
                        type="button"
                        onClick={() => handleTogglePreview(track)}
                        className={`p-2 rounded-xl transition shrink-0 ${
                          isThisPlaying
                            ? "bg-rose-500 text-white shadow-xs"
                            : "bg-slate-100 hover:bg-slate-200 text-slate-700"
                        }`}
                        title={isThisPlaying ? "Dừng nghe thử" : "Nghe thử bản nhạc"}
                      >
                        {isThisPlaying ? <Pause size={13} className="fill-current" /> : <Play size={13} className="fill-current ml-0.5" />}
                      </button>

                      <div className="min-w-0 flex-1">
                        {isEditing ? (
                          <div className="flex items-center gap-2 max-w-md">
                            <input
                              type="text"
                              value={editingTrackTitle}
                              maxLength={150}
                              onChange={(e) => setEditingTrackTitle(e.target.value)}
                              onKeyDown={(e) => {
                                if (e.key === "Enter") handleSaveTrackTitle(track.id);
                                if (e.key === "Escape") handleCancelEdit();
                              }}
                              className="px-2.5 py-1 border border-rose-300 rounded-lg text-xs font-semibold text-slate-800 bg-white focus:outline-rose-500 flex-1"
                              autoFocus
                            />
                            <button
                              type="button"
                              onClick={() => handleSaveTrackTitle(track.id)}
                              disabled={writeBusy}
                              className="p-1.5 rounded-lg bg-rose-600 hover:bg-rose-700 text-white shrink-0 transition"
                              title="Lưu tên bài hát"
                            >
                              <Check size={13} />
                            </button>
                            <button
                              type="button"
                              onClick={handleCancelEdit}
                              className="p-1.5 rounded-lg bg-slate-200 hover:bg-slate-300 text-slate-700 shrink-0 transition"
                              title="Hủy sửa"
                            >
                              <X size={13} />
                            </button>
                          </div>
                        ) : (
                          <div className="flex items-center gap-2 flex-wrap">
                            <span
                              className={`font-semibold truncate max-w-sm ${
                                isHidden ? "text-slate-500 line-through decoration-slate-400" : "text-slate-800"
                              }`}
                              title={track.title}
                            >
                              {track.title}
                            </span>
                            {track.type === "synth" ? (
                              <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-100 text-amber-700 border border-amber-200">
                                Piano Synth
                              </span>
                            ) : (
                              <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-rose-100 text-rose-700 border border-rose-200">
                                MP3
                              </span>
                            )}
                            {isHidden ? (
                              <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-slate-200 text-slate-600 border border-slate-300">
                                Đã ẩn
                              </span>
                            ) : (
                              <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
                                Đang hiển thị
                              </span>
                            )}
                          </div>
                        )}

                        {track.url && (
                          <div className="text-[11px] text-slate-400 truncate max-w-md mt-0.5 font-mono">
                            {track.url}
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Action buttons on the right */}
                    <div className="flex items-center gap-1.5 shrink-0 self-end sm:self-center">
                      {/* Edit Title button */}
                      {isAdmin && !isEditing && (
                        <button
                          type="button"
                          onClick={() => handleStartEdit(track)}
                          disabled={writeBusy || !playlistLoaded}
                          className="inline-flex items-center gap-1 px-2 py-1 text-[11px] font-semibold text-slate-600 hover:text-slate-900 rounded-lg hover:bg-slate-100 transition"
                          title="Sửa tên bài hát"
                        >
                          <Pencil size={12} />
                          <span className="hidden md:inline">Sửa tên</span>
                        </button>
                      )}

                      {/* Replace MP3 File button (only for audio tracks) */}
                      {isAdmin && track.type === "audio" && (
                        <button
                          type="button"
                          onClick={() => handleTriggerReplaceMp3(track.id)}
                          disabled={writeBusy || !playlistLoaded}
                          className="inline-flex items-center gap-1 px-2 py-1 text-[11px] font-semibold text-rose-600 hover:text-rose-700 rounded-lg hover:bg-rose-50 transition"
                          title="Tải tệp MP3 mới thay thế cho bài này"
                        >
                          <UploadCloud size={12} />
                          <span className="hidden md:inline">Đổi tệp</span>
                        </button>
                      )}

                      {/* Toggle Hidden / Visible button */}
                      {isAdmin && (
                        <button
                          type="button"
                          onClick={() => handleToggleHidden(track.id)}
                          disabled={writeBusy || !playlistLoaded}
                          className={`inline-flex items-center gap-1 px-2 py-1 text-[11px] font-semibold rounded-lg transition ${
                            isHidden
                              ? "bg-slate-200 hover:bg-slate-300 text-slate-700"
                              : "bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border border-emerald-200"
                          }`}
                          title={isHidden ? "Hiện lại bài hát trên website" : "Ẩn bài hát khỏi website (tạm ngưng phát)"}
                        >
                          {isHidden ? <Eye size={12} /> : <EyeOff size={12} />}
                          <span>{isHidden ? "Hiện" : "Ẩn"}</span>
                        </button>
                      )}

                      {/* Delete Track button */}
                      {isAdmin && tracks.length > 1 && (
                        <button
                          type="button"
                          onClick={() => handleDeleteTrack(track.id)}
                          disabled={writeBusy || !playlistLoaded}
                          className="p-1.5 text-slate-400 hover:text-rose-600 rounded-lg hover:bg-rose-50 transition"
                          title="Xóa bài hát"
                        >
                          <Trash2 size={13} />
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
