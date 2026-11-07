const res = await fetch("https://weddingmatch.website/api/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email: "vendor.ctm-palace@wedding.com", password: "123456" })
});
console.log("Status:", res.status);
const data = await res.json();
console.log("Vendor user:", data.user?.fullName, data.user?.role);

