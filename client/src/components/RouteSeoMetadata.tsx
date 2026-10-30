import { useEffect } from "react";
import { useLocation } from "react-router-dom";
import { getCanonicalUrl, getSeoMetadata } from "../utils/seoPolicy.mjs";

function setMeta(attribute: "name" | "property", key: string, content: string) {
  let element = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`);
  if (!element) {
    element = document.createElement("meta");
    element.setAttribute(attribute, key);
    document.head.append(element);
  }
  element.content = content;
}

export default function RouteSeoMetadata() {
  const { pathname } = useLocation();

  useEffect(() => {
    const metadata = getSeoMetadata(pathname);
    const robots = metadata.indexable ? "index, follow, max-image-preview:large" : "noindex, nofollow";
    setMeta("name", "robots", robots);

    if (!metadata.indexable || !metadata.title || !metadata.description) {
      document.head.querySelector('link[rel="canonical"]')?.remove();
      return;
    }

    document.title = metadata.title;
    setMeta("name", "description", metadata.description);
    setMeta("property", "og:type", "website");
    setMeta("property", "og:site_name", "Wedding Match");
    setMeta("property", "og:title", metadata.title);
    setMeta("property", "og:description", metadata.description);

    let canonical = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!canonical) {
      canonical = document.createElement("link");
      canonical.rel = "canonical";
      document.head.append(canonical);
    }
    canonical.href = getCanonicalUrl(pathname) ?? "";
  }, [pathname]);

  return null;
}
