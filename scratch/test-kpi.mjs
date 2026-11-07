const res = await fetch("https://weddingmatch.website/api/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email: "anhduc21165@gmail.com", password: "password" })
});
console.log("Login status:", res.status);
if (res.ok) {
  const data = await res.json();
  const token = data.token;
  const kpiRes = await fetch("https://weddingmatch.website/api/admin/dashboard", {
    headers: { Authorization: `Bearer ${token}` }
  });
  console.log("Dashboard status:", kpiRes.status);
  if (kpiRes.ok) {
    const kpi = await kpiRes.json();
    console.log("KPI Data:", JSON.stringify(kpi, null, 2));
  }
}

