async function test() {
  for (const pwd of ["123456", "Password123!", "admin123", "Admin@123"]) {
    const res = await fetch("http://localhost:5008/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "admin@wedding.com", password: pwd })
    });
    console.log(`Password: ${pwd} => Status: ${res.status}`);
    const text = await res.text();
    console.log(`Body: ${text.slice(0, 100)}`);
    if (res.ok) break;
  }
}
test();

