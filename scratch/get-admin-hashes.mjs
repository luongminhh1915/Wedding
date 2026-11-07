import fs from 'fs';

let content = fs.readFileSync('scratch/all-users-detail.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
const users = JSON.parse(content);

const admins = users.filter(u => u.Role === 'SuperAdmin');
console.log(admins.map(a => ({ name: a.FullName, email: a.Email, hash: a.PasswordHash })));

