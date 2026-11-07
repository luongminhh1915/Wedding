import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { Sparkles, Pause, Play } from "lucide-react";
import axiosClient from "../../../services/axiosClient";

interface PetalItem {
  id: number;
  left: number; // percentage across screen (0 - 98)
  size: number; // width in pixels (14 - 28)
  fallDuration: number; // seconds (7 - 14)
  fallDelay: number; // seconds
  swayDuration: number; // seconds (3 - 6)
  swayOffset: number; // horizontal drift px (20 - 55)
  gradId: string;
  opacity: number;
}

// Classical romantic cherry/rose petal gradients from original design
const GRADIENTS = [
  { id: "petalGrad1", start: "#ffd6e0", middle: "#ffadc2", end: "#f87593" },
  { id: "petalGrad2", start: "#ffe5ec", middle: "#ffc2d1", end: "#fb9ab0" },
  { id: "petalGrad3", start: "#fff0f3", middle: "#ffccd5", end: "#ff97b7" },
  { id: "petalGrad4", start: "#fceade", middle: "#f4acb7", end: "#e07a8c" },
];

const readPreference = (key: string) => { try { return localStorage.getItem(key); } catch { return null; } };
const savePreference = (key: string, value: string) => { try { localStorage.setItem(key, value); } catch { /* Device preferences may be unavailable. */ } };
const reducedMotion = () => typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const safeDensity = (value: unknown) => {
  const number = typeof value === "number" ? value : Number(value);
  return Number.isFinite(number) ? Math.max(6, Math.min(24, Math.round(number / 2) * 2)) : 14;
};
const STYLE_PALETTES: Record<PetalStyle, readonly [string, string, string][]> = {
  blush: GRADIENTS.map(g => [g.start, g.middle, g.end] as [string, string, string]),
  red: [["#f5bec0", "#d97783", "#a43d53"], ["#f6c9c3", "#df8b87", "#b34d59"]],
  white: [["#fffdf5", "#f4eadc", "#e0cbb8"], ["#fffaf6", "#f2e4dd", "#dfc5bf"]],
  mixed: [["#ffd6e0", "#ffadc2", "#f87593"], ["#fffdf5", "#f4eadc", "#e0cbb8"], ["#f5bec0", "#d97783", "#a43d53"]],
};

export function FallingPetals() {
  // Always AUTO-ON by default for visitors, unless explicitly turned off by toggle
  const [enabled, setEnabled] = useState(() => {
    const saved = readPreference("wm_petals_enabled");
    return saved !== null ? saved === "true" : !reducedMotion();
  });

  const [density, setDensity] = useState(() => {
    const saved = readPreference("wm_petals_density");
    // Default 14 petals: delicate, subtle and gentle (reduced count as requested)
    return saved ? safeDensity(saved) : 14;
  });
  const [style, setStyle] = useState<PetalStyle>("blush");
  const [speed, setSpeed] = useState<"slow" | "normal" | "fast">("normal");
  const gradients = GRADIENTS.map((gradient, index) => {
    const [start, middle, end] = STYLE_PALETTES[style][index % STYLE_PALETTES[style].length];
    return { ...gradient, start, middle, end };
  });

  const [mounted, setMounted] = useState(false);
  const [petals, setPetals] = useState<PetalItem[]>([]);

  // Sync settings with server if available
  useEffect(() => {
    const controller = new AbortController();
    axiosClient
      .get<{ petalsEnabled?: boolean; petalsDensity?: number; petalsStyle?: string; petalsSpeed?: string }>("/api/upload/appearance/settings", { signal: controller.signal })
      .then((res) => {
        if (!controller.signal.aborted && res.data) {
          if (typeof res.data.petalsDensity === "number") setDensity(safeDensity(res.data.petalsDensity));
          if (typeof res.data.petalsEnabled === "boolean" && readPreference("wm_petals_enabled") === null)
            setEnabled(res.data.petalsEnabled && !reducedMotion());
          if (["blush", "red", "white", "mixed"].includes(res.data.petalsStyle ?? "")) setStyle(res.data.petalsStyle as PetalStyle);
          if (["slow", "normal", "fast"].includes(res.data.petalsSpeed ?? "")) setSpeed(res.data.petalsSpeed as "slow" | "normal" | "fast");
        }
      })
      .catch(() => {
        // Fallback to local settings
      });
    return () => {
      controller.abort();
    };
  }, []);

  useEffect(() => {
    setMounted(true);
    // Bounded between 6 and 24 petals for a clean, non-cluttered romantic feel
    const count = safeDensity(density);
    const pace = speed === "slow" ? 1.35 : speed === "fast" ? 0.75 : 1;
    const generated: PetalItem[] = Array.from({ length: count }, (_, i) => ({
      id: i,
      left: Math.random() * 98,
      size: 14 + Math.random() * 14,
      fallDuration: (7 + Math.random() * 7) * pace,
      fallDelay: -(Math.random() * 12), // negative delay so petals are already mid-flight on page load
      swayDuration: (3 + Math.random() * 3.5) * pace,
      swayOffset: 25 + Math.random() * 35,
      gradId: GRADIENTS[i % GRADIENTS.length].id,
      opacity: 0.65 + Math.random() * 0.3,
    }));
    setPetals(generated);
  }, [density, speed]);

  if (!mounted) return null;

  const content = (
    <>
      <style>{`
        @keyframes wmPetalFallKeyframes {
          0% {
            top: -50px;
            transform: rotate(0deg) rotateY(0deg);
          }
          100% {
            top: 105vh;
            transform: rotate(360deg) rotateY(360deg);
          }
        }
        @keyframes wmPetalSwayKeyframes {
          0%, 100% {
            transform: translateX(0px);
          }
          50% {
            transform: translateX(var(--sway-distance, 35px));
          }
        }
        .wm-petal-container {
          position: fixed;
          top: 0;
          left: 0;
          width: 100vw;
          height: 100vh;
          pointer-events: none !important;
          z-index: 99999;
          overflow: hidden;
        }
        .wm-petal-item {
          position: absolute;
          will-change: top, transform;
          animation-name: wmPetalFallKeyframes, wmPetalSwayKeyframes;
          animation-iteration-count: infinite, infinite;
          animation-timing-function: linear, ease-in-out;
        }
      `}</style>

      {/* SVG Definitions for realistic petal gradients */}
      <svg width="0" height="0" className="absolute pointer-events-none" aria-hidden="true">
        <defs>
          {gradients.map((g) => (
            <linearGradient key={g.id} id={g.id} x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor={g.start} stopOpacity="0.9" />
              <stop offset="60%" stopColor={g.middle} stopOpacity="0.85" />
              <stop offset="100%" stopColor={g.end} stopOpacity="0.7" />
            </linearGradient>
          ))}
        </defs>
      </svg>

      {/* Falling Petals Layer */}
      {enabled && (
        <div className="wm-petal-container" aria-hidden="true">
          {petals.map((p) => (
            <div
              key={p.id}
              className="wm-petal-item"
              style={
                {
                  left: `${p.left}%`,
                  width: `${p.size}px`,
                  height: `${p.size * 1.35}px`,
                  opacity: p.opacity,
                  animationDuration: `${p.fallDuration}s, ${p.swayDuration}s`,
                  animationDelay: `${p.fallDelay}s, ${p.fallDelay * 0.4}s`,
                  "--sway-distance": `${p.swayOffset}px`,
                } as React.CSSProperties
              }
            >
              <svg viewBox="0 0 30 40" className="w-full h-full drop-shadow-xs">
                {/* Organic curved petal shape */}
                <path
                  d="M15 1 C25 6 30 22 15 39 C0 22 5 6 15 1 Z"
                  fill={`url(#${p.gradId})`}
                />
              </svg>
            </div>
          ))}
        </div>
      )}

      {/* Floating Toggle Button (Bottom Right) */}
      <button
        type="button"
        onClick={() => {
          const next = !enabled;
          setEnabled(next);
          savePreference("wm_petals_enabled", String(next));
        }}
        className="wm-petals-toggle fixed bottom-5 right-5 z-50 inline-flex items-center gap-1.5 rounded-full border border-rose-300/80 bg-white/95 px-3 py-1.5 text-[11px] font-semibold text-rose-800 shadow-lg backdrop-blur-md transition hover:bg-rose-50 hover:text-rose-900"
        title={enabled ? "Tạm dừng hiệu ứng cánh hoa rơi" : "Bật lại hiệu ứng cánh hoa rơi"}
        aria-label={enabled ? "Tắt cánh hoa" : "Bật cánh hoa"}
      >
        <Sparkles size={13} className={enabled ? "animate-spin text-rose-500" : "text-rose-400"} />
        <span>Cánh hoa: {enabled ? "Bật" : "Tắt"}</span>
        {enabled ? <Pause size={10} /> : <Play size={10} />}
      </button>
    </>
  );

  return createPortal(content, document.body);
}
export type PetalStyle = "blush" | "red" | "white" | "mixed";
