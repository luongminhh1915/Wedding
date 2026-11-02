import { useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { ComboDialog } from "../combo/ComboDialog";
import { comboMediaSources } from "../combo/comboMedia";
import { OrderPhoto } from "./OrderUi";

export interface OrderGallerySelection {
  title: string;
  sources: string[];
}
export function OrderGallery({
  title,
  sources,
  onClose,
}: OrderGallerySelection & { onClose: () => void }) {
  const photos = comboMediaSources(sources);
  const [index, setIndex] = useState(0);
  const visibleIndex = Math.min(index, Math.max(0, photos.length - 1));
  const step = (direction: number) => {
    if (photos.length)
      setIndex((visibleIndex + direction + photos.length) % photos.length);
  };
  return (
    <ComboDialog title={title} wide onClose={onClose}>
      <div
        onKeyDown={(event) => {
          if (event.key === "ArrowLeft") {
            event.preventDefault();
            step(-1);
          }
          if (event.key === "ArrowRight") {
            event.preventDefault();
            step(1);
          }
        }}
      >
        <div className="co-dialog-photo">
          <OrderPhoto
            sources={photos.length ? [photos[visibleIndex]] : []}
            alt={`${title} · ảnh ${visibleIndex + 1}`}
          />
        </div>
        <div className="co-photo-controls">
          <button
            className="co-button secondary"
            disabled={photos.length < 2}
            onClick={() => step(-1)}
            aria-label="Ảnh trước"
          >
            <ChevronLeft size={16} />
          </button>
          <span role="status">
            {photos.length ? visibleIndex + 1 : 0} / {photos.length}
          </span>
          <button
            className="co-button secondary"
            disabled={photos.length < 2}
            onClick={() => step(1)}
            aria-label="Ảnh tiếp"
          >
            <ChevronRight size={16} />
          </button>
        </div>
        {photos.length > 1 && (
          <div className="co-detail-gallery">
            {photos.map((photo, photoIndex) => (
              <button
                key={photo}
                onClick={() => setIndex(photoIndex)}
                aria-label={`Xem ảnh ${photoIndex + 1}`}
                aria-pressed={photoIndex === visibleIndex}
              >
                <OrderPhoto
                  sources={[photo]}
                  alt={`Ảnh dịch vụ ${photoIndex + 1}`}
                />
              </button>
            ))}
          </div>
        )}
        <p className="co-summary-note">
          Ảnh do NCC công bố cho dịch vụ liên quan. Phạm vi đã chọn được ghi
          trong báo giá / hợp đồng của bạn.
        </p>
      </div>
    </ComboDialog>
  );
}
