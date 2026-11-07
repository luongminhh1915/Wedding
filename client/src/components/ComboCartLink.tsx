import { ShoppingBag } from "lucide-react";
import { Link } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { useWeddingCartStore } from "../store/weddingCartStore";

export default function ComboCartLink() {
  const user = useAuthStore((s) => s.user);
  const count = useWeddingCartStore((s) =>
    (s.items ?? []).reduce((sum, i) => sum + (i.quantity || 1), 0)
  );

  if (user && user.role !== "Customer") return null;

  return (
    <Link
      className="wm-cart-link"
      to="/cart"
      aria-label={`Giỏ hàng dịch vụ cưới, ${count} dịch vụ`}
    >
      <ShoppingBag size={18} />
      <span>{count}</span>
    </Link>
  );
}
