import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";

export interface WeddingCartItem {
  id: string; // listingId
  listingId: string;
  title: string;
  slug?: string;
  vendorBrandName: string;
  vendorId?: string;
  categoryName: string;
  price: number;
  priceFormatted: string;
  unit?: string;
  imageUrl?: string | null;
  quantity: number;
  notes?: string;
  isCombo?: boolean;
  subServices?: string[];
  addedAt: string;
}

export type AddCartItemInput = {
  id: string;
  listingId: string;
  title: string;
  slug?: string;
  vendorBrandName: string;
  vendorId?: string;
  categoryName: string;
  price: number;
  priceFormatted: string;
  unit?: string;
  imageUrl?: string | null;
  notes?: string;
  isCombo?: boolean;
  subServices?: string[];
};

interface WeddingCartStore {
  items: WeddingCartItem[];
  addItem: (item: AddCartItemInput, quantity?: number) => void;
  removeItem: (id: string) => void;
  updateQuantity: (id: string, quantity: number) => void;
  updateNotes: (id: string, notes: string) => void;
  clearCart: () => void;
  hasItem: (id: string) => boolean;
  getItemCount: () => number;
  getTotalAmount: () => number;
}

const safeStorage = {
  getItem: (name: string) => {
    if (typeof window === "undefined" || typeof localStorage === "undefined") return null;
    try {
      return localStorage.getItem(name);
    } catch {
      return null;
    }
  },
  setItem: (name: string, value: string) => {
    if (typeof window === "undefined" || typeof localStorage === "undefined") return;
    try {
      localStorage.setItem(name, value);
    } catch {
      // ignore quota or access errors
    }
  },
  removeItem: (name: string) => {
    if (typeof window === "undefined" || typeof localStorage === "undefined") return;
    try {
      localStorage.removeItem(name);
    } catch {
      // ignore
    }
  },
};

export const useWeddingCartStore = create<WeddingCartStore>()(
  persist(
    (set, get) => ({
      items: [],

      addItem: (itemInput: AddCartItemInput, quantity = 1) => {
        const { items } = get();
        const existingIndex = items.findIndex((i) => i.id === itemInput.id);

        if (existingIndex >= 0) {
          const updated = [...items];
          const curr = updated[existingIndex];
          updated[existingIndex] = {
            ...curr,
            quantity: curr.quantity + quantity,
          };
          set({ items: updated });
        } else {
          const newItem: WeddingCartItem = {
            ...itemInput,
            quantity: Math.max(1, quantity),
            addedAt: new Date().toISOString(),
          };
          set({ items: [newItem, ...items] });
        }
      },

      removeItem: (id: string) => {
        set({ items: get().items.filter((i) => i.id !== id) });
      },

      updateQuantity: (id: string, quantity: number) => {
        if (quantity <= 0) {
          set({ items: get().items.filter((i) => i.id !== id) });
          return;
        }
        set({
          items: get().items.map((i) =>
            i.id === id ? { ...i, quantity: Math.min(99, quantity) } : i
          ),
        });
      },

      updateNotes: (id: string, notes: string) => {
        set({
          items: get().items.map((i) =>
            i.id === id ? { ...i, notes } : i
          ),
        });
      },

      clearCart: () => {
        set({ items: [] });
      },

      hasItem: (id: string) => {
        return (get().items || []).some((i) => i.id === id);
      },

      getItemCount: () => {
        return (get().items || []).reduce((sum, item) => sum + (item.quantity || 1), 0);
      },

      getTotalAmount: () => {
        return (get().items || []).reduce(
          (sum, item) => sum + (item.price || 0) * (item.quantity || 1),
          0
        );
      },
    }),
    {
      name: "wedding-match-unified-cart-v2",
      storage: createJSONStorage(() => safeStorage),
    }
  )
);
