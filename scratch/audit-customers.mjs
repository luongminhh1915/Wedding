import fs from 'fs';

let content = fs.readFileSync('scratch/all-users-detail-utf8.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
const users = JSON.parse(content);

const customers = users.filter(u => u.Role === 'Customer');
console.log(`Total customers: ${customers.length}`);

const lyCustomers = customers.filter(c => c.FullName.startsWith('Lý ') || c.FullName.includes(' Lý ') || c.FullName.endsWith(' Lý'));
console.log(`Customers with name 'Lý': ${lyCustomers.length}`);
lyCustomers.forEach(c => console.log(`  - ${c.FullName} (${c.Email})`));

const nonGmailFpt = customers.filter(c => {
  const email = (c.Email || '').toLowerCase();
  return !email.endsWith('@gmail.com') && !email.endsWith('@fpt.edu.vn');
});
console.log(`\nCustomers with non-gmail/fpt domain: ${nonGmailFpt.length}`);
const domainCounts = {};
nonGmailFpt.forEach(c => {
  const domain = c.Email.split('@')[1] || 'no-domain';
  domainCounts[domain] = (domainCounts[domain] || 0) + 1;
});
console.log('Domain breakdown:', domainCounts);

