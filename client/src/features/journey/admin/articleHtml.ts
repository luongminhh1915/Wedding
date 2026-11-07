import createDOMPurify from "dompurify";

const styleProperties = new Set([
  "color", "background-color", "text-align", "font-weight", "font-style", "font-family", "font-size",
  "text-decoration", "line-height", "vertical-align", "border-collapse", "border", "border-color",
  "border-width", "border-style", "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
  "margin", "margin-top", "margin-right", "margin-bottom", "margin-left", "width", "max-width", "height", "list-style-type",
]);
const articleClasses = new Set(["editorial-lead", "editorial-callout", "callout-title", "editorial-table-wrapper", "editorial-table", "editorial-figure", "text-rose-600", "underline", "font-semibold"]);

/** Article HTML is authored/pasted content, even when the author is trusted staff. */
export function sanitizeArticleHtml(html: string): string {
  const purifier = createDOMPurify(window);
  purifier.addHook("afterSanitizeAttributes", node => {
    if (!(node instanceof HTMLElement)) return;
    for (const property of Array.from(node.style)) {
      const value = node.style.getPropertyValue(property);
      if (!styleProperties.has(property) || !/^[a-z\d\s.,()%\u0027\u0022#-]+$/i.test(value)
        || /(?:url|expression|var)\s*\(/i.test(value)) node.style.removeProperty(property);
    }
    if (!node.style.cssText) node.removeAttribute("style");
    const classes = [...node.classList].filter(name => articleClasses.has(name));
    if (classes.length) node.className = classes.join(" "); else node.removeAttribute("class");
    if (node.id && !/^section-\d+$/.test(node.id)) node.removeAttribute("id");
    for (const attribute of ["href", "src"]) {
      const value = node.getAttribute(attribute);
      if (value === null) continue;
      const relative = /^\/(?!\/)/.test(value) && !/[\\\u0000-\u0020]/.test(value);
      const https = /^https:\/\//i.test(value) && !/[\\\u0000-\u0020]/.test(value);
      const link = attribute === "href" && /^(?:#[a-z\d_-]+|mailto:[^\s<>]+|tel:[+\d -]+)$/i.test(value);
      if (!relative && !https && !link) node.removeAttribute(attribute);
    }
    if (node.tagName === "A" && node.getAttribute("target") === "_blank") node.setAttribute("rel", "noopener noreferrer");
  });
  return purifier.sanitize(html, {
    ALLOWED_TAGS: ["p", "h1", "h2", "h3", "h4", "h5", "h6", "b", "strong", "i", "em", "u", "s", "strike", "span", "br", "div", "ul", "ol", "li", "a", "img", "figure", "figcaption", "blockquote", "table", "thead", "tbody", "tfoot", "tr", "th", "td", "hr", "sub", "sup"],
    ALLOWED_ATTR: ["href", "src", "alt", "title", "class", "style", "colspan", "rowspan", "target", "rel", "id"],
    ALLOW_DATA_ATTR: false,
    ALLOW_ARIA_ATTR: false,
  });
}
