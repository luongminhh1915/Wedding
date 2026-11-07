import fs from 'fs';

let content = fs.readFileSync('scratch/all-prod-users.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) {
  content = content.slice(1);
}
const users = JSON.parse(content);

console.log("=== SUPERADMINS ===");
const admins = users.filter(u => u.Role === 'SuperAdmin');
admins.forEach(a => console.log(`- ${a.FullName} | Email: ${a.Email} | Created: ${a.CreatedAt}`));

console.log("\n=== VENDORS (Sample 10) ===");
const vendors = users.filter(u => u.Role === 'VendorOwner');
vendors.slice(0, 10).forEach(v => console.log(`- ${v.BrandName || v.FullName} | LoginName: ${v.LoginName} | Email: ${v.Email}`));

console.log("\n=== CUSTOMERS (Original 19 before seed) ===");
const customers = users.filter(u => u.Role === 'Customer');
const originalCustomers = customers.filter(c => new Date(c.CreatedAt).getTime() < new Date('2026-08-15').getTime());
console.log(`Original customers count: ${originalCustomers.length}`);
originalCustomers.forEach(c => console.log(`- ${c.FullName} | Email: ${c.Email} | Active: ${c.IsActive}`));

console.log(`Seeded customers count: ${customers.length - originalCustomers.length}`);
