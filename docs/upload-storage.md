# Durable uploaded images

Listing uploads and invitation images are files, not SQL Server blobs. Back up both the SQL Server database and the upload folders; a database backup alone cannot restore photographs.

Set these environment variables (or the equivalent private ASP.NET Core configuration) on the server:

```text
Uploads__ListingRoot=../private/uploads/listings
Uploads__InvitationRoot=../private/uploads/invitations
```

Paths may be absolute, or relative to the API's content root. Confirm the actual hosting directory layout and write permissions before using these examples. Keep both folders outside the deployment target and outside the public web root. Do not include them in a release ZIP, erase them when publishing, or store them on temporary disks. Copy the folders, preserving their filenames and invitation owner subdirectories, when moving to another server.

Without an override, local listing files go to `App_Data/listing-uploads` and invitation files to `App_Data/invitation-uploads`. New listing files no longer go into `wwwroot/uploads/listings`. Previously generated listing filenames can still be read from that old location; this fallback never writes or deletes old files. Before a clean deployment, copy existing listing files into the configured durable listing folder and verify their URLs, rather than relying on the old publish folder.

The public URLs stay unchanged:

- Listings: `/uploads/listings/{32-character-guid}.{jpg|jpeg|png|webp}`.
- Invitations: `/uploads/invitations/{owner-guid}/{32-character-guid}.{jpg|png|webp}`.

Uploaded photographs are publicly accessible to anyone with their URL. Do not upload identity documents, financial records, or other private documents through these endpoints. Neither endpoint accepts user-selected filesystem paths, SVG, GIF, HTML, or arbitrary files. Listing images use the same raster validation and metadata stripping as invitation images, with a limit of 8 MB per image, 8,192 pixels per edge, and 25 million pixels. Listings have a shared safety quota of 5,000 files / 1 GB; invitation quotas remain per wedding owner. Listing upload batches support up to 10 images, and failed batches roll back only the new files created during that request. This does not add a public image deletion endpoint or change account authorization.

The API request limit for a listing batch is 81 MB. A reverse proxy or IIS can impose a smaller limit; configure its request limit intentionally or upload smaller batches. A free shared host with 256 MB of RAM should use small, compressed photographs and small batches.

The hosting health and persistence check should include uploading a disposable image, restarting the API, deploying a new release without replacing private folders, and confirming that the same URL still serves the same image. A SQL backup plus an upload-folder archive should be downloaded after each day of collecting data and before every release; keep at least one copy outside the hosting account.
