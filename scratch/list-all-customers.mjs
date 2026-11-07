import fs from 'fs';

let content = fs.readFileSync('scratch/all-users-detail-utf8.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
const users = JSON.parse(content);

const customers = users.filter(u => u.Role === 'Customer');
console.log(`Total customers: ${customers.length}`);

customers.forEach((c, i) => {
  console.log(`${i + 1}. [${c.Id}] "${c.FullName}" <${c.Email}> | Active: ${c.IsActive} | Phone: ${c.PhoneNumber}`);
});

