import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { invitationImageSrc, isInvitationImageUrl } from "../src/features/journey/invitations/invitationImages.ts";

const local = "/uploads/invitations/0123456789abcdef0123456789abcdef/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.webp";
describe("Invitation photographs", () => {
  it("preserves external photographs already saved in an invitation", () => {
    for (const url of ["https://images.example.com/wedding.jpg", "http://old.example.com/photo.png"]) {
      assert.ok(isInvitationImageUrl(url)); assert.equal(invitationImageSrc(url, "https://api.example.com"), url);
    }
  });
  it("recognizes persisted local cover and album paths", () => {
    assert.ok(isInvitationImageUrl(local)); assert.equal(invitationImageSrc(local, ""), local);
  });
  it("resolves uploads to a separate deployed API without changing stored database URLs", () => {
    assert.equal(invitationImageSrc(local, "https://api.example.com/"), `https://api.example.com${local}`);
    assert.equal(invitationImageSrc(undefined, "https://api.example.com"), "");
  });
  it("rejects executable, temporary, traversal and unsupported image references", () => {
    for (const url of ["javascript:alert(1)", "data:image/svg+xml,svg", "blob:https://example.com/temp", "/uploads/invitations/../../appsettings.json", local.replace(".webp", ".svg"), "//untrusted.example.com/photo.png", null, 42]) assert.equal(isInvitationImageUrl(url), false);
  });
});
