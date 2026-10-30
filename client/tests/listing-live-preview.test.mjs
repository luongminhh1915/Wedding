import { test } from "node:test";
import assert from "node:assert/strict";

// Test formatting and fallback presentation rules for live preview
function formatPreviewPrice(minPrice, maxPrice) {
  const numMin = minPrice !== '' && minPrice != null ? Number(minPrice) : null;
  const numMax = maxPrice !== '' && maxPrice != null ? Number(maxPrice) : null;

  if (numMin != null && numMax != null && !Number.isNaN(numMin) && !Number.isNaN(numMax)) {
    if (numMin === numMax) {
      return `${numMin.toLocaleString('vi-VN')} ₫`;
    }
    return `${numMin.toLocaleString('vi-VN')} ₫ – ${numMax.toLocaleString('vi-VN')} ₫`;
  }
  if (numMin != null && !Number.isNaN(numMin)) {
    return `Từ ${numMin.toLocaleString('vi-VN')} ₫`;
  }
  return 'Chưa đặt giá';
}

function resolvePreviewDisplay(title, categoryName, location, brandName) {
  return {
    title: (title || '').trim() || 'Tiêu đề gói dịch vụ sẽ hiển thị ở đây',
    category: (categoryName || '').trim() || 'Danh mục dịch vụ',
    location: (location || '').trim() || 'Chưa cập nhật địa điểm',
    brand: (brandName || '').trim() || 'Thương hiệu của bạn',
  };
}

test("live preview formats equal min and max as single price with currency symbol", () => {
  assert.equal(formatPreviewPrice(15000000, 15000000), "15.000.000 ₫");
  assert.equal(formatPreviewPrice("20000000", "20000000"), "20.000.000 ₫");
});

test("live preview formats min and max price range correctly", () => {
  assert.equal(formatPreviewPrice(12500000, 25000000), "12.500.000 ₫ – 25.000.000 ₫");
  assert.equal(formatPreviewPrice("5000000", "10000000"), "5.000.000 ₫ – 10.000.000 ₫");
});

test("live preview handles open-ended or missing prices safely", () => {
  assert.equal(formatPreviewPrice(10000000, null), "Từ 10.000.000 ₫");
  assert.equal(formatPreviewPrice("", ""), "Chưa đặt giá");
  assert.equal(formatPreviewPrice(null, null), "Chưa đặt giá");
});

test("live preview applies friendly Vietnamese fallbacks for empty field values", () => {
  const empty = resolvePreviewDisplay("", "", "", "");
  assert.equal(empty.title, "Tiêu đề gói dịch vụ sẽ hiển thị ở đây");
  assert.equal(empty.category, "Danh mục dịch vụ");
  assert.equal(empty.location, "Chưa cập nhật địa điểm");
  assert.equal(empty.brand, "Thương hiệu của bạn");

  const filled = resolvePreviewDisplay("Sảnh Tiệc Hoàng Gia", "Tiệc cưới", "TP. Hồ Chí Minh", "Trung Tâm Cưới Diamond");
  assert.equal(filled.title, "Sảnh Tiệc Hoàng Gia");
  assert.equal(filled.category, "Tiệc cưới");
  assert.equal(filled.location, "TP. Hồ Chí Minh");
  assert.equal(filled.brand, "Trung Tâm Cưới Diamond");
});

test("live preview handles rich service details metadata without throwing", () => {
  const details = {
    shortDescription: "Gói tiệc cao cấp",
    priceUnit: "bàn 10 khách",
    highlights: "Âm thanh ánh sáng chuẩn 5 sao",
    includedItems: "Thực đơn 8 món, MC tiệc cưới",
    excludedItems: "Bia rượu nhập khẩu",
    duration: "4 giờ",
    process: "Đặt cọc - Thử món - Tổ chức",
    terms: "Báo trước 30 ngày nếu đổi ngày",
    portfolioUrls: ["https://example.com/gallery1", "https://example.com/gallery2"],
    videoUrl: "https://youtube.com/watch?v=sample",
  };
  assert.ok(details.includedItems.includes("Thực đơn"));
  assert.equal(details.portfolioUrls.length, 2);
  assert.equal(details.videoUrl.startsWith("https://"), true);
});

