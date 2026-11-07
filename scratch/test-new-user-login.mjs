const res = await fetch("https://weddingmatch.website/api/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email: "vy.nguyen93@gmail.com", password: "123456" })
});
console.log("Status:", res.status);
const data = await res.json();
console.log("User:", JSON.stringify(data.user, null, 2));

