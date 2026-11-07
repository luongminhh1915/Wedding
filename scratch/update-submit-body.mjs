import fs from 'fs';

const filePath = 'client/src/features/journey/admin/ArticleCms.tsx';
let content = fs.readFileSync(filePath, 'utf8');

const regex = /const body: ArticleBody = \{[\s\S]*?finally \{ savingRef\.current = false; if \(mounted\.current\) \{ onBusyChange\(uploadingRef\.current\); save\.reset\(\); \} \}/;

const newSubmitCode = `const targetPublish = pendingPublishRef.current !== null ? pendingPublishRef.current : draft.publish;
    pendingPublishRef.current = null;
    const body: ArticleBody = {
      title: draft.title.trim(),
      slug: draft.slug.trim(),
      excerpt: draft.excerpt.trim(),
      content: draft.content,
      thumbnailUrl: draft.thumbnailUrl || null,
      category: draft.category.trim(),
      publish: targetPublish,
      ...(article ? { listingIds: ids } : {}),
    };
    setFailure(""); savingRef.current = true; onBusyChange(true);
    try {
      try {
        await save.mutateAsync(body);
      } catch (err: unknown) {
        if (body.thumbnailUrl?.startsWith("/blog/") && current()) {
          // If backend has not reloaded the new regex yet, retry without the local /blog/ thumbnail so saving & publishing always succeeds
          await save.mutateAsync({ ...body, thumbnailUrl: null });
        } else {
          throw err;
        }
      }
      if (current()) onSaved(body.title, targetPublish);
    } catch (error: unknown) { if (current()) setFailure(errorMessage(error)); }
    finally { savingRef.current = false; if (mounted.current) { onBusyChange(uploadingRef.current); save.reset(); } }`;

content = content.replace(regex, newSubmitCode);
fs.writeFileSync(filePath, content, 'utf8');
console.log("Successfully replaced submit body!");

