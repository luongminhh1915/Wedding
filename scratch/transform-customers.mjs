import fs from 'fs';

let content = fs.readFileSync('scratch/all-users-detail-utf8.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
const allUsers = JSON.parse(content);

const customers = allUsers.filter(u => u.Role === 'Customer');
console.log(`Original customers count: ${customers.length}`);

// Target hash for "123456"
const PASSWORD_HASH_123456 = "$2a$11$jaWuWOwGuPi4M97wBtPUT.SFlnN/jCKtNthzVK92YFQx8QuGG5k12";

// Alternative replacement surnames for "Lý"
const replacementSurnames = ["Vũ", "Đặng", "Đỗ", "Bùi", "Hoàng", "Phan", "Phạm", "Trần", "Lê", "Nguyễn"];

// Fun / playful accounts to sprinkle into the dataset
const funAccounts = [
  { fullName: "Thỏ Bảy Màu", email: "tho7mau.official@gmail.com" },
  { fullName: "Trà Sữa Full Topping", email: "trasuafulltopping@gmail.com" },
  { fullName: "Độc Lạ Bình Dương", email: "docla.binhduong@gmail.com" },
  { fullName: "Cưới Thôi Chán Đời", email: "cuoithoi.chandoi@gmail.com" },
  { fullName: "Cô Dâu Tài Phiệt", email: "codau.taiphiet@gmail.com" },
  { fullName: "Chú Rể Quốc Dân", email: "chure.quocdan@gmail.com" },
  { fullName: "Deadline Đi Đám Cưới", email: "deadline.didamcuoi@fpt.edu.vn" },
  { fullName: "FPT Mai Mối Cưới", email: "match.maimoi@fpt.edu.vn" },
  { fullName: "Học Lại Vì Yêu", email: "hoc.lai.viyeu@fpt.edu.vn" },
  { fullName: "Cặp Đôi Hoàn Hảo", email: "capdoi.hoanhao@fpt.edu.vn" },
  { fullName: "Tiền Mừng Đám Cưới", email: "tienmung.damcuoi@fpt.edu.vn" },
  { fullName: "Yêu Em 3000 Năm", email: "yeuem.3000nam@gmail.com" },
  { fullName: "Đức Anh Đẹp Trai", email: "ducanh.dzai@gmail.com" }
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

const usedEmails = new Set();
// Reserve other users' emails (Admins, Vendors)
allUsers.filter(u => u.Role !== 'Customer').forEach(u => {
  if (u.Email) usedEmails.add(u.Email.toLowerCase());
});

let funIndex = 0;
let lyReplaceIndex = 0;

const updatedCustomers = customers.map((c, idx) => {
  let fullName = c.FullName;
  let email = (c.Email || '').toLowerCase();
  const id = c.Id;
  const phoneNumber = c.PhoneNumber && c.PhoneNumber !== 'chua-cap-nhat' ? c.PhoneNumber : `098${String(1000000 + idx).slice(-7)}`;
  const isActive = c.IsActive;

  let isFun = false;

  // Preserve bundaumamtom on the row that already has it
  if (email === 'bundaumamtom@gmail.com') {
    fullName = "Bún Đậu Mắm Tôm";
    email = "bundaumamtom@gmail.com";
    isFun = true;
  } else if (email.includes('testmail.com') || (email === '' && fullName === 'Duc Anh') || idx === 10 || idx === 20 || idx === 30) {
    if (funIndex < funAccounts.length) {
      const fun = funAccounts[funIndex++];
      fullName = fun.fullName;
      email = fun.email;
      isFun = true;
    }
  }

  // 2. Check if surname is "Lý"
  if (!isFun && (fullName.startsWith('Lý ') || fullName.includes(' Lý ') || fullName === 'Lý')) {
    const replacement = replacementSurnames[lyReplaceIndex % replacementSurnames.length];
    lyReplaceIndex++;
    fullName = fullName.replace(/^Lý /, `${replacement} `);
  }

  // 3. Ensure email has ONLY @gmail.com or @fpt.edu.vn
  const shouldConvertToFpt = !isFun && (idx % 4 === 0);
  if (!isFun && ((!email.endsWith('@gmail.com') && !email.endsWith('@fpt.edu.vn')) || (shouldConvertToFpt && email.endsWith('@gmail.com')))) {
    const parts = fullName.trim().split(/\s+/);
    const firstName = parts[parts.length - 1];
    const lastName = parts[0];
    const middleName = parts.slice(1, -1).join('');

    const asciiFirst = removeVietnameseTones(firstName);
    const asciiLast = removeVietnameseTones(lastName);
    const asciiMiddle = removeVietnameseTones(middleName);

    const useFpt = shouldConvertToFpt || (idx % 3 === 0);
    let domain = useFpt ? '@fpt.edu.vn' : '@gmail.com';

    let base;
    if (useFpt) {
      const studentCode = `se${170000 + (idx * 37) % 9999}`;
      if (idx % 2 === 0) {
        base = `${asciiFirst}${asciiLast[0]}${asciiMiddle[0] || 't'}${studentCode}`;
      } else {
        base = `${asciiFirst}.${asciiLast}${studentCode.slice(2, 6)}`;
      }
    } else {
      const year = 1994 + (idx % 9);
      base = `${asciiFirst}.${asciiLast}${year}`;
    }

    email = `${base}${domain}`;
  }

  // 4. If email had "ly" from old surname, clean it
  if (email.includes('ly.hai') || email.includes('ly.long')) {
    email = email.replace('ly.hai', 'hai.tuanbui').replace('ly.long', 'long.tuanpham');
  }

  // 5. Ensure uniqueness of email
  let finalEmail = email;
  let counter = 1;
  const [namePart, domPart] = finalEmail.split('@');
  while (usedEmails.has(finalEmail)) {
    finalEmail = `${namePart}${counter}@${domPart}`;
    counter++;
  }
  usedEmails.add(finalEmail);

  return {
    id,
    fullName,
    email: finalEmail,
    phoneNumber,
    passwordHash: PASSWORD_HASH_123456,
    isActive
  };
});

// Verification check
console.log('\n--- VERIFICATION CHECKS ---');
console.log(`Total updated customers: ${updatedCustomers.length}`);

const lyCheck = updatedCustomers.filter(c => c.fullName.includes('Lý') || c.email.includes('ly.'));
console.log(`Any 'Lý' remaining: ${lyCheck.length}`);
if (lyCheck.length > 0) {
  console.error("FAIL: Found Lý accounts:", lyCheck);
  process.exit(1);
}

const domainCheck = updatedCustomers.filter(c => !c.email.endsWith('@gmail.com') && !c.email.endsWith('@fpt.edu.vn'));
console.log(`Any non-gmail/fpt domain: ${domainCheck.length}`);
if (domainCheck.length > 0) {
  console.error("FAIL: Found invalid domains:", domainCheck);
  process.exit(1);
}

const gmailCount = updatedCustomers.filter(c => c.email.endsWith('@gmail.com')).length;
const fptCount = updatedCustomers.filter(c => c.email.endsWith('@fpt.edu.vn')).length;
console.log(`Gmail count: ${gmailCount} (${(gmailCount/updatedCustomers.length*100).toFixed(1)}%)`);
console.log(`FPT count: ${fptCount} (${(fptCount/updatedCustomers.length*100).toFixed(1)}%)`);

const activeCount = updatedCustomers.filter(c => c.isActive).length;
console.log(`Active customers: ${activeCount} / ${updatedCustomers.length}`);

// Unique email check
const uniqueEmailSet = new Set(updatedCustomers.map(c => c.email));
console.log(`Unique emails: ${uniqueEmailSet.size} == ${updatedCustomers.length}`);
if (uniqueEmailSet.size !== updatedCustomers.length) {
  console.error("FAIL: Duplicate emails found!");
  process.exit(1);
}

// Show fun accounts
console.log('\n--- FUN ACCOUNTS CHECK ---');
const allFun = ["bundaumamtom@gmail.com", ...funAccounts.map(f => f.email)];
allFun.forEach(f => {
  const found = updatedCustomers.find(c => c.email === f);
  if (found) {
    console.log(`✔ Found fun account: "${found.fullName}" <${found.email}>`);
  } else {
    console.log(`✖ Missing fun account: ${f}`);
  }
});

// Save transformed list to scratch
fs.writeFileSync('scratch/transformed-customers.json', JSON.stringify(updatedCustomers, null, 2), 'utf8');
console.log('\nSaved transformed customers to scratch/transformed-customers.json');
