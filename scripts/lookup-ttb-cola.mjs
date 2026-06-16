import { execFileSync } from "node:child_process";

const curl = (args) =>
  execFileSync("curl.exe", ["-sL", "-A", "Mozilla/5.0", ...args], {
    encoding: "utf8",
    maxBuffer: 20 * 1024 * 1024,
  });

const parseField = (source, label) => {
  const re = new RegExp(`<strong>${label}:\\s*</strong>[\\s\\S]*?&nbsp;\\s*([^<\\n]+)`, "i");
  const m = source.match(re);
  return m ? m[1].trim() : "";
};

const ids = process.argv.slice(2);
for (const ttbId of ids) {
  const page = curl([
    `https://ttbonline.gov/colasonline/viewColaDetails.do?action=publicDisplaySearchBasic&ttbid=${ttbId}`,
  ]);
  console.log(
    JSON.stringify(
      {
        ttbId,
        brandName: parseField(page, "Brand Name"),
        fancifulName: parseField(page, "Fanciful Name"),
        classType: parseField(page, "Class/Type Code"),
        status: parseField(page, "Status"),
      },
      null,
      2,
    ),
  );
}
