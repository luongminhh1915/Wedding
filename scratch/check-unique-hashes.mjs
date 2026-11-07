import fs from 'fs';

let content = fs.readFileSync('scratch/all-users-detail.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
const users = JSON.parse(content);

const hashMap = new Map();
users.forEach(u => {
  if (!hashMap.has(u.PasswordHash)) {
    hashMap.set(u.PasswordHash, []);
  }
  hashMap.get(u.PasswordHash).push(u);
});

console.log(`Total unique password hashes: ${hashMap.size}`);
for (const [hash, userList] of hashMap.entries()) {
  console.log(`Hash: ${hash.slice(0, 20)}... | Count: ${userList.length} | Sample: ${userList[0].Role} - ${userList[0].Email || userList[0].LoginName}`);
}

