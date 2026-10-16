import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { EMPTY_CART, type ComboCart } from "../features/journey/combo/cartModel";

// Separate each account's local editing cart. All submitted drafts/orders are persisted by the API.
export const useComboCartStore = create<{
  carts: Record<string, ComboCart>;
  update: (scope: string, update: Partial<ComboCart> | ((cart: ComboCart) => ComboCart)) => void;
  clear: (scope: string) => void;
}>()(persist((set) => ({
  carts: {},
  update: (scope, update) => set((state) => {
    const previous = state.carts[scope] ?? EMPTY_CART;
    return { carts: { ...state.carts, [scope]: typeof update === "function" ? update(previous) : { ...previous, ...update } } };
  }),
  clear: (scope) => set((state) => ({ carts: { ...state.carts, [scope]: { ...EMPTY_CART } } })),
}), { name: "wedding-match-combo-carts-v1", storage: createJSONStorage(() => localStorage), version: 1 }));
