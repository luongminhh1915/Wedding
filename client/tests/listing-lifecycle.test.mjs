import { test } from "node:test";
import assert from "node:assert/strict";
import { listingCanEdit, listingCanSubmit, listingPreviewUrl } from "../src/features/vendor-portal/listingLifecycle.ts";

test("unpublished drafts and rejections retain the existing edit/submit workflow", () => {
  for (const status of ['Draft', 'Rejected']) {
    assert.equal(listingCanEdit({ status }), true);
    assert.equal(listingCanSubmit({ status }), true);
  }
  for (const status of ['PendingApproval', 'Unknown', 'Approved']) {
    assert.equal(listingCanEdit({ status }), false);
    assert.equal(listingCanSubmit({ status }), false);
  }
});
test("published/hidden listings edit only through revisions, and submit only an explicit draft/rejection", () => {
  for (const status of ['Active', 'Hidden']) {
    assert.equal(listingCanEdit({ status }), true);
    for (const revisionStatus of ['Draft', 'Rejected']) assert.equal(listingCanSubmit({ status, revisionStatus }), true);
    for (const revisionStatus of [undefined, 'Pending', 'Approved', 'Superseded', 'Baseline']) assert.equal(listingCanSubmit({ status, revisionStatus }), false);
  }
});
test("preview selects only an outstanding private revision; approved history never replaces public detail by default", () => {
  for (const revisionStatus of ['Draft', 'Pending', 'Rejected'])
    assert.equal(listingPreviewUrl({ id: 'service-a', revisionStatus, latestRevisionId: 'revision-a' }), '/listings/service-a?preview=vendor&revision=revision-a');
  for (const revisionStatus of [undefined, 'Approved', 'Superseded', 'Baseline'])
    assert.equal(listingPreviewUrl({ id: 'service-a', revisionStatus, latestRevisionId: 'revision-a' }), '/listings/service-a?preview=vendor');
  assert.equal(listingPreviewUrl({ id: 'service-a', revisionStatus: 'Draft', latestRevisionId: null }), '/listings/service-a?preview=vendor');
});
