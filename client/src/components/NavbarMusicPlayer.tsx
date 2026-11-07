import { useState, useEffect, useRef, useCallback } from "react";
import { Link } from "react-router-dom";
import {
  Volume2, VolumeX, Play, Pause, ChevronDown,
  Sparkles, X, UploadCloud, Trash2, Music, Loader2, Palette, Eye, EyeOff
} from "lucide-react";
import axiosClient from "../services/axiosClient";
import { useAuthStore } from "../store/authStore";
import {
  DEFAULT_NAVBAR_TRACKS, extractYouTubeId, navbarMusicActor, navbarMusicActorIsCurrent, navbarMusicUrl,
  navbarMusicVolume, normalizeNavbarPlaylist, saveNavbarPlaylist
} from "./navbarMusicModel.ts";
import type { NavbarMusicActor, NavbarMusicTrack as TrackItem } from "./navbarMusicModel.ts";

const DEFAULT_TRACKS = DEFAULT_NAVBAR_TRACKS;
const storedPreference = (key: string) => { try { return localStorage.getItem(key); } catch { return null; } };
const storePreference = (key: string, value: string) => { try { localStorage.setItem(key, value); } catch { /* Preferences may be unavailable. */ } };

// Web Audio API Procedural Piano Synthesizer (Pachelbel's Canon in D)
class WeddingSynthPlayer {
  private ctx: AudioContext | null = null;
  private isPlaying = false;
  private timerId: number | null = null;
  private masterGain: GainNode | null = null;
  private volume = 0.45;

  private chords = [
    [146.83, 220.0, 293.66, 369.99, 440.0],       // D maj
    [110.0, 164.81, 220.0, 277.18, 329.63],       // A maj
    [123.47, 185.0, 246.94, 293.66, 369.99],      // B min
    [92.5, 138.59, 185.0, 220.0, 277.18],        // F# min
    [98.0, 146.83, 196.0, 246.94, 293.66],       // G maj
    [146.83, 220.0, 293.66, 369.99, 440.0],       // D maj
    [98.0, 146.83, 196.0, 246.94, 293.66],       // G maj
    [110.0, 164.81, 220.0, 277.18, 329.63],       // A maj
  ];

  private currentChord = 0;
  private currentNote = 0;

  private initContext() {
    if (!this.ctx) {
      const AudioCtx =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.ctx = new AudioCtx();
      this.masterGain = this.ctx.createGain();
      this.masterGain.gain.setValueAtTime(this.volume, this.ctx.currentTime);
      this.masterGain.connect(this.ctx.destination);
    }
    if (this.ctx.state === "suspended") {
      void this.ctx.resume().catch(() => {});
    }
  }

  public setVolume(vol: number) {
    this.volume = navbarMusicVolume(vol);
    if (this.masterGain && this.ctx) {
      this.masterGain.gain.setValueAtTime(this.volume, this.ctx.currentTime);
    }
  }

  private playTone(freq: number, duration: number, isBass = false) {
    if (!this.ctx || !this.masterGain) return;
    const now = this.ctx.currentTime;

    const osc1 = this.ctx.createOscillator();
    const osc2 = this.ctx.createOscillator();
    const noteGain = this.ctx.createGain();
    const filter = this.ctx.createBiquadFilter();

    filter.type = "lowpass";
    filter.frequency.setValueAtTime(isBass ? 450 : 1800, now);

    osc1.type = "triangle";
    osc1.frequency.setValueAtTime(freq, now);

    osc2.type = "sine";
    osc2.frequency.setValueAtTime(freq * 2, now);

    const attack = isBass ? 0.08 : 0.03;
    const peak = isBass ? 0.35 : 0.22;
    noteGain.gain.setValueAtTime(0, now);
    noteGain.gain.linearRampToValueAtTime(peak, now + attack);
    noteGain.gain.exponentialRampToValueAtTime(0.001, now + duration);

    osc1.connect(filter);
    osc2.connect(filter);
    filter.connect(noteGain);
    noteGain.connect(this.masterGain);

    osc1.start(now);
    osc2.start(now);
    osc1.stop(now + duration);
    osc2.stop(now + duration);
  }

  public start() {
    if (this.isPlaying) return;
    this.initContext();
    this.isPlaying = true;
    this.currentChord = 0;
    this.currentNote = 0;

    const step = () => {
      if (!this.isPlaying) return;

      const chord = this.chords[this.currentChord];
      const freq = chord[this.currentNote];
      const isBass = this.currentNote === 0;

      this.playTone(freq, isBass ? 1.8 : 0.9, isBass);

      this.currentNote++;
      if (this.currentNote >= chord.length) {
        this.currentNote = 0;
        this.currentChord = (this.currentChord + 1) % this.chords.length;
      }

      this.timerId = window.setTimeout(step, 380);
    };

    step();
  }

  public stop() {
    this.isPlaying = false;
    if (this.timerId) {
      clearTimeout(this.timerId);
      this.timerId = null;
    }
  }

  public dispose() {
    this.stop();
    const context = this.ctx;
    this.ctx = null;
    this.masterGain = null;
    if (context && context.state !== "closed") void context.close().catch(() => {});
  }
}

export default function NavbarMusicPlayer() {
  const user = useAuthStore(state => state.user);
  const token = useAuthStore(state => state.token);
  const validated = useAuthStore(state => state.validated);
  const isAdmin = validated && !!token && (user?.role === "SuperAdmin" || user?.role === "Moderator");
  const [isPlaying, setIsPlaying] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [volume, setVolume] = useState(() => navbarMusicVolume(storedPreference("wm_bgm_volume")));
  const [isMuted, setIsMuted] = useState(false);
  // The public server playlist is authoritative; old local playlist JSON is never trusted.
  const [tracks, setTracks] = useState<TrackItem[]>(DEFAULT_TRACKS);
  const tracksRef = useRef<TrackItem[]>(DEFAULT_TRACKS);
  const [activeTrackId, setActiveTrackId] = useState(() => storedPreference("wm_active_track_id") || "canon-synth");
  const activeTrackIdRef = useRef(activeTrackId);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState("");

  const synthRef = useRef<WeddingSynthPlayer | null>(null);
  const audioElRef = useRef<HTMLAudioElement | null>(null);
  const ytPlayerRef = useRef<any>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const mountedRef = useRef(false);
  const readRef = useRef<AbortController | null>(null);
  const operationRef = useRef<{ actor: NavbarMusicActor; controller: AbortController } | null>(null);
  const playbackRef = useRef(0);
  const effectiveTracks = isAdmin ? tracks : tracks.filter(t => !t.hidden);
  const displayTracks = effectiveTracks.length > 0 ? effectiveTracks : DEFAULT_TRACKS;
  const activeTrack = displayTracks.find(t => t.id === activeTrackId) || displayTracks[0] || DEFAULT_TRACKS[0];

  const stopPlayback = useCallback(() => {
    playbackRef.current++;
    synthRef.current?.stop();
    audioElRef.current?.pause();
    if (ytPlayerRef.current?.pauseVideo) {
      try { ytPlayerRef.current.pauseVideo(); } catch { /* ignore */ }
    }
    setIsPlaying(false);
  }, []);

  const acceptPlaylist = useCallback((saved: TrackItem[]) => {
    const prior = tracksRef.current.find(t => t.id === activeTrackIdRef.current);
    const next = saved.find(t => t.id === activeTrackIdRef.current);
    if (!next || prior?.url !== next.url || prior?.type !== next.type || (!isAdmin && next.hidden)) stopPlayback();
    tracksRef.current = saved;
    setTracks(saved);
    if (!next || (!isAdmin && next.hidden)) {
      const visible = saved.filter(t => !t.hidden);
      const id = visible[0]?.id || saved[0]?.id || DEFAULT_TRACKS[0].id;
      activeTrackIdRef.current = id;
      setActiveTrackId(id);
    }
  }, [stopPlayback, isAdmin]);

  useEffect(() => {
    mountedRef.current = true;
    const synth = new WeddingSynthPlayer();
    synthRef.current = synth;
    const read = new AbortController();
    readRef.current = read;
    void axiosClient.get<unknown>("/api/upload/music/playlist", { signal: read.signal })
      .then(({ data }) => {
        if (mountedRef.current && !read.signal.aborted) acceptPlaylist(normalizeNavbarPlaylist(data));
      }).catch(() => {
        if (mountedRef.current && !read.signal.aborted)
          setUploadError("Chưa tải được danh sách nhạc. Bạn vẫn có thể chọn bản nhạc mặc định.");
      });

    // Sync initial volume from server if visitor has not customized their volume
    if (storedPreference("wm_bgm_volume") === null) {
      void axiosClient
        .get<{ defaultMusicVolume?: number }>("/api/upload/appearance/settings", { signal: read.signal })
        .then(({ data }) => {
          if (mountedRef.current && !read.signal.aborted && storedPreference("wm_bgm_volume") === null && data && typeof data.defaultMusicVolume === "number") {
            setVolume(navbarMusicVolume(data.defaultMusicVolume));
          }
        })
        .catch(() => {});
    }

    // Load YouTube API script if needed
    if (!(window as any).YT) {
      const tag = document.createElement("script");
      tag.src = "https://www.youtube.com/iframe_api";
      document.body.appendChild(tag);
    }

    const unsubscribe = useAuthStore.subscribe((session, previous) => {
      const operation = operationRef.current;
      if (operation && !navbarMusicActorIsCurrent(operation.actor, session)) operation.controller.abort();
      if (session.token !== previous.token || session.user?.id !== previous.user?.id ||
        session.user?.role !== previous.user?.role || session.sessionRevision !== previous.sessionRevision) {
        setUploadError("");
      }
    });
    const audio = audioElRef.current;
    const outside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) setIsOpen(false);
    };
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape") setIsOpen(false); };
    document.addEventListener("mousedown", outside);
    document.addEventListener("keydown", escape);
    return () => {
      mountedRef.current = false;
      read.abort();
      operationRef.current?.controller.abort();
      unsubscribe();
      synth.dispose();
      if (audio) { audio.pause(); audio.removeAttribute("src"); audio.load(); }
      if (ytPlayerRef.current?.pauseVideo) {
        try { ytPlayerRef.current.pauseVideo(); } catch { /* ignore */ }
      }
      document.removeEventListener("mousedown", outside);
      document.removeEventListener("keydown", escape);
    };
  }, [acceptPlaylist]);

  useEffect(() => {
    const effectiveVolume = isMuted ? 0 : volume;
    synthRef.current?.setVolume(effectiveVolume);
    if (audioElRef.current) audioElRef.current.volume = effectiveVolume;
    if (ytPlayerRef.current?.setVolume) {
      try {
        ytPlayerRef.current.setVolume(effectiveVolume * 100);
        if (isMuted) ytPlayerRef.current.mute();
        else ytPlayerRef.current.unMute();
      } catch { /* ignore */ }
    }
  }, [volume, isMuted]);

  useEffect(() => { storePreference("wm_active_track_id", activeTrackId); }, [activeTrackId]);

  const runPlaylistSave = async (build: (actor: NavbarMusicActor, signal: AbortSignal, isCurrent: () => boolean) => Promise<TrackItem[]>) => {
    // Ref guard prevents same-render double clicks and overlapping uploads/saves.
    if (operationRef.current) return false;
    const actor = navbarMusicActor(useAuthStore.getState());
    if (!actor) { setUploadError("Cần phiên quản trị đã được xác thực để sửa danh sách nhạc."); return false; }
    const controller = new AbortController();
    const operation = { actor, controller };
    operationRef.current = operation;
    readRef.current?.abort();
    const isCurrent = () => mountedRef.current && !controller.signal.aborted && navbarMusicActorIsCurrent(actor, useAuthStore.getState());
    setIsUploading(true);
    setUploadError("");
    try {
      const proposed = await build(actor, controller.signal, isCurrent);
      const saved = await saveNavbarPlaylist(proposed, {
        isCurrent, signal: controller.signal,
        send: async (playlist, signal) => (await axiosClient.put("/api/upload/music/playlist", playlist,
          { signal, headers: { Authorization: `Bearer ${actor.token}` } })).data,
      });
      if (!isCurrent()) return false;
      acceptPlaylist(saved);
      return true;
    } catch (failure) {
      if (isCurrent()) setUploadError(failure instanceof Error ? failure.message : "Chưa lưu được danh sách nhạc. Vui lòng thử lại.");
      return false;
    } finally {
      if (operationRef.current === operation) {
        operationRef.current = null;
        if (mountedRef.current) setIsUploading(false);
      }
    }
  };

  const resolveAudioUrl = (raw: string) => {
    const url = navbarMusicUrl(raw);
    if (url.startsWith("https://")) return url;
    return `${(import.meta.env.VITE_API_URL || "").replace(/\/$/, "")}${url}`;
  };

  const playTrack = (track: TrackItem) => {
    activeTrackIdRef.current = track.id;
    setActiveTrackId(track.id);
    setUploadError("");
    const generation = ++playbackRef.current;
    try {
      if (track.type === "synth") {
        audioElRef.current?.pause();
        if (ytPlayerRef.current?.pauseVideo) {
          try { ytPlayerRef.current.pauseVideo(); } catch { /* ignore */ }
        }
        synthRef.current?.start();
        setIsPlaying(true);
      } else if (track.type === "youtube") {
        audioElRef.current?.pause();
        synthRef.current?.stop();
        const videoId = track.youtubeId || extractYouTubeId(track.url);
        if (!videoId) {
          setUploadError("Mã video YouTube không hợp lệ.");
          setIsPlaying(false);
          return;
        }
        const effectiveVol = isMuted ? 0 : Math.round(volume * 100);
        if (!ytPlayerRef.current && (window as any).YT?.Player) {
          ytPlayerRef.current = new (window as any).YT.Player("wm-navbar-youtube-player", {
            height: "1",
            width: "1",
            videoId,
            playerVars: { autoplay: 1, controls: 0, loop: 1, playlist: videoId },
            events: {
              onReady: (e: any) => {
                try {
                  e.target.setVolume(effectiveVol);
                  if (isMuted) e.target.mute(); else e.target.unMute();
                  e.target.playVideo();
                  if (mountedRef.current && playbackRef.current === generation) setIsPlaying(true);
                } catch { /* ignore */ }
              },
              onStateChange: (e: any) => {
                if (mountedRef.current && playbackRef.current === generation) {
                  if (e.data === 1) setIsPlaying(true);
                  else if (e.data === 2) setIsPlaying(false);
                  else if (e.data === 0) {
                    try { e.target.playVideo(); } catch { /* ignore */ }
                  }
                }
              },
              onError: () => {
                if (mountedRef.current && playbackRef.current === generation) {
                  setUploadError("Không thể phát video YouTube này. Vui lòng kiểm tra lại liên kết.");
                  setIsPlaying(false);
                }
              },
            },
          });
        } else if (ytPlayerRef.current?.loadVideoById) {
          try {
            ytPlayerRef.current.loadVideoById(videoId);
            ytPlayerRef.current.setVolume(effectiveVol);
            if (isMuted) ytPlayerRef.current.mute(); else ytPlayerRef.current.unMute();
            ytPlayerRef.current.playVideo();
            setIsPlaying(true);
          } catch {
            setUploadError("Trình phát YouTube chưa sẵn sàng.");
            setIsPlaying(false);
          }
        }
      } else if (track.url && audioElRef.current) {
        synthRef.current?.stop();
        if (ytPlayerRef.current?.pauseVideo) {
          try { ytPlayerRef.current.pauseVideo(); } catch { /* ignore */ }
        }
        const audio = audioElRef.current;
        audio.src = resolveAudioUrl(track.url);
        void audio.play().then(() => {
          if (mountedRef.current && playbackRef.current === generation) setIsPlaying(true);
        }).catch(() => {
          if (mountedRef.current && playbackRef.current === generation) {
            setUploadError("Không thể phát tệp âm thanh. Kiểm tra định dạng hoặc kết nối mạng.");
            setIsPlaying(false);
          }
        });
      }
    } catch {
      setUploadError("Trình duyệt chưa phát được âm thanh. Vui lòng thử lại.");
      setIsPlaying(false);
    }
  };

  const togglePlay = () => { if (isPlaying) stopPlayback(); else playTrack(activeTrack); };

  const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !isAdmin) return;
    if (!/\.mp3$/i.test(file.name) || file.size <= 0 || file.size > 12 * 1024 * 1024) {
      setUploadError("Chọn tệp MP3 có dung lượng tối đa 12 MB."); return;
    }
    await runPlaylistSave(async (actor, signal, isCurrent) => {
      const form = new FormData(); form.append("file", file);
      const uploaded = await axiosClient.post<{ url?: unknown }>("/api/upload/music", form,
        { signal, headers: { "Content-Type": "multipart/form-data", Authorization: `Bearer ${actor.token}` } });
      if (!isCurrent()) throw new Error("Phiên quản trị đã thay đổi.");
      return [...tracksRef.current, { id: crypto.randomUUID(), title: file.name.replace(/\.[^/.]+$/, ""),
        url: navbarMusicUrl(uploaded.data.url), type: "audio", isCustom: true }];
    });
  };



  const handleToggleHidden = async (id: string, event: React.MouseEvent) => {
    event.stopPropagation();
    const target = tracks.find(t => t.id === id);
    if (!target) return;
    const nextHidden = !target.hidden;
    const remainingVisible = tracks.filter(t => t.id !== id && !t.hidden);
    if (nextHidden && remainingVisible.length === 0) {
      setUploadError("Cần giữ ít nhất 1 bài hát hiển thị.");
      return;
    }
    if (activeTrackId === id && nextHidden) {
      stopPlayback();
    }
    await runPlaylistSave(async () => tracks.map(t => t.id === id ? { ...t, hidden: nextHidden } : t));
  };

  const handleDeleteTrack = async (id: string, event: React.MouseEvent) => {
    event.stopPropagation();
    if (!isAdmin || tracksRef.current.length <= 1) return;
    await runPlaylistSave(async () => tracksRef.current.filter(t => t.id !== id));
  };

  return (
    <div ref={containerRef} className="relative inline-flex items-center">
      <audio
        ref={audioElRef}
        loop
        onEnded={() => setIsPlaying(false)}
        onError={() => {
          setUploadError("Không thể kết nối tệp âm thanh.");
          setIsPlaying(false);
        }}
      />

      {/* Hidden YouTube player container */}
      <div
        id="wm-navbar-youtube-player"
        className="fixed -bottom-[999px] -right-[999px] w-[1px] h-[1px] opacity-0 pointer-events-none"
      />

      {/* Hidden file input for Admin uploading MP3 */}
      {isAdmin && (
        <input
          ref={fileInputRef}
          type="file"
          accept="audio/mpeg,.mp3"
          className="hidden"
          onChange={handleFileChange}
        />
      )}

      {/* Outermost Navbar Pill Button */}
      <div className="flex items-center rounded-full bg-white/10 hover:bg-white/15 border border-white/20 transition backdrop-blur-xs text-[#fcf6ea] shadow-xs">
        {/* Main Play / Pause Button */}
        <button
          type="button"
          onClick={togglePlay}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold hover:text-white transition"
          title={isPlaying ? `Đang phát: ${activeTrack.title} (Bấm để tạm dừng)` : "Bật nhạc nền đám cưới"}
          aria-label={isPlaying ? "Tạm dừng nhạc nền" : "Bật nhạc nền đám cưới"}
        >
          {isPlaying ? (
            <Pause size={13} className="fill-current text-rose-300" />
          ) : (
            <Play size={13} className="fill-current text-amber-200 ml-0.5" />
          )}

          {/* Equalizer animation when playing */}
          {isPlaying && (
            <span className="flex items-end gap-0.5 h-3 px-0.5" aria-hidden="true">
              <span className="w-0.5 h-2 bg-rose-300 animate-pulse rounded-full" />
              <span className="w-0.5 h-3 bg-amber-200 animate-pulse delay-75 rounded-full" />
              <span className="w-0.5 h-1.5 bg-rose-300 animate-pulse delay-150 rounded-full" />
            </span>
          )}

          <span className="hidden sm:inline max-w-[120px] truncate">
            {isPlaying ? activeTrack.title : "Nhạc cưới"}
          </span>
        </button>

        {/* Dropdown Toggle Button */}
        <button
          type="button"
          onClick={() => setIsOpen((prev) => !prev)}
          className="px-2 py-1.5 border-l border-white/15 hover:bg-white/10 rounded-r-full text-slate-300 hover:text-white transition"
          title="Mở danh sách nhạc và cài đặt"
          aria-label="Cài đặt nhạc nền"
          aria-expanded={isOpen}
        >
          <ChevronDown size={12} className={`transition-transform duration-150 ${isOpen ? "rotate-180" : ""}`} />
        </button>
      </div>

      {/* Dropdown Menu below Navbar */}
      {isOpen && (
        <div className="absolute right-0 top-full mt-2 w-80 rounded-2xl bg-white p-4 shadow-2xl border border-slate-200 z-50 text-slate-800 animate-in fade-in slide-in-from-top-1 duration-150 space-y-3.5">
          <div className="flex items-center justify-between border-b border-slate-100 pb-2">
            <div className="flex items-center gap-1.5 text-xs font-bold text-slate-900">
              <Sparkles size={14} className="text-rose-500" />
              <span>Giai điệu đám cưới lãng mạn</span>
            </div>
            <div className="flex items-center gap-2">
              {isAdmin && (
                <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-100 text-amber-800 border border-amber-200">
                  Admin
                </span>
              )}
              <button
                type="button"
                onClick={() => setIsOpen(false)}
                className="p-1 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100"
                aria-label="Đóng bảng nhạc"
              >
                <X size={14} />
              </button>
            </div>
          </div>

          {/* Volume Control */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-xs font-medium text-slate-600">
              <span className="flex items-center gap-1.5">
                <button
                  type="button"
                  onClick={() => setIsMuted((m) => !m)}
                  className="hover:text-rose-600"
                  title={isMuted ? "Bật âm thanh" : "Tắt tiếng"}
                  aria-label={isMuted ? "Bật âm thanh" : "Tắt tiếng"}
                >
                  {isMuted || volume === 0 ? <VolumeX size={15} /> : <Volume2 size={15} />}
                </button>
                <span>Âm lượng</span>
              </span>
              <span>{isMuted ? "Tắt tiếng" : `${Math.round(volume * 100)}%`}</span>
            </div>
            <input
              type="range"
              min="0"
              max="1"
              step="0.05"
              aria-label="Âm lượng nhạc nền"
              value={isMuted ? 0 : volume}
              onChange={(e) => {
                const nextVolume = navbarMusicVolume(e.target.value);
                storePreference("wm_bgm_volume", String(nextVolume));
                setVolume(nextVolume);
                if (isMuted) setIsMuted(false);
              }}
              className="w-full h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer accent-rose-500"
            />
          </div>

          {/* Playlist Section */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-bold text-slate-700 uppercase tracking-wider">
                Danh sách bài hát ({tracks.length})
              </span>
              {isAdmin && (
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={isUploading}
                  className="inline-flex items-center gap-1 text-[11px] font-semibold text-rose-600 hover:text-rose-700 hover:underline disabled:opacity-50"
                  title="Tải lên file nhạc MP3 từ máy của bạn"
                >
                  {isUploading ? (
                    <>
                      <Loader2 size={11} className="animate-spin" />
                      <span>Đang tải lên...</span>
                    </>
                  ) : (
                    <>
                      <UploadCloud size={12} />
                      <span>Tải file MP3</span>
                    </>
                  )}
                </button>
              )}
            </div>

            {uploadError && (
              <p role="alert" className="text-[11px] text-red-600 bg-red-50 p-2 rounded-lg border border-red-100">
                {uploadError}
              </p>
            )}

            {/* Track list items */}
            <div className="max-h-48 overflow-y-auto space-y-1 pr-1">
              {displayTracks.map((t) => {
                const isSelected = t.id === activeTrackId;
                return (
                  <div
                    key={t.id}
                    onClick={() => playTrack(t)}
                    className={`flex items-center justify-between px-2.5 py-2 rounded-xl text-xs cursor-pointer transition ${
                      isSelected
                        ? "bg-rose-50 border border-rose-200 text-rose-900 font-semibold shadow-2xs"
                        : "hover:bg-slate-50 text-slate-700 border border-transparent"
                    }`}
                  >
                    <div className="flex items-center gap-2 min-w-0">
                      {isSelected && isPlaying ? (
                        <Pause size={12} className="text-rose-600 shrink-0 fill-current" />
                      ) : (
                        <Music size={12} className={isSelected ? "text-rose-500 shrink-0" : "text-slate-400 shrink-0"} />
                      )}
                      <span className="truncate" title={t.title}>
                        {t.title}
                      </span>

                    </div>

                    <div className="flex items-center gap-1.5 shrink-0 ml-2">
                      {isSelected && (
                        <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-rose-200 text-rose-800">
                          {isPlaying ? "Đang phát" : "Đã chọn"}
                        </span>
                      )}
                      {t.hidden && (
                        <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-slate-200 text-slate-600">
                          Đã ẩn
                        </span>
                      )}
                      {isAdmin && (
                        <button
                          type="button"
                          onClick={(e) => handleToggleHidden(t.id, e)}
                          disabled={isUploading}
                          className="p-1 text-slate-400 hover:text-slate-700 rounded-md hover:bg-slate-100 transition"
                          title={t.hidden ? "Hiện lại bài hát" : "Ẩn bài hát"}
                          aria-label={t.hidden ? "Hiện lại bài hát" : "Ẩn bài hát"}
                        >
                          {t.hidden ? <Eye size={12} /> : <EyeOff size={12} />}
                        </button>
                      )}
                      {isAdmin && tracks.length > 1 && (
                        <button
                          type="button"
                          onClick={(e) => handleDeleteTrack(t.id, e)}
                          disabled={isUploading}
                          className="p-1 text-slate-400 hover:text-red-600 rounded-md hover:bg-red-50 transition"
                          title="Xóa bài hát khỏi danh sách"
                          aria-label={`Xóa ${t.title} khỏi danh sách nhạc`}
                        >
                          <Trash2 size={12} />
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>



          {isAdmin ? (
            <div className="pt-2 border-t border-slate-100 flex items-center justify-between">
              <Link
                to="/admin/dashboard?tab=appearance"
                onClick={() => setIsOpen(false)}
                className="inline-flex items-center gap-1.5 text-xs font-semibold text-rose-600 hover:text-rose-700 hover:underline"
              >
                <Palette size={13} />
                <span>Mở trang quản lý nền & hoa rơi →</span>
              </Link>
            </div>
          ) : (
            <p className="text-[10px] text-slate-400 leading-relaxed italic text-center">
              Lựa chọn bài hát và điều chỉnh âm lượng để thưởng thức âm nhạc ngày cưới cùng Wedding Match.
            </p>
          )}
        </div>
      )}
    </div>
  );
}
