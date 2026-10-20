import { test } from "node:test";
import assert from "node:assert/strict";
import { comboMediaSources } from "../src/features/journey/combo/comboMedia.ts";

test("combo photos use actual public catalog sources, skip empty sources and deduplicate", () => {
  assert.deepEqual(
    comboMediaSources(
      [
        null,
        "",
        "https://shop.example/cover.jpg",
        "https://shop.example/cover.jpg",
      ],
      "",
    ),
    ["https://shop.example/cover.jpg"],
  );
});
test("combo upload pictures resolve to the deployed API without rewriting catalog data", () => {
  assert.deepEqual(
    comboMediaSources(["/uploads/listings/photo.jpg"], "https://api.example/"),
    ["https://api.example/uploads/listings/photo.jpg"],
  );
});
test("combo photo sources exclude inline, executable, traversal and protocol-relative URLs", () => {
  assert.deepEqual(
    comboMediaSources(
      [
        "javascript:alert(1)",
        "data:image/svg+xml,test",
        "//other.example/image.jpg",
        "/uploads/../private",
        "https:\\bad.example\\photo.jpg",
      ],
      "",
    ),
    [],
  );
});
