import fs from 'fs';

const path = 'client/src/features/journey/admin/OperationsPanels.tsx';
let src = fs.readFileSync(path, 'utf8');

src = src.replace('import { useState, useEffect } from "react";', 'import { useState, useEffect, useMemo } from "react";');

const targetBody = `          {/* If content is rich HTML with tags, render HTML; else fallback to pre-wrap */}
          {article.content && (article.content.includes("<p>") || article.content.includes("<h2>") || article.content.includes("<table")) ? (
            <div
              className={\`editorial-content \${fontSizeClass}\`}
              dangerouslySetInnerHTML={{ __html: article.content }}
            />
          ) : (`;

const newBody = `          {/* If content is rich HTML with tags, render HTML; else fallback to pre-wrap */}
          {article.content && (article.content.includes("<p>") || article.content.includes("<h2>") || article.content.includes("<table")) ? (
            <div
              className={\`editorial-content \${fontSizeClass}\`}
              dangerouslySetInnerHTML={{ __html: renderedContent }}
            />
          ) : (`;

src = src.replace(targetBody, newBody);

const targetBeforeRender = `  const fontSizeClass = fontSize === "sm" ? "text-[0.9375rem]" : fontSize === "lg" ? "text-[1.1875rem]" : "text-[1.0625rem]";`;
const newBeforeRender = `  const fontSizeClass = fontSize === "sm" ? "text-[0.9375rem]" : fontSize === "lg" ? "text-[1.1875rem]" : "text-[1.0625rem]";

  const renderedContent = useMemo(() => {
    if (!article?.content) return "";
    let count = 0;
    return article.content.replace(/<h2>/g, () => {
      count++;
      return \`<h2 id="section-\${count}">\`;
    });
  }, [article?.content]);`;

src = src.replace(targetBeforeRender, newBeforeRender);

fs.writeFileSync(path, src, 'utf8');
console.log("Updated OperationsPanels.tsx!");

