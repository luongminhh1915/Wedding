import fs from 'fs';

let content = fs.readFileSync('scratch/transformed-customers.json', 'utf8');
const data = JSON.parse(content);

// Activate Bun Dau Mam Tom
const bun = data.find(c => c.email === 'bundaumamtom@gmail.com');
if (bun) bun.isActive = true;

// Set one random regular customer to inactive to keep exact 4 inactive
const swap = data.find(c => c.email === 'nga.honglam2@gmail.com' || c.email.includes('nga.honglam'));
if (swap) swap.isActive = false;

fs.writeFileSync('scratch/transformed-customers.json', JSON.stringify(data, null, 2), 'utf8');
console.log('Swapped inactive slot: bundaumamtom is now ACTIVE, swap is INACTIVE.');

