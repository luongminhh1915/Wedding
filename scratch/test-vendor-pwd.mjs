async function test() {
  for (const pwd of ["123456", "Password123!", "admin123", "Admin@123", "password"]) {
    const res = await fetch("http://localhost:5008/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "vendor.tina-le-makeup@wedding.com", password: pwd })
    });
    console.log(`Password: ${pwd} => Status: ${res.status}`);
    if (res.ok) {
      console.log("Success! Password is:", pwd);
      break;
    }
  }
}
test();

