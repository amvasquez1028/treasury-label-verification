const res = await fetch("https://ttbonline.gov/colasonline/publicSearchColasBasic.do", {
  headers: { "User-Agent": "Mozilla/5.0" },
});
const html = await res.text();
const names = [...html.matchAll(/name="([^"]+)"/g)].map((m) => m[1]);
console.log([...new Set(names)].filter((n) => /search|brand|date|fanciful|product/i.test(n)).join("\n"));
