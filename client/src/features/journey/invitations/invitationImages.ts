export const isInvitationImageUrl = (value: unknown): value is string =>
  typeof value === "string" &&
  (/^https?:\/\//i.test(value) ||
    /^\/uploads\/invitations\/[a-f0-9]{32}\/[a-f0-9]{32}\.(jpg|png|webp)$/.test(
      value,
    ));

/** Relative upload URLs survive API host changes; external legacy photographs remain untouched. */
export function invitationImageSrc(value?: string, apiBase = import.meta.env?.VITE_API_URL || "") {
  if (!value) return "";
  if (value.startsWith("/uploads/"))
    return `${apiBase.replace(/\/$/, "")}${value}`;
  return value;
}

/** Browser decoding normalizes phone orientation, removes location metadata and keeps web uploads compact. */
export async function prepareInvitationImage(file: File): Promise<File> {
  if (
    !["image/jpeg", "image/png", "image/webp"].includes(file.type) ||
    !/\.(jpe?g|png|webp)$/i.test(file.name)
  )
    throw new Error(
      "Chỉ hỗ trợ JPG, PNG và WebP. Hãy đổi ảnh HEIC/SVG sang JPG trước.",
    );
  if (file.size === 0 || file.size > 20 * 1024 * 1024)
    throw new Error("Ảnh gốc cần nhỏ hơn 20 MB.");
  const url = URL.createObjectURL(file);
  try {
    const image = new Image();
    await new Promise<void>((resolve, reject) => {
      image.onload = () => resolve();
      image.onerror = () =>
        reject(
          new Error("Ảnh không đọc được hoặc bị hỏng. Hãy chọn ảnh khác."),
        );
      image.src = url;
    });
    if (!image.naturalWidth || !image.naturalHeight)
      throw new Error("Ảnh không có kích thước hợp lệ.");
    const scale = Math.min(
      1,
      2400 / Math.max(image.naturalWidth, image.naturalHeight),
    );
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
    canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
    const context = canvas.getContext("2d");
    if (!context)
      throw new Error(
        "Trình duyệt chưa hỗ trợ xử lý ảnh. Hãy thử Chrome, Edge hoặc Safari mới hơn.",
      );
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, "image/webp", 0.88),
    );
    if (!blob || blob.size > 8 * 1024 * 1024)
      throw new Error("Ảnh sau tối ưu vẫn quá 8 MB. Hãy chọn ảnh nhỏ hơn.");
    const extension = blob.type === "image/webp" ? "webp" : "png";
    return new File([blob], `wedding-photo.${extension}`, { type: blob.type });
  } finally {
    URL.revokeObjectURL(url);
  }
}
