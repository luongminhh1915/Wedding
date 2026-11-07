async function test() {
  const res = await fetch("http://localhost:5008/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: "moderator@wedding.com", password: "123456" })
  });
  console.log("moderator@wedding.com login:", res.status);
  const data = await res.json();
  console.log("Role:", data.user?.role);
}
test();

