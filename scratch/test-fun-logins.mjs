const testList = [
  { email: 'tho7mau.official@gmail.com', name: 'Thỏ Bảy Màu' },
  { email: 'bundaumamtom@gmail.com', name: 'Bún Đậu Mắm Tôm' },
  { email: 'trasuafulltopping@gmail.com', name: 'Trà Sữa Full Topping' },
  { email: 'deadline.didamcuoi@fpt.edu.vn', name: 'Deadline Đi Đám Cưới' },
  { email: 'match.maimoi@fpt.edu.vn', name: 'FPT Mai Mối Cưới' },
  { email: 'ducanh.dzai@gmail.com', name: 'Đức Anh Đẹp Trai' }
];

for (const t of testList) {
  const res = await fetch('https://weddingmatch.website/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: t.email, password: '123456' })
  });
  console.log(`${t.name} <${t.email}> => Status: ${res.status}`);
  if (res.ok) {
    const d = await res.json();
    console.log(`  Logged in as: ${d.user?.fullName} | Role: ${d.user?.role}`);
  }
}

