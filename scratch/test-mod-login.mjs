const res = await fetch("https://weddingmatch.website/api/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email: "moderator@wedding.com", password: "123456" })
});
console.log("moderator login status:", res.status);
const data = await res.json();
console.log("moderator user:", data.user?.fullName, data.user?.role);

