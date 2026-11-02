export type SeoMetadata = {
  indexable: boolean;
  title?: string;
  description?: string;
  canonicalPath?: string;
};

export function getSeoMetadata(pathname: string): SeoMetadata;
export function getCanonicalUrl(pathname: string): string | null;
