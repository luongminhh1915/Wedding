import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import {
  EMPTY_CART,
  type ComboCart,
} from "../features/journey/combo/cartModel";
import { useAuthStore } from "./authStore";

// Separate each account's local editing cart. All submitted drafts/orders are persisted by the API.
export const useComboCartStore = create<{
  carts: Record<string, ComboCart>;
  update: (
    scope: string,
    update: Partial<ComboCart> | ((cart: ComboCart) => ComboCart),
  ) => void;
  clear: (scope: string) => void;
}>()(
  persist(
    (set) => ({
      carts: {},
      update: (scope, update) =>
        set((state) => {
          const previous = state.carts[scope] ?? EMPTY_CART;
          return {
            carts: {
              ...state.carts,
              [scope]:
                typeof update === "function"
                  ? update(previous)
                  : { ...previous, ...update },
            },
          };
        }),
      clear: (scope) =>
        set((state) => ({
          carts: { ...state.carts, [scope]: { ...EMPTY_CART } },
        })),
    }),
    {
      name: "wedding-match-combo-carts-v1",
      storage: createJSONStorage(() => localStorage),
      version: 1,
    },
  ),
);

const unsubscribe = useAuthStore.subscribe((state, previous) => {
  if (!state.user || previous.user || state.user.role !== "Customer") return;
  const store = useComboCartStore.getState();
  const guest = store.carts.guest;
  if (
    guest?.selections.length &&
    !store.carts[state.user.id]?.selections.length
  ) {
    store.update(state.user.id, { ...guest });
    store.clear("guest");
  }
});
if (import.meta.hot) import.meta.hot.dispose(unsubscribe);
