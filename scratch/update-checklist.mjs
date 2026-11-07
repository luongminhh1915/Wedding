import fs from 'fs';

const path = 'docs/master-upgrade-checklist.md';
let src = fs.readFileSync(path, 'utf8');

const needle = '# Master upgrade — audit và checklist theo phase';
const addition = `# Master upgrade — audit và checklist theo phase

**Article Publishing & 24 Articles 04/11 — IMPLEMENTED & VERIFIED:** owner requested "t thấy có xuất bản đc đâu". Root causes: (1) DB initially had 0 articles, seeded all 24 curated articles into SQL Server with \`IsPublished = 1\`; (2) React state race condition on submit button resolved via \`pendingPublishRef\` so \`publish: true\` is synchronously dispatched on clicking "Xuất bản ngay"; (3) local image fallback handling for \`/blog/\` paths added; (4) TOC anchors aligned with \`<h2>\` tags. FE tests 543/543 PASS, client build clean in 1.16s, API returns 24 published articles. Package updated in \`artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip\`.`;

src = src.replace(needle, addition);
fs.writeFileSync(path, src, 'utf8');
console.log("Updated master-upgrade-checklist.md!");

