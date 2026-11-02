import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { VIETNAM_PROVINCES, normalizeProvince, searchProvinces } from "../src/data/vietnamProvinces.ts";

test("provincial selector exposes all 34 distinct current names including Huế", () => {
  assert.equal(VIETNAM_PROVINCES.length, 34);
  assert.equal(new Set(VIETNAM_PROVINCES).size, 34);
  for (const name of ["Hà Nội", "Hồ Chí Minh", "Huế", "Đồng Nai", "Bắc Ninh", "Quảng Ninh", "Tuyên Quang", "Cà Mau"])
    assert.ok(VIETNAM_PROVINCES.includes(name), name);
  for (const retired of ["Thừa Thiên Huế", "Bình Dương", "Bà Rịa - Vũng Tàu", "Hà Giang", "Bắc Giang"])
    assert.equal(VIETNAM_PROVINCES.includes(retired), false, retired);
  assert.deepEqual(searchProvinces(""), VIETNAM_PROVINCES);
});

test("province lookup supports Vietnamese marks, keyboard-friendly aliases and current administrative prefixes", () => {
  for (const [query, expected] of [[" TP. HCM ", "Hồ Chí Minh"], ["hn", "Hà Nội"], ["Tỉnh Đắk Lắk", "Đắk Lắk"], ["thanh pho Dong Nai", "Đồng Nai"], ["Hue", "Huế"], ["sai gon", "Hồ Chí Minh"]]) {
    assert.equal(normalizeProvince(query), expected);
    assert.ok(searchProvinces(query).includes(expected));
  }
  assert.deepEqual(searchProvinces("da nang"), ["Đà Nẵng"]);
  assert.deepEqual(searchProvinces("quang"), ["Quảng Ngãi", "Quảng Ninh", "Quảng Trị", "Tuyên Quang"]);
  assert.deepEqual(searchProvinces("khong co dia phuong nay"), []);
  assert.equal(normalizeProvince("Bình Dương"), null);
  for (const province of VIETNAM_PROVINCES) assert.equal(normalizeProvince(province), province);
});

test("frontend selector and backend registration validation use exactly the same province directory", async () => {
  const backend = await readFile(new URL("../../src/Wedding.Domain/Entities/VietnamProvinces.cs", import.meta.url), "utf8");
  const namesBlock = backend.match(/Array\.AsReadOnly\(new\[\]\s*\{([\s\S]*?)\}\)/)?.[1];
  assert.ok(namesBlock);
  assert.deepEqual([...namesBlock.matchAll(/"([^"]+)"/g)].map((match) => match[1]), VIETNAM_PROVINCES);
});
