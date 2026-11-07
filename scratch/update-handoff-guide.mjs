import fs from 'fs';

const path = 'docs/AGENT_HANDOFF.md';
let src = fs.readFileSync(path, 'utf8');

const target = '- Packaged into: [artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip](file:///d:/Gitgithubhub/Wedding/artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip).';
const replacement = `- Packaged into: [artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip](file:///d:/Gitgithubhub/Wedding/artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip).
    - Hướng dẫn Deploy cho Codex: xem [docs/CODEX_DEPLOYMENT_GUIDE.md](file:///d:/Gitgithubhub/Wedding/docs/CODEX_DEPLOYMENT_GUIDE.md).`;

src = src.replace(target, replacement);
fs.writeFileSync(path, src, 'utf8');
console.log("Updated AGENT_HANDOFF.md with link to CODEX_DEPLOYMENT_GUIDE.md");

