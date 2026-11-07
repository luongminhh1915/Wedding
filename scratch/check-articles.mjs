import fs from 'fs';
const data = JSON.parse(fs.readFileSync('client/src/features/journey/admin/curatedArticles24.json', 'utf8'));
console.log('Total articles:', data.length);
const regex = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
for (const a of data) {
  if (!regex.test(a.slug)) console.log('INVALID SLUG:', a.num, a.slug);
  if (!a.title || a.title.length > 300) console.log('INVALID TITLE:', a.num);
  if ((a.excerpt?.length || 0) > 1000) console.log('EXCERPT TOO LONG:', a.num, a.excerpt.length);
  if (!a.content) console.log('NO CONTENT:', a.num);
  if (!a.category || a.category.length > 100) console.log('INVALID CAT:', a.num);
}
console.log('Validation complete!');

