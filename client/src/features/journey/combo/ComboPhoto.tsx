import { useState, type ReactNode } from "react";
import { ImageIcon } from "lucide-react";
import { comboMediaSources } from "./comboMedia";

export function ComboPhoto({
  sources,
  alt,
  className = "",
  placeholder,
  eager = false,
}: {
  sources: (string | null | undefined)[];
  alt: string;
  className?: string;
  placeholder?: ReactNode;
  eager?: boolean;
}) {
  const [failed, setFailed] = useState<string[]>([]);
  const source = comboMediaSources(sources).find(
    (url) => !failed.includes(url),
  );
  return (
    <div className={`combo-photo ${className}`}>
      {source ? (
        <img
          src={source}
          alt={alt}
          loading={eager ? "eager" : "lazy"}
          decoding="async"
          onError={() => setFailed((previous) => [...previous, source])}
        />
      ) : (
        <span className="combo-photo-placeholder">
          {placeholder ?? (
            <>
              <ImageIcon size={28} strokeWidth={1.3} />
              <span>Chưa có ảnh</span>
            </>
          )}
        </span>
      )}
    </div>
  );
}
