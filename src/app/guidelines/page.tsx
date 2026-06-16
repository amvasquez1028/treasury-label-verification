import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { TreasuryLayout } from "@/components/TreasuryLayout";
import { LABEL_VERIFICATION_CSV_TEMPLATE_PATH } from "@/lib/labelVerificationCsv";

const colaRegistryLinks = [
  {
    href: "https://www.ttb.gov/regulated-commodities/labeling/colas",
    label: "Certificates of Label Approval (COLAs)",
  },
  {
    href: "https://www.ttb.gov/regulated-commodities/labeling/labeling-resources",
    label: "TTB Labeling Resources",
  },
  {
    href: "https://ttbonline.gov/colasonline/publicSearchColasBasic.do",
    label: "Public COLA Registry (approved label search)",
  },
  {
    href: "https://www.ttb.gov/news/using-cola-registry-search-certificates",
    label: "How to Search the COLA Registry",
  },
];

const ttbLinks = [
  {
    href: "https://www.ttb.gov/regulated-commodities/beverage-alcohol",
    label: "TTB Beverage Alcohol Overview",
  },
  {
    href: "https://www.ttb.gov/regulated-commodities/beverage-alcohol/distilled-spirits/labeling",
    label: "Distilled Spirits Labeling",
  },
  {
    href: "https://www.ttb.gov/regulated-commodities/beverage-alcohol/wine/labeling",
    label: "Wine Labeling",
  },
  {
    href: "https://www.ttb.gov/regulated-commodities/beverage-alcohol/beer/labeling",
    label: "Malt Beverage Labeling",
  },
  {
    href: "https://www.ttb.gov/regulated-commodities/beverage-alcohol/distilled-spirits/ds-labeling-home/ds-health-warning",
    label: "Distilled Spirits Health Warning Statement",
  },
];

const ttbElements = [
  {
    title: "Brand name",
    description:
      "The brand name as it appears on the approved Certificate of Label Approval (COLA). Must match the approved spelling and presentation.",
  },
  {
    title: "Class / type designation",
    description:
      "The standardized class or type statement (e.g., Straight Bourbon Whiskey, Distilled Gin, Table Wine, Malt Beverage) required for the product category.",
  },
  {
    title: "Alcohol content",
    description:
      "Alcohol by volume (ABV) declaration. Wine and malt beverages may use alternate TTB-approved formats; select the correct product category when verifying.",
  },
  {
    title: "Net contents",
    description:
      "The fill volume or weight of the container (e.g., 750 mL, 1 L, 12 fl oz) in metric or U.S. customary units as permitted for the product type.",
  },
  {
    title: "Name and address of bottler / producer",
    description:
      "The name and business address of the bottler, producer, importer, or other responsible party as required on the label.",
  },
  {
    title: "Country of origin (imports)",
    description:
      "For imported products, the country of origin must appear on the label. Domestic products do not require this field — leave it blank in verification parameters.",
  },
  {
    title: "Government Health Warning Statement",
    description:
      "The mandatory TTB warning text for distilled spirits, wine, and malt beverages, with the opening phrase (typically GOVERNMENT WARNING:) displayed prominently in bold.",
  },
];

export default function GuidelinesPage() {
  return (
    <AuthGuard>
      <TreasuryLayout
        title="Guidelines"
        subtitle="How to use the Label Verification Portal and interpret results."
      >
        <div className="mx-auto max-w-6xl space-y-8 px-4 pb-10">
          <section className="parameter-card">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">
              Using the Verify Page
            </h3>
            <ol className="mt-4 list-decimal space-y-3 pl-5 text-[var(--color-base-darkest)]">
              <li>
                Open <strong>Verify</strong> from the navigation menu. Each label row supports one
                image and its own verification parameters.
              </li>
              <li>
                <strong>Load parameters from CSV:</strong> Click <strong>CSV manifest</strong> and
                select a spreadsheet with one row per label. The portal prompts you to attach PNG or
                JPEG files whose filenames match the <strong>labelImage</strong> column. Download the{" "}
                <a
                  href={LABEL_VERIFICATION_CSV_TEMPLATE_PATH}
                  download
                  className="font-semibold text-[var(--color-primary-darker)] underline"
                >
                  CSV template
                </a>{" "}
                for required columns and an example row.
              </li>
              <li>
                <strong>Reviewer samples:</strong> Click <strong>Load samples</strong> to load the
                five Texas ODP demo labels with parameters and artwork prefilled.
              </li>
              <li>
                <strong>Manual entry:</strong> Use <strong>Add label</strong> for extra rows. Attach
                each <strong>Label image (PNG or JPEG)</strong>, then use <strong>Show parameters</strong>{" "}
                to review or edit brand name, fanciful name (if applicable), bottler/producer address,
                country of origin (imports only), product category, class/type designation, ABV %,
                net contents, full TTB warning text, and bold warning phrase.
              </li>
              <li>
                Use <strong>Show parameters</strong> / <strong>Hide parameters</strong> to collapse the
                form when reviewing images only.
              </li>
              <li>
                Click <strong>Verify labels</strong> to run OCR and field-level matching for every
                label with an image. The portal verifies labels one at a time at full resolution;
                Texas ODP flat artwork may take about 30 seconds per label. Each result shows its own
                status badge, confidence score (when OCR succeeds), and field-level reasoning.
              </li>
              <li>
                Review results below the form. Outcomes include pass, review, fail, unreadable (OCR
                could not read the image), and processing error (for example when OCR is not
                available on the server). Each completed run is saved to <strong>History</strong> in
                your browser for audit review. See also the in-app{" "}
                <Link
                  href="/security/"
                  className="font-semibold text-[var(--color-primary-darker)] underline"
                >
                  Security
                </Link>{" "}
                page for authentication, upload controls, and deployment notes.
              </li>
            </ol>
          </section>

          <section className="parameter-card">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">
              TTB Label Requirements
            </h3>
            <p className="mt-4 text-[var(--color-base-darkest)]">
              Federal alcohol beverage labels must include mandatory information governed by TTB
              regulations. This portal verifies seven core elements against OCR-extracted artwork text.
            </p>
            <ol className="mt-4 list-decimal space-y-3 pl-5 text-[var(--color-base-darkest)]">
              {ttbElements.map((element) => (
                <li key={element.title}>
                  <strong>{element.title}</strong> — {element.description}
                </li>
              ))}
            </ol>
            <ul className="mt-6 list-disc space-y-2 pl-5 text-sm">
              {ttbLinks.map((link) => (
                <li key={link.href}>
                  <a
                    href={link.href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="font-semibold text-[var(--color-primary-darker)] underline"
                  >
                    {link.label}
                  </a>
                </li>
              ))}
            </ul>
          </section>

          <section className="parameter-card">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">
              COLA Registry &amp; Approved Labels
            </h3>
            <p className="mt-4 text-[var(--color-base-darkest)]">
              The TTB Public COLA Registry lists government-approved label certificates. Use it to
              research approved artwork, compare mandatory elements, and understand how agents validate
              labels against COLA metadata.
            </p>
            <ul className="mt-4 list-disc space-y-2 pl-5 text-sm">
              {colaRegistryLinks.map((link) => (
                <li key={link.href}>
                  <a
                    href={link.href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="font-semibold text-[var(--color-primary-darker)] underline"
                  >
                    {link.label}
                  </a>
                </li>
              ))}
            </ul>
          </section>

          <section className="parameter-card">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">
              Verification Parameter Framework
            </h3>
            <ul className="mt-4 list-disc space-y-2 pl-5 text-[var(--color-base-darkest)]">
              <li><strong>Brand name</strong> — Expected product brand as approved on the COLA.</li>
              <li>
                <strong>Class / type designation</strong> — Standardized product class or type
                statement on the label.
              </li>
              <li>
                <strong>Product category</strong> — Distilled spirits, wine, or beer/malt beverage;
                affects alcohol content matching notes for alternate TTB formats.
              </li>
              <li><strong>ABV %</strong> — Declared alcohol by volume on the label.</li>
              <li>
                <strong>Net contents</strong> — Container fill volume or weight (e.g., 750 mL, 12 fl
                oz).
              </li>
              <li>
                <strong>Bottler / producer address</strong> — Name and business address of the
                responsible party on the label.
              </li>
              <li>
                <strong>Country of origin</strong> — Required for imports; leave blank for domestic
                products (verification passes automatically).
              </li>
              <li>
                <strong>TTB warning text</strong> — Full government warning statement required on
                the label.
              </li>
              <li>
                <strong>Bold warning phrase</strong> — The phrase that must appear prominently
                (typically &quot;GOVERNMENT WARNING:&quot;).
              </li>
            </ul>
          </section>

          <section className="parameter-card">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">
              Decision Framework
            </h3>
            <p className="mt-4 text-sm text-[var(--color-base-darkest)]">
              Compliance outcome (pass / review / fail) is separate from confidence display bands.
              Overall confidence is the minimum of all field confidences.
            </p>
            <div className="mt-4 overflow-x-auto">
              <table className="w-full min-w-[480px] text-left text-sm">
                <thead>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <th className="px-3 py-2 font-bold">Compliance outcome</th>
                    <th className="px-3 py-2 font-bold">Criteria</th>
                  </tr>
                </thead>
                <tbody className="text-[var(--color-base-darkest)]">
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2 font-semibold text-[var(--color-verdict-green)]">Pass</td>
                    <td className="px-3 py-2">
                      All required fields match and overall confidence is at or above 90%.
                    </td>
                  </tr>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2 font-semibold text-[var(--color-verdict-yellow)]">Review</td>
                    <td className="px-3 py-2">
                      No field hard-fail, but overall confidence is below 90% — manual review recommended.
                    </td>
                  </tr>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2 font-semibold text-[var(--color-verdict-red)]">Fail</td>
                    <td className="px-3 py-2">
                      At least one required field does not match (brand, ABV, warning text, bold phrase, etc.).
                    </td>
                  </tr>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2 font-semibold text-[var(--color-verdict-red)]">Timeout</td>
                    <td className="px-3 py-2">
                      Processing exceeded the 5-second per-label limit — retry with a clearer image.
                    </td>
                  </tr>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2 font-semibold text-[#4a5568]">Unreadable</td>
                    <td className="px-3 py-2">
                      OCR could not extract enough reliable text (blur, glare, blank crop, or low
                      contrast). Field checks are skipped — request a clearer photo or scan from the
                      applicant instead of failing on content mismatch.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <h4 className="mt-6 text-sm font-bold text-[var(--color-primary-darker)]">
              Confidence display bands (UI)
            </h4>
            <div className="mt-2 overflow-x-auto">
              <table className="w-full min-w-[480px] text-left text-sm">
                <thead>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <th className="px-3 py-2 font-bold">Display %</th>
                    <th className="px-3 py-2 font-bold">Badge</th>
                    <th className="px-3 py-2 font-bold">Agent action</th>
                  </tr>
                </thead>
                <tbody className="text-[var(--color-base-darkest)]">
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2">≥ 90%</td>
                    <td className="px-3 py-2 font-semibold text-[var(--color-verdict-green)]">Green</td>
                    <td className="px-3 py-2">High confidence — trust the automated result.</td>
                  </tr>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2">60–89%</td>
                    <td className="px-3 py-2 font-semibold text-[var(--color-verdict-yellow)]">Yellow</td>
                    <td className="px-3 py-2">
                      Review recommended — click the score for expected vs extracted reasoning.
                    </td>
                  </tr>
                  <tr className="border-b border-[var(--color-base-lighter)]">
                    <td className="px-3 py-2">&lt; 60%</td>
                    <td className="px-3 py-2 font-semibold text-[var(--color-verdict-red)]">Red</td>
                    <td className="px-3 py-2">
                      Low OCR confidence — verify by eye or upload a clearer image.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p className="mt-4 text-sm text-[var(--color-base-darkest)]">
              A label can <strong>fail</strong> compliance while still showing a yellow confidence badge
              (for example, 88% confidence with a warning-text mismatch). Outcome and confidence band are
              evaluated independently.
            </p>
            <p className="mt-3 text-sm text-[var(--color-base-darkest)]">
              When a <strong>government warning</strong> field fails, the Verify results list shows inline
              confidence commentary (green / yellow / red band plus a short note). Expand the field row for
              expected vs extracted values and the full reasoning string from the server.
            </p>
          </section>

          <section id="ocr" className="parameter-card scroll-mt-8">
            <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">
              OCR and Local Processing
            </h3>
            <p className="mt-4 text-[var(--color-base-darkest)]">
              This portal uses Tesseract OCR running on the application server. Uploaded images are processed locally — text is extracted from the label artwork and compared against your verification parameters using normalized string matching and field-specific rules.
            </p>
            <ul className="mt-4 list-disc space-y-2 pl-5 text-[var(--color-base-darkest)]">
              <li>Supported formats: PNG and JPEG.</li>
              <li>OCR quality depends on image resolution, contrast, and font clarity.</li>
              <li>Processing runs server-side; raw OCR text is available in API responses for audit.</li>
              <li>CSV manifest upload loads all verification parameters in one step; images are matched by filename.</li>
            </ul>
          </section>
        </div>
      </TreasuryLayout>
    </AuthGuard>
  );
}
