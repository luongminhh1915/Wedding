import { create } from "zustand";
import { persist } from "zustand/middleware";

export interface CompareItem {
  id: string;
  title: string;
  vendorBrandName: string;
  vendorId?: string;
  priceAmount: number;
  priceFormatted: string;
  rating: number;
  totalReviews: number;
  location: string;
  style: string;
  categoryName: string;
  imageUrl?: string | null;
  prepTime?: string;
  isVerified?: boolean;
}

export interface CompareItemInput {
  id: string;
  title: string;
  vendorBrandName?: string;
  vendorId?: string;
  startingPrice?: number;
  price?: number;
  basePrice?: number | null;
  minPrice?: number;
  averageRating?: number;
  reviewCount?: number;
  location?: string;
  categoryName?: string;
  thumbnailUrl?: string;
  primaryImageUrl?: string;
  description?: string;
}

interface CompareStore {
  items: CompareItem[];
  addItem: (item: CompareItem) => boolean;
  removeItem: (id: string) => void;
  clear: () => void;
  hasItem: (id: string) => boolean;
}

export const useCompareStore = create<CompareStore>()(
  persist(
    (set, get) => ({
      items: [],
      addItem: (item: CompareItem) => {
        const { items } = get();
        if (items.some((i) => i.id === item.id)) return true;
        if (items.length >= 4) return false;
        set({ items: [...items, item] });
        return true;
      },
      removeItem: (id: string) => {
        set({ items: get().items.filter((i) => i.id !== id) });
      },
      clear: () => set({ items: [] }),
      hasItem: (id: string) => get().items.some((i) => i.id === id),
    }),
    {
      name: "wedding-match-compare-items",
    }
  )
);

export function listingToCompareItem(listing: CompareItemInput): CompareItem {
  const t = (listing.title + " " + (listing.description || "")).toLowerCase();
  let style = "Hiện đại";
  if (t.includes("cổ điển") || t.includes("vintage") || t.includes("retro")) style = "Cổ điển";
  else if (t.includes("lãng mạn") || t.includes("hoa") || t.includes("romantic")) style = "Lãng mạn";
  else if (t.includes("tối giản") || t.includes("minimal")) style = "Tối giản";
  else if (t.includes("sang trọng") || t.includes("luxury") || t.includes("premium")) style = "Sang trọng";
  else if (t.includes("sân vườn") || t.includes("ngoài trời") || t.includes("garden")) style = "Sân vườn";
  else if (t.includes("boho") || t.includes("bohemian")) style = "Boho";

  let prepTime = "2-3 tháng";
  const cat = listing.categoryName || "";
  if (cat.includes("Váy") || cat.includes("Trang phục")) prepTime = "1-2 tháng";
  else if (cat.includes("Tiệc") || cat.includes("Trung tâm")) prepTime = "3-6 tháng";
  else if (cat.includes("Trang trí")) prepTime = "1-3 tháng";

  const priceAmount = listing.startingPrice ?? listing.price ?? listing.basePrice ?? listing.minPrice ?? 10000000;
  const priceFormatted = `Từ ${priceAmount.toLocaleString("vi-VN")}đ`;

  return {
    id: listing.id,
    title: listing.title,
    vendorBrandName: listing.vendorBrandName || "Nhà cung cấp",
    vendorId: listing.vendorId,
    priceAmount,
    priceFormatted,
    rating: listing.averageRating || 4.8,
    totalReviews: listing.reviewCount || 12,
    location: listing.location || "Toàn quốc",
    style,
    categoryName: listing.categoryName || "Dịch vụ cưới",
    imageUrl: listing.primaryImageUrl ?? listing.thumbnailUrl,
    prepTime,
    isVerified: true,
  };
}
