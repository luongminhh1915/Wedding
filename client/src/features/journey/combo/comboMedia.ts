/** Resolve public catalog images without allowing executable, inline or protocol-relative sources. */
export function comboMediaSources(
  sources: (string | null | undefined)[],
  apiBase = import.meta.env?.VITE_API_URL || "",
): string[] {
  return [
    ...new Set(
      sources.flatMap((source) => {
        const value = source?.trim();
        if (!value || /[\\\r\n]/.test(value)) return [];
        if (/^https?:\/\//i.test(value)) return [value];
        if (value.startsWith("/uploads/") && !value.includes(".."))
          return [`${apiBase.replace(/\/$/, "")}${value}`];
        return [];
      }),
    ),
  ];
}
