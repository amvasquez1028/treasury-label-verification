import fs from "node:fs";

const lines = fs.readFileSync("c:/Users/Alaina/Downloads/Approved_Product_Label_Search_20260614.csv", "utf8").split("\n");

const searches = [
  { name: "JD 40", fn: (l) => l.includes("JACK DANIEL") && l.includes("TENNESSEE WHISKEY") && l.includes('","40",') },
  { name: "Remy Club", fn: (l) => l.includes("REMY MARTIN") && l.includes("CLUB") },
  { name: "Remy FC 40", fn: (l) => l.includes("REMY MARTIN") && l.includes("FINE CHAMPAGNE") && l.includes('","40",') },
  { name: "Leopard Chard", fn: (l) => l.includes("LEOPARD") && l.includes("CHARDONNAY") },
  { name: "Ven Sierra", fn: (l) => l.includes("LA VENENOSA") && l.includes("SIERRA") },
  { name: "Agave Austral", fn: (l) => l.includes("AGAVE") && l.includes("AUSTRAL") },
  { name: "Act Of", fn: (l) => l.includes("ACT OF") },
  { name: "Juniper Tree", fn: (l) => l.includes("JUNIPER TREE") },
  { name: "House Porfidio", fn: (l) => l.includes("HOUSE OF PORFIDIO") || l.includes("JUNIPER TREE") },
];

for (const { name, fn } of searches) {
  const hits = lines.filter(fn).slice(0, 5);
  console.log(`\n=== ${name} (${hits.length}) ===`);
  for (const hit of hits) {
    console.log(hit.slice(0, 260));
  }
}

const ids = [
  "13343001000271",
  "14106001000237",
  "18303001000896",
  "14086001000311",
  "14086001000327",
  "14086001000323",
  "09036001000049",
  "10160001000332",
];
console.log("\n=== TTB ID lookup ===");
for (const id of ids) {
  const hit = lines.find((l) => l.includes(id));
  console.log(id, hit ? hit.slice(0, 240) : "NOT FOUND");
}

const extra = [
  { name: "JD Old No 40", fn: (l) => l.includes("JACK DANIEL") && l.includes("OLD NO") && l.includes('","40",') },
  { name: "Remy Club", fn: (l) => l.includes("REMY") && l.includes("CLUB") },
  { name: "Ven Occidental", fn: (l) => l.includes("LA VENENOSA") && l.includes("OCCIDENTAL") },
  { name: "Gin Porfidio", fn: (l) => l.includes("GIN") && (l.includes("PORFIDIO") || l.includes("GRASSL")) },
  { name: "Leopard Leap", fn: (l) => l.includes("LEOPARD") && l.includes("LEAP") },
  { name: "Act Treason", fn: (l) => l.includes("TREASON") && l.includes("AGAVE") },
];
for (const { name, fn } of extra) {
  const hits = lines.filter(fn).slice(0, 3);
  console.log(`\n=== ${name} ===`);
  hits.forEach((h) => console.log(h.slice(0, 240)));
}
