import { useState } from "react";
import { BadgeCheck, Check, Plus } from "lucide-react";
import type { CatalogOption } from "../comboApi";
import { vnd } from "../format";
import { ComboDialog } from "./ComboDialog";
import { ComboPhoto } from "./ComboPhoto";
import {
  estimateLine,
  quantityFor,
  unitName,
  type CartSelection,
} from "./cartModel";

export function ServiceConfigurator({
  item,
  categoryId,
  guests,
  current,
  replacedTitle,
  onSave,
  onClose,
  onBrowseAlternatives,
}: {
  item: CatalogOption;
  categoryId: string;
  guests: number;
  current?: CartSelection;
  replacedTitle?: string;
  onSave: (selection: CartSelection) => void;
  onClose: () => void;
  onBrowseAlternatives: () => void;
}) {
  const [selection, setSelection] = useState<CartSelection>(
    () =>
      current ?? {
        categoryId,
        serviceItemId: item.serviceItemId,
        variantIds: [],
        addOnIds: [],
        hours: 1,
      },
  );
  const toggle = (field: "variantIds" | "addOnIds", id: string) =>
    setSelection((s) => ({
      ...s,
      [field]: s[field].includes(id)
        ? s[field].filter((x) => x !== id)
        : [...s[field], id],
    }));
  const quantity = quantityFor(item.unit, guests, selection.hours);
  return (
    <ComboDialog title="Chọn dịch vụ cho combo" onClose={onClose}>
      <ComboPhoto
        className="combo-config-image"
        sources={[item.imageUrl, ...(item.galleryUrls ?? [])]}
        alt={`${item.title} — ${item.vendorName}`}
      />
      <div className="combo-config-title">
        <p>
          <BadgeCheck size={14} /> {item.vendorName}
        </p>
        <h3>{item.title}</h3>
        <span>
          {vnd(item.price)} / {unitName[item.unit]} · {item.minGuests}–
          {item.maxGuests} khách
        </span>
      </div>
      <button
        type="button"
        className="combo-text-button"
        onClick={onBrowseAlternatives}
      >
        Đổi sang dịch vụ khác của NCC này →
      </button>
      {replacedTitle && (
        <p className="combo-notice">
          Dịch vụ này sẽ thay “{replacedTitle}” trong cùng hạng mục, không cộng
          trùng chi phí.
        </p>
      )}
      {item.variants.length > 0 && (
        <fieldset className="combo-config-options">
          <legend>Nâng cấp gói dịch vụ</legend>
          <p>Chọn thêm những phần bạn cần.</p>
          {item.variants.map((v) => (
            <label key={v.id}>
              <input
                type="checkbox"
                checked={!!v.id && selection.variantIds.includes(v.id)}
                onChange={() => v.id && toggle("variantIds", v.id)}
              />
              <span>{v.name}</span>
              <strong>
                {v.priceDelta >= 0 ? "+" : ""}
                {vnd(v.priceDelta)}/{unitName[item.unit]}
              </strong>
            </label>
          ))}
        </fieldset>
      )}
      {item.addOns.length > 0 && (
        <fieldset className="combo-config-options">
          <legend>Dịch vụ đi kèm</legend>
          {item.addOns.map((a) => (
            <label key={a.id}>
              <input
                type="checkbox"
                checked={!!a.id && selection.addOnIds.includes(a.id)}
                onChange={() => a.id && toggle("addOnIds", a.id)}
              />
              <span>{a.name}</span>
              <strong>
                +{vnd(a.price)}
                {a.perUnit ? `/${unitName[item.unit]}` : ""}
              </strong>
            </label>
          ))}
        </fieldset>
      )}
      {item.unit === "Hour" && (
        <label className="combo-field">
          Số giờ sử dụng
          <input
            className="input"
            type="number"
            min={1}
            max={24}
            value={selection.hours}
            onChange={(e) =>
              setSelection({
                ...selection,
                hours: Math.max(1, Math.min(24, Number(e.target.value) || 1)),
              })
            }
          />
        </label>
      )}
      <p className="combo-policy">
        <strong>Chính sách hủy</strong>
        {item.cancellationPolicy}
      </p>
      <div className="combo-config-footer">
        <div>
          <span>
            {guests > 0
              ? `${quantity} ${unitName[item.unit]} · trước ưu đãi`
              : `Đơn giá theo ${unitName[item.unit]}`}
          </span>
          <strong>
            {guests > 0
              ? vnd(estimateLine(item, selection, guests))
              : `${vnd(item.price)}/${unitName[item.unit]}`}
          </strong>
        </div>
        <button
          type="button"
          className="combo-primary"
          onClick={() => onSave(selection)}
        >
          {current || replacedTitle ? <Check size={16} /> : <Plus size={16} />}
          {current
            ? "Lưu tùy chỉnh"
            : replacedTitle
              ? "Thay dịch vụ"
              : "Thêm vào giỏ"}
        </button>
      </div>
    </ComboDialog>
  );
}
