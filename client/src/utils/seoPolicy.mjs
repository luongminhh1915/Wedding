const publicPages = new Map([
  ["/", {
    title: "Wedding Match | Lên kế hoạch cưới cùng nhau",
    description: "Lên kế hoạch cưới, quản lý ngân sách, tạo thiệp online và kết nối dịch vụ cưới trong một không gian dành cho hai bạn.",
    canonicalPath: "/",
  }],
  ["/browse", {
    title: "Khám phá dịch vụ cưới | Wedding Match",
    description: "Tìm nhà cung cấp và dịch vụ cưới phù hợp cho ngày vui của hai bạn trên Wedding Match.",
    canonicalPath: "/browse",
  }],
  ["/combo", {
    title: "Combo cưới | Wedding Match",
    description: "Khám phá các gói dịch vụ cưới và lựa chọn phương án phù hợp với kế hoạch của hai bạn.",
    canonicalPath: "/combo",
  }],
  ["/cam-nang", {
    title: "Cẩm nang cưới | Wedding Match",
    description: "Tham khảo kiến thức và gợi ý hữu ích để chuẩn bị đám cưới chu đáo hơn.",
    canonicalPath: "/cam-nang",
  }],
  ["/invitation", {
    title: "Mẫu thiệp cưới | Wedding Match",
    description: "Xem các mẫu thiệp cưới trực tuyến dành cho ngày vui của hai bạn.",
    canonicalPath: "/invitation",
  }],
]);

const SITE_ORIGIN = "https://weddingmatch.website";

function normalizePath(pathname) {
  const pathOnly = (pathname || "/").split(/[?#]/, 1)[0] || "/";
  if (pathOnly === "/") return "/";
  return pathOnly.replace(/\/+$/, "");
}

/** Returns safe, indexable metadata only for explicitly public catalog/content routes. */
export function getSeoMetadata(pathname) {
  const path = normalizePath(pathname);

  if (path === "/vendors" || path === "/wiki") {
    return { ...publicPages.get(path === "/vendors" ? "/browse" : "/cam-nang"), indexable: true };
  }

  const exact = publicPages.get(path);
  if (exact) return { ...exact, indexable: true };

  const segments = path.split("/").filter(Boolean);
  if (segments.length === 2 && segments[0] === "listings") {
    return {
      title: "Dịch vụ cưới | Wedding Match",
      description: "Xem thông tin nhà cung cấp và dịch vụ cưới trên Wedding Match.",
      canonicalPath: path,
      indexable: true,
    };
  }

  if (segments.length === 2 && segments[0] === "combo") {
    return {
      title: "Chi tiết combo cưới | Wedding Match",
      description: "Xem thông tin gói dịch vụ cưới trên Wedding Match.",
      canonicalPath: path,
      indexable: true,
    };
  }

  if (segments.length === 2 && segments[0] === "wiki") {
    return {
      title: "Cẩm nang cưới | Wedding Match",
      description: "Tham khảo kiến thức và gợi ý hữu ích để chuẩn bị đám cưới.",
      canonicalPath: path,
      indexable: true,
    };
  }

  // This includes private invitation slugs, account pages, dashboards and unknown routes.
  return { indexable: false };
}

export function getCanonicalUrl(pathname) {
  const metadata = getSeoMetadata(pathname);
  return metadata.indexable && metadata.canonicalPath
    ? `${SITE_ORIGIN}${metadata.canonicalPath}`
    : null;
}
