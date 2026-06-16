import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const cookieJar = path.join(__dirname, "_ttb_cookies.txt");

const curl = (args) =>
  execFileSync("curl.exe", ["-sL", "-A", "Mozilla/5.0", "-c", cookieJar, "-b", cookieJar, ...args], {
    encoding: "utf8",
    maxBuffer: 20 * 1024 * 1024,
  });

try {
  fs.unlinkSync(cookieJar);
} catch {
  // fresh session
}

const searchBody =
  "searchCriteria.dateCompletedFrom=06/15/2011&searchCriteria.dateCompletedTo=06/14/2026&searchCriteria.productOrFancifulName=REMY%20MARTIN&searchCriteria.productNameSearchType=B";

let html = curl([
  "-X",
  "POST",
  "https://ttbonline.gov/colasonline/publicSearchColasBasicProcess.do?action=search",
  "-d",
  searchBody,
]);

const rows = [];
for (let page = 0; page < 5; page += 1) {
  const matches = [...html.matchAll(
    /ttbid=(\d{14})[\s\S]*?<td width="82">([^<]*)<\/td>\s*<td width="83">([^<]*)<\/td>/g,
  )];
  for (const m of matches) {
    rows.push({ ttbId: m[1], fancifulName: m[2].trim(), brandName: m[3].trim() });
  }

  const next = html.match(/publicPageBasicCola.do\?action=page&pgfcn=nextset/);
  if (!next) {
    break;
  }

  html = curl(["https://ttbonline.gov/colasonline/publicPageBasicCola.do?action=page&pgfcn=nextset"]);
}

const clubHits = rows.filter(
  (r) => /club/i.test(r.fancifulName) || /club/i.test(r.brandName),
);
console.log("Total rows:", rows.length);
console.log("Club hits:", clubHits);
console.log(
  "All fanciful:",
  rows.map((r) => `${r.ttbId}: ${r.fancifulName || "(none)"}`).join("\n"),
);
