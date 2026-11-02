import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { comboServicePreviewInput } from '../src/features/journey/combo/comboServiceDiscoveryModel.ts';
import { readComboFilters, comboFilterSearch } from '../src/features/journey/combo/comboFilterModel.ts';

const first = '00000000-0000-0000-0000-000000000001';
const second = '00000000-0000-0000-0000-000000000002';
const vendor = '00000000-0000-0000-0000-000000000003';
const catalog = () => ({ builder: { categories: [{ categoryId:first, options:[] }, { categoryId:second, options:[] }], templates:[] }, categoryCounts: [{categoryId:first,count:0}, {categoryId:second,count:8}] });

test('default package discovery loads real services even when there are no published combos', () => {
  const input = comboServicePreviewInput(catalog(), readComboFilters(''), null, 200);
  assert.equal(input.categoryId, second);
  assert.equal(input.page, 1);
  assert.equal(input.pageSize, 4);
  assert.deepEqual(input.selectedItemIds, []);
  assert.equal(input.vendorId, undefined);
  assert.equal(comboServicePreviewInput(catalog(), readComboFilters('?category=templates'), vendor, 150).vendorId, vendor);
});

test('preview is bounded, shares ordinary filters and never applies package-only discounts or component counts', () => {
  const filters = readComboFilters(`?search=studio&sort=savings&page=9&filterVendorId=${vendor}&minPrice=1000&maxPrice=9000&city=HCM&region=HCM&minRating=4&minServices=3&maxServices=8&minSavings=500&templateCategoryId=${first}`);
  const input = comboServicePreviewInput(catalog(), filters, vendor, 180);
  assert.equal(input.categoryId, first);
  assert.equal(input.search, 'studio');
  assert.equal(input.sort, 'recommended');
  assert.equal(input.filterVendorId, vendor);
  assert.equal(input.minPrice, 1000);
  assert.equal(input.maxPrice, 9000);
  assert.equal(input.city, 'HCM');
  assert.equal(input.region, 'HCM');
  assert.equal(input.minRating, 4);
  assert.equal(input.guests, 180);
  for (const field of ['minServices','maxServices','minSavings','templateCategoryId','templateId','listingId']) assert.equal(input[field], undefined);
  assert.equal(filters.page, 9);
  assert.equal(filters.minSavings, 500);
});

test('category and Listing deep links keep their existing main service page without a second preview request', () => {
  assert.equal(comboServicePreviewInput(catalog(), readComboFilters(`?category=${second}`), vendor, 200), null);
  assert.equal(comboServicePreviewInput(catalog(), readComboFilters(`?listing=${first}`), vendor, 200), null);
  assert.equal(comboServicePreviewInput(undefined, readComboFilters(''), vendor, 200), null);
  assert.equal(comboServicePreviewInput({...catalog(),categoryCounts:[]}, readComboFilters(''), vendor, 200), null);
});

test('service navigation keeps search/vendor/event-independent filters without package-only restrictions or cart writes', () => {
  const prior = readComboFilters(`?search=studio&minRating=4&filterVendorId=${vendor}&minServices=3&minSavings=999&templateCategoryId=${first}`);
  const next = readComboFilters(comboFilterSearch({...prior,category:second,page:1,minServices:undefined,maxServices:undefined,minSavings:undefined,templateCategoryId:undefined}));
  assert.equal(next.category, second);
  assert.equal(next.search, 'studio');
  assert.equal(next.filterVendorId, vendor);
  assert.equal(next.minRating, 4);
  assert.equal(next.minServices, undefined);
  assert.equal(next.minSavings, undefined);
  const source = readFileSync(new URL('../src/features/journey/combo/ComboBuilderPage.tsx', import.meta.url), 'utf8');
  assert.match(source, /aria-label="Tự chọn dịch vụ lẻ"/);
  assert.match(source, /<ComboServiceCards/);
  assert.match(source, /comboServicePreviewInput\(catalog\.data, filters/);
  assert.match(source, /previewCategory\?\.options\.map/);
  assert.match(source, /<ComboBuilder key=\{userId\}/);
});
