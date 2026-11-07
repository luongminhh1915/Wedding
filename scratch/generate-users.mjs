import fs from 'fs';

const firstNamesMale = [
  'Tuấn', 'Nam', 'Dũng', 'Khánh', 'Hoàng', 'Long', 'Quân', 'Huy', 'Cường', 'Đạt',
  'Khoa', 'Lâm', 'Phúc', 'Thịnh', 'Bách', 'Hiếu', 'Phong', 'Kiên', 'Tùng', 'Việt',
  'Sơn', 'Duy', 'Trọng', 'Nghĩa', 'Minh', 'Hải', 'Thành', 'Trung', 'Bình', 'Hưng',
  'Quang', 'Bảo', 'Tài', 'Vinh', 'Đức', 'Toàn', 'Tiến', 'Chiến', 'An', 'Bằng'
];

const firstNamesFemale = [
  'Vy', 'Linh', 'Trang', 'Hằng', 'Mai', 'Lan', 'Nga', 'Phương', 'Thảo', 'Ngọc',
  'Hà', 'My', 'Hương', 'Quỳnh', 'Dung', 'Chi', 'Anh', 'Tuyết', 'Yến', 'Trâm',
  'Oanh', 'Thư', 'Thủy', 'Huyền', 'Vân', 'Ly', 'Châu', 'Diệp', 'Tâm', 'Giang',
  'Ngân', 'Loan', 'Bích', 'Trúc', 'Thương', 'Trang', 'Nhàn', 'Quyên', 'Thoa', 'Ánh'
];

const lastNames = [
  'Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Huỳnh', 'Phan', 'Vũ', 'Võ', 'Đặng',
  'Bùi', 'Đỗ', 'Hồ', 'Ngô', 'Dương', 'Lý', 'Đinh', 'Đoàn', 'Lâm', 'Trịnh'
];

const middleNamesMale = ['Văn', 'Hữu', 'Minh', 'Đức', 'Quốc', 'Đình', 'Thanh', 'Gia', 'Hải', 'Tuấn', 'Công', 'Xuân'];
const middleNamesFemale = ['Thị', 'Ngọc', 'Phương', 'Kim', 'Thu', 'Thảo', 'Hồng', 'Diệu', 'Thanh', 'Bảo', 'Mai', 'Khánh'];

const telcoPrefixes = [
  '098', '097', '096', '086', '038', '039', '035', '036', '037', // Viettel
  '091', '094', '088', '083', '085', '084', '081',             // Vina
  '090', '093', '089', '079', '077', '078', '076'              // Mobi
];

const domains = [
  '@gmail.com', '@gmail.com', '@gmail.com', '@gmail.com', '@gmail.com', '@gmail.com',
  '@outlook.com', '@yahoo.com', '@icloud.com'
];

function removeVietnameseTones(str) {
  str = str.replace(/à|á|ạ|ả|ã|â|ầ|ấ|ậ|ẩ|ẫ|ă|ằ|ắ|ặ|ẳ|ẵ/g, "a");
  str = str.replace(/è|é|ẹ|ẻ|ẽ|ê|ề|ế|ệ|ể|ễ/g, "e");
  str = str.replace(/ì|í|ị|ỉ|ĩ/g, "i");
  str = str.replace(/ò|ó|ọ|ỏ|õ|ô|ồ|ố|ộ|ổ|ỗ|ơ|ờ|ớ|ợ|ở|ỡ/g, "o");
  str = str.replace(/ù|ú|ụ|ủ|ũ|ư|ừ|ứ|ự|ử|ữ/g, "u");
  str = str.replace(/ỳ|ý|ỵ|ỷ|ỹ/g, "y");
  str = str.replace(/đ/g, "d");
  str = str.replace(/À|Á|Ạ|Ả|Ã|Â|Ầ|Ấ|Ậ|Ẩ|Ẫ|Ă|Ằ|Ắ|Ặ|Ẳ|Ẵ/g, "A");
  str = str.replace(/È|É|Ẹ|Ẻ|Ẽ|Ê|Ề|Ế|Ệ|Ể|Ễ/g, "E");
  str = str.replace(/Ì|Í|Ị|Ỉ|Ĩ/g, "I");
  str = str.replace(/Ò|Ó|Ọ|Ỏ|Õ|Ô|Ồ|Ố|Ộ|Ổ|Ỗ|Ơ|Ờ|Ớ|Ợ|Ở|Ỡ/g, "O");
  str = str.replace(/Ù|Ú|Ụ|Ủ|Ũ|Ư|Ừ|Ứ|Ự|Ử|Ữ/g, "U");
  str = str.replace(/Ỳ|Ý|Ỵ|Ỷ|Ỹ/g, "Y");
  str = str.replace(/Đ/g, "D");
  return str.toLowerCase();
}

const users = [];
const usedEmails = new Set();
const usedPhones = new Set();

const defaultPasswordHash = "$2a$11$soA1kT9hP0O2BMzRpO1zme4h6lHgaoSgYIUGJie.O359oFVCTosJq"; // "123456"

// We need 176 users total: 173 active, 3 inactive
const TARGET_TOTAL = 176;

// Date range: 2026-08-15 to 2026-11-05
const startDate = new Date('2026-08-15T08:00:00Z').getTime();
const endDate = new Date('2026-11-05T12:00:00Z').getTime();

for (let i = 0; i < TARGET_TOTAL; i++) {
  const isFemale = i % 2 === 0;
  const lastName = lastNames[i % lastNames.length];
  const middleName = isFemale ? middleNamesFemale[(i * 3) % middleNamesFemale.length] : middleNamesMale[(i * 3) % middleNamesMale.length];
  const firstName = isFemale ? firstNamesFemale[(i * 7) % firstNamesFemale.length] : firstNamesMale[(i * 7) % firstNamesMale.length];

  const fullName = `${lastName} ${middleName} ${firstName}`;
  const asciiFirst = removeVietnameseTones(firstName);
  const asciiLast = removeVietnameseTones(lastName);
  const asciiMiddle = removeVietnameseTones(middleName);

  // Birth year between 1993 and 2002 (24 - 33 years old, prime wedding planning age)
  const birthYear = 1993 + (i % 10);
  const shortYear = String(birthYear).slice(-2);

  let emailBase;
  const patternType = i % 4;
  if (patternType === 0) {
    emailBase = `${asciiFirst}.${asciiLast}${shortYear}`;
  } else if (patternType === 1) {
    emailBase = `${asciiLast}${asciiMiddle}${asciiFirst}${shortYear}`;
  } else if (patternType === 2) {
    emailBase = `${asciiFirst}.${asciiMiddle}${asciiLast}`;
  } else {
    emailBase = `${asciiLast}.${asciiFirst}${birthYear}`;
  }

  const domain = domains[i % domains.length];
  let email = `${emailBase}${domain}`;
  let suffix = 1;
  while (usedEmails.has(email)) {
    email = `${emailBase}${suffix}${domain}`;
    suffix++;
  }
  usedEmails.add(email);

  // Phone number
  const prefix = telcoPrefixes[i % telcoPrefixes.length];
  let phone;
  do {
    const random7 = Math.floor(1000000 + Math.random() * 9000000);
    phone = `${prefix}${random7}`;
  } while (usedPhones.has(phone));
  usedPhones.add(phone);

  // Creation timestamp: staggered organically
  const progressRatio = i / TARGET_TOTAL;
  // Weighted so more users signed up recently (Sept/Oct/Nov)
  const timeOffset = Math.pow(progressRatio, 1.3) * (endDate - startDate);
  const jitter = (Math.random() - 0.5) * 86400000 * 2; // +/- 2 days jitter
  const createdTime = Math.min(endDate - 3600000, Math.max(startDate, startDate + timeOffset + jitter));
  const createdAt = new Date(createdTime).toISOString();

  // Active status: 3 inactive accounts (index 35, 88, 140)
  const isActive = !(i === 35 || i === 88 || i === 140);

  // LastLoginAt
  let lastLoginAt = null;
  if (isActive) {
    // Some logged in very recently (Nov 4-5), some a few days ago, some earlier
    const loginRecencyRatio = Math.random();
    let loginTime;
    if (loginRecencyRatio < 0.35) {
      // 35% logged in within the last 48 hours (active today/yesterday)
      loginTime = endDate - Math.random() * 48 * 3600000;
    } else if (loginRecencyRatio < 0.70) {
      // 35% logged in within the last 7 days
      loginTime = endDate - (2 + Math.random() * 5) * 86400000;
    } else {
      // 30% logged in within 2-4 weeks after signup
      loginTime = Math.min(endDate - 86400000, createdTime + Math.random() * 14 * 86400000);
    }
    lastLoginAt = new Date(Math.max(createdTime, loginTime)).toISOString();
  }

  users.push({
    fullName,
    email,
    phoneNumber: phone,
    passwordHash: defaultPasswordHash,
    role: 'Customer',
    isActive,
    createdAt,
    lastLoginAt,
    securityVersion: 1
  });
}

fs.writeFileSync('scratch/seed-users.json', JSON.stringify(users, null, 2), 'utf8');
console.log(`Generated ${users.length} users successfully!`);
console.log(`Active: ${users.filter(u => u.isActive).length}, Inactive: ${users.filter(u => !u.isActive).length}`);

