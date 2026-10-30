# Durable uploaded images and invitation music

Listing uploads, article cover images, invitation images and uploaded MP3 music are files, not SQL Server blobs. Back up both the SQL Server database and the upload folders; a database backup alone cannot restore photographs or music.

Set these environment variables (or the equivalent private ASP.NET Core configuration) on the server:

```text
Uploads__ListingRoot=../private/uploads/listings
Uploads__ArticleRoot=../private/uploads/articles
Uploads__InvitationRoot=../private/uploads/invitations
Uploads__InvitationAudioRoot=../private/uploads/invitation-audio
```

Paths may be absolute, or relative to the API's content root. Confirm the actual hosting directory layout and write permissions before using these examples. Keep all four folders outside the deployment target and outside the public web root. Do not include them in a release ZIP, erase them when publishing, or store them on temporary disks. Copy the folders, preserving their filenames and invitation owner subdirectories, when moving to another server.

Without an override, local listing files go to `App_Data/listing-uploads` and invitation files to `App_Data/invitation-uploads`. New listing files no longer go into `wwwroot/uploads/listings`. Previously generated listing filenames can still be read from that old location; this fallback never writes or deletes old files. Before a clean deployment, copy existing listing files into the configured durable listing folder and verify their URLs, rather than relying on the old publish folder.

The public URLs stay unchanged:

- Listings: `/uploads/listings/{32-character-guid}.{jpg|jpeg|png|webp}`.
- Article covers: `/uploads/articles/{32-character-guid}.{jpg|png|webp}`.
- Invitations: `/uploads/invitations/{owner-guid}/{32-character-guid}.{jpg|png|webp}`.
- Invitation music: `/uploads/invitation-audio/{owner-guid}/{32-character-guid}.mp3`.

Article covers (29/10/2026) are uploaded through `POST /api/admin/articles/images` with multipart field `file`, restricted to active SuperAdmin/Moderator accounts rechecked in SQL. `Uploads:ArticleRoot` is optional: if absent and ListingRoot is configured, the API uses an `articles` sibling of that resolved listing directory. Existing hosting configuration therefore persists covers in private/uploads/articles without a configuration rewrite. With neither root configured, the local fallback is App_Data/article-uploads. Limits are 8 MB per JPEG/PNG/WebP, 8,192 pixels per edge, 25 million pixels and a shared 5,000-file/1 GB technical quota. Structural validation/metadata removal reuses the listing raster validator; it is not antivirus or full image decoding. The quota lock is process-local. Public reads enforce generated filenames, nosniff and a sandbox CSP. Draft cover URLs are still public if known; do not use them for confidential material. Replacing/removing a cover changes only its SQL reference; unreferenced files remain until an explicit cleanup workflow. Back up this directory alongside the other upload roots.

MP3 uploads use `Uploads:InvitationAudioRoot`, defaulting locally to `App_Data/invitation-audio`. The technical safety caps are 12 MB/file, 20 files and 100 MB per wedding owner; these are not a billing policy. The server checks extension, MIME, bounded size and consecutive MPEG Layer III frame structure, and removes ID3 tags. This is structural validation, not a full decoder or antivirus scan; clients may still reject an unsupported/damaged track. Only an active Customer with write access to the wedding may upload. Saved invitation paths must belong to that wedding owner, including when a shared Editor uploads. Audio is public to anyone with the generated URL, served with `audio/mpeg`, `nosniff` and range support. Never use this endpoint for private recordings or documents. Replacing/removing music changes the invitation reference only; it does not delete files referenced by an older saved invitation. Orphan cleanup requires an explicit administrative workflow; repeated replacement can reach the technical quota.

YouTube references remain canonical video URLs in the existing `MusicUrl` SQL column; no audio is downloaded or ripped, and no new schema is required. The browser derives the source/video ID, loads the embedded player only after a click, and never autoplays. Uploaded paths remain relative for host portability; the frontend resolves them against `VITE_API_URL` in separate-origin deployments. YouTube availability, copyright/embed restrictions and mobile browser volume behavior are external conditions; the fallback link leaves the rest of the invitation/RSVP usable. The implementation follows the [official embedded-player parameters](https://developers.google.com/youtube/player_parameters).

Uploaded photographs are publicly accessible to anyone with their URL. Do not upload identity documents, financial records, or other private documents through these endpoints. Neither endpoint accepts user-selected filesystem paths, SVG, GIF, HTML, or arbitrary files. Listing images use the same raster validation and metadata stripping as invitation images, with a limit of 8 MB per image, 8,192 pixels per edge, and 25 million pixels. Listings have a shared safety quota of 5,000 files / 1 GB; invitation quotas remain per wedding owner. Listing upload batches support up to 10 images, and failed batches roll back only the new files created during that request. This does not add a public image deletion endpoint or change account authorization.

The API request limit for a listing batch is 81 MB. A reverse proxy or IIS can impose a smaller limit; configure its request limit intentionally or upload smaller batches. A free shared host with 256 MB of RAM should use small, compressed photographs and small batches.

The hosting health and persistence check should include uploading a disposable image, restarting the API, deploying a new release without replacing private folders, and confirming that the same URL still serves the same image. A SQL backup plus an upload-folder archive should be downloaded after each day of collecting data and before every release; keep at least one copy outside the hosting account.
