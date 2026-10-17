import { comboMediaSources } from "../combo/comboMedia";
import type { OrderMedia } from "./customerOrderApi";

export const orderMediaPhotos = (media?: OrderMedia) =>
  comboMediaSources([media?.imageUrl, ...(media?.galleryUrls ?? [])]);
