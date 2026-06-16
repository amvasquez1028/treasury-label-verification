"use client";

import {
  ChangeEvent,
  FormEvent,
  useState,
} from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { ConfidenceScore } from "@/components/ConfidenceScore";
import { FieldResultDetail } from "@/components/FieldResultDetail";
import { LabelFileInput } from "@/components/LabelFileInput";
import { LabelVerificationFields } from "@/components/LabelVerificationFields";
import { ProcessingErrorPanel } from "@/components/ProcessingErrorPanel";
import { TreasuryLayout } from "@/components/TreasuryLayout";
import { UnreadableResultPanel } from "@/components/UnreadableResultPanel";
import { VerificationStatusBadge } from "@/components/VerificationStatusBadge";
import {
  BatchVerificationResult,
  verifyLabelsSequential,
} from "@/lib/api";
import { cloneExpectedLabelFields } from "@/lib/defaultExpected";
import { parseColaCachePayload } from "@/lib/parseColaCache";
import {
  saveBatchFailureToHistory,
  saveVerificationToHistory,
} from "@/lib/history";
import { prepareImageForServerUpload } from "@/lib/clientOcr";
import { assessFilesQuality, ImageQualityWarning } from "@/lib/imageQuality";
import {
  parseLabelVerificationCsv,
  promptForLabelImages,
  readTextFromFile,
  resolveLabelImageFile,
  type LabelVerificationCsvRow,
} from "@/lib/labelVerificationCsv";
import { fetchSampleFile, loadSampleManifest } from "@/lib/sampleManifest";

type LabelRow = {
  id: string;
  file: File | null;
  previewUrl: string | null;
  expectedImageName: string;
  expected: ReturnType<typeof cloneExpectedLabelFields>;
  fancifulName: string;
  prefillRevision: number;
  fieldsExpanded: boolean;
};

type ResultItem = BatchVerificationResult["items"][number];

const createLabelRow = (file: File | null = null): LabelRow => ({
  id: crypto.randomUUID(),
  file,
  previewUrl: file ? URL.createObjectURL(file) : null,
  expectedImageName: file?.name ?? "",
  expected: cloneExpectedLabelFields(),
  fancifulName: "",
  prefillRevision: 0,
  fieldsExpanded: !file,
});

const csvRowsToLabelRows = async (
  csvRows: LabelVerificationCsvRow[],
  localFiles: File[],
): Promise<LabelRow[]> => {
  const rows: LabelRow[] = [];

  for (const item of csvRows) {
    const row = createLabelRow();
    row.expectedImageName = item.labelImage;
    row.expected = item.expected;
    row.fancifulName = item.fancifulName;
    row.prefillRevision = 1;
    row.fieldsExpanded = false;

    const match = await resolveLabelImageFile(item.labelImage, localFiles);
    if (match) {
      row.file = match;
      row.previewUrl = URL.createObjectURL(match);
    }

    rows.push(row);
  }

  return rows;
};

export default function VerifyPage() {
  const [rows, setRows] = useState<LabelRow[]>([createLabelRow()]);
  const [results, setResults] = useState<ResultItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadingMessage, setLoadingMessage] = useState("Verifying label(s)");
  const [qualityWarnings, setQualityWarnings] = useState<ImageQualityWarning[]>([]);
  const [csvRowCount, setCsvRowCount] = useState(0);

  const readyCount = rows.filter((row) => row.file !== null).length;

  const handleCsvManifest = async (event: ChangeEvent<HTMLInputElement>) => {
    const input = event.target;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    let text: string;
    try {
      text = await readTextFromFile(file);
    } catch {
      input.value = "";
      setError("Could not read the CSV file. Try selecting the manifest again.");
      return;
    }
    input.value = "";

    setError(null);
    setLoading(true);
    setLoadingMessage("Loading CSV manifest...");
    try {
      const csvRows = parseLabelVerificationCsv(text);
      if (csvRows.length === 0) {
        throw new Error(
          "No label rows found in CSV. Download the template from Guidelines.",
        );
      }

      setRows((current) => {
        current.forEach((row) => {
          if (row.previewUrl) {
            URL.revokeObjectURL(row.previewUrl);
          }
        });
        return [];
      });

      let nextRows = await csvRowsToLabelRows(csvRows, []);

      const missingAfterSampleFetch = nextRows.filter(
        (row) => !row.file && row.expectedImageName,
      );
      if (missingAfterSampleFetch.length > 0) {
        setLoadingMessage("Select matching label images...");
        const localFiles = await promptForLabelImages();
        nextRows = await csvRowsToLabelRows(csvRows, localFiles);
      }

      setRows(nextRows);
      setCsvRowCount(csvRows.length);
      setResults([]);

      const attachedFiles = nextRows
        .map((row) => row.file)
        .filter((rowFile): rowFile is File => rowFile !== null);
      setQualityWarnings(
        attachedFiles.length > 0 ? await assessFilesQuality(attachedFiles) : [],
      );

      const unmatched = nextRows.filter((row) => !row.file && row.expectedImageName);
      if (unmatched.length > 0) {
        setError(
          `Loaded ${csvRows.length} label parameter row(s). Attach images for: ${unmatched
            .map((row) => row.expectedImageName)
            .join(", ")}`,
        );
      }
    } catch (csvError) {
      const message =
        csvError instanceof Error ? csvError.message : "Could not load CSV manifest.";
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  const handleLoadSamples = async () => {
    setError(null);
    setLoading(true);
    setLoadingMessage("Loading samples...");
    try {
      const manifest = await loadSampleManifest();
      const sampleItems = manifest.slice(0, 5);

      if (sampleItems.length === 0) {
        throw new Error("No demo samples found. Run pnpm setup:reviewer-pack.");
      }

      setRows((current) => {
        current.forEach((row) => {
          if (row.previewUrl) {
            URL.revokeObjectURL(row.previewUrl);
          }
        });
        return [];
      });

      const nextRows: LabelRow[] = [];

      for (const item of sampleItems) {
        const sampleFile = await fetchSampleFile(item.file);
        const row = createLabelRow(sampleFile);
        row.expectedImageName = item.file;
        row.fieldsExpanded = false;

        if (item.expectedLabelFields) {
          const { fancifulName, expected } = parseColaCachePayload({
            ...item.expectedLabelFields,
            fancifulName: item.fancifulName ?? item.expectedLabelFields.fancifulName,
          });
          row.expected = expected;
          row.fancifulName = fancifulName;
          row.prefillRevision = 1;
        }

        nextRows.push(row);
      }

      setRows(nextRows);
      setCsvRowCount(0);
      setResults([]);
      setQualityWarnings([]);
    } catch (sampleError) {
      setError(
        sampleError instanceof Error ? sampleError.message : "Could not load samples.",
      );
    } finally {
      setLoading(false);
    }
  };

  const handleAddLabel = () => {
    setRows((current) => [...current, createLabelRow()]);
  };

  const handleRemoveLabel = (id: string) => {
    setRows((current) => {
      if (current.length <= 1) {
        return current;
      }
      const removed = current.find((r) => r.id === id);
      if (removed?.previewUrl) {
        URL.revokeObjectURL(removed.previewUrl);
      }
      return current.filter((row) => row.id !== id);
    });
  };

  const handleDuplicateParameters = (sourceId: string) => {
    const source = rows.find((row) => row.id === sourceId);
    if (!source) {
      return;
    }

    setRows((current) =>
      current.map((row) =>
        row.id === sourceId
          ? row
          : {
              ...row,
              expected: { ...source.expected },
              fancifulName: source.fancifulName,
            },
      ),
    );
  };

  const handleToggleFields = (id: string) => {
    setRows((current) =>
      current.map((row) =>
        row.id === id ? { ...row, fieldsExpanded: !row.fieldsExpanded } : row,
      ),
    );
  };

  const handleFileChange = async (id: string, event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    setRows((current) =>
      current.map((row) => {
        if (row.id !== id) {
          return row;
        }
        if (row.previewUrl) {
          URL.revokeObjectURL(row.previewUrl);
        }
        return {
          ...row,
          file,
          previewUrl: file ? URL.createObjectURL(file) : null,
          expectedImageName: file?.name ?? row.expectedImageName,
        };
      }),
    );
    setResults([]);
    setError(null);

    if (file) {
      const warnings = await assessFilesQuality([file]);
      setQualityWarnings(warnings);
    }
  };

  const handleFileRemove = (id: string) => {
    setRows((current) =>
      current.map((row) => {
        if (row.id !== id) {
          return row;
        }
        if (row.previewUrl) {
          URL.revokeObjectURL(row.previewUrl);
        }
        return { ...row, file: null, previewUrl: null };
      }),
    );
    setResults([]);
    setError(null);
  };

  const handleExpectedChange = (
    id: string,
    field: keyof LabelRow["expected"],
    value: string,
  ) => {
    setRows((current) =>
      current.map((row) => {
        if (row.id !== id) {
          return row;
        }

        return {
          ...row,
          expected: {
            ...row.expected,
            [field]: field === "abvPercent" ? Number(value) : value,
          },
        };
      }),
    );
  };

  const handleVerify = async (event: FormEvent) => {
    event.preventDefault();

    const items = rows.filter((row) => row.file !== null);
    if (items.length === 0) {
      setError("Add at least one label image to verify.");
      return;
    }

    setLoading(true);
    setLoadingMessage(`Verifying ${items.length} label(s)`);
    setError(null);
    setResults([]);

    try {
      const preparedFiles = await Promise.all(
        items.map((row) => prepareImageForServerUpload(row.file!)),
      );

      const batch = await verifyLabelsSequential(
        items.map((row, index) => ({
          file: preparedFiles[index],
          expected: {
            ...row.expected,
            ...(row.fancifulName.trim()
              ? { fancifulName: row.fancifulName.trim() }
              : {}),
          },
        })),
        (index, total, fileName) => {
          setLoadingMessage(`Verifying label ${index + 1} of ${total}: ${fileName}`);
        },
      );

      batch.items.forEach((item) => {
        const brandName =
          items.find((row) => row.file?.name === item.fileName)?.expected.brandName ??
          "Unknown brand";

        if (item.result) {
          saveVerificationToHistory(brandName, item.result, item.fileName);
        } else if (item.error) {
          saveBatchFailureToHistory(brandName, item.fileName, item.error);
        }
      });

      setResults(batch.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Verification failed.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthGuard>
      <TreasuryLayout
        title="Label Verification"
        subtitle="Compare label image text to treasury application values loaded from CSV or entered below."
      >
        <div className="mx-auto max-w-6xl px-4 pb-10 pt-4">
          <form onSubmit={handleVerify} className="space-y-6">
            <section
              className="rounded-lg border-2 border-dashed border-[var(--color-base-lighter)] p-6"
              aria-label="Label intake"
            >
              <p className="text-sm text-[var(--color-base-darkest)]">
                Upload a CSV manifest to load verification parameters, then attach matching
                PNG or JPEG label images for each row.
              </p>
              <div className="mt-4 flex flex-wrap gap-3">
                <label className="cursor-pointer rounded border border-[var(--color-primary-dark)] px-4 py-3 text-sm font-semibold text-[var(--color-primary-darker)] hover:bg-[var(--color-base-lighter)]">
                  CSV manifest
                  <input
                    type="file"
                    accept=".csv,text/csv"
                    className="sr-only"
                    onChange={(event) => void handleCsvManifest(event)}
                    aria-label="Upload CSV manifest with label parameters and image filenames"
                  />
                </label>
                <button
                  type="button"
                  onClick={() => void handleLoadSamples()}
                  disabled={loading}
                  className="rounded border border-[var(--color-primary-dark)] px-4 py-3 text-sm font-semibold text-[var(--color-primary-darker)] hover:bg-[var(--color-base-lighter)] disabled:opacity-60"
                >
                  Load samples
                </button>
                <button
                  type="button"
                  onClick={handleAddLabel}
                  className="rounded border border-[var(--color-base-lighter)] px-4 py-3 text-sm font-semibold hover:bg-[var(--color-base-lighter)]"
                >
                  Add label
                </button>
              </div>
              {csvRowCount > 0 ? (
                <p className="mt-2 text-xs text-[var(--color-base)]">
                  CSV manifest loaded: {csvRowCount} label parameter row(s).
                </p>
              ) : null}
            </section>

            {qualityWarnings.length > 0 ? (
              <div
                className="rounded border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900"
                role="status"
              >
                <p className="font-semibold">Image quality notes (non-blocking)</p>
                <ul className="mt-1 list-disc pl-5">
                  {qualityWarnings.map((w) => (
                    <li key={`${w.id}-${w.message}`}>{w.message}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            {rows.map((row, index) => (
              <section key={row.id} className="parameter-card space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h4 className="font-bold text-[var(--color-primary-darker)]">
                    Label {index + 1}
                    {row.file
                      ? `: ${row.file.name}`
                      : row.expectedImageName
                        ? `: ${row.expectedImageName}`
                        : ""}
                  </h4>
                  <div className="flex flex-wrap gap-2">
                    {index > 0 ? (
                      <button
                        type="button"
                        onClick={() => handleDuplicateParameters(rows[0].id)}
                        className="rounded border border-[var(--color-base-lighter)] px-3 py-1 text-xs font-semibold hover:bg-[var(--color-base-lighter)]"
                      >
                        Copy parameters from Label 1
                      </button>
                    ) : null}
                    <button
                      type="button"
                      onClick={() => handleToggleFields(row.id)}
                      className="rounded border border-[var(--color-base-lighter)] px-3 py-1 text-xs font-semibold hover:bg-[var(--color-base-lighter)]"
                      aria-expanded={row.fieldsExpanded}
                    >
                      {row.fieldsExpanded ? "Hide parameters" : "Show parameters"}
                    </button>
                    {rows.length > 1 ? (
                      <button
                        type="button"
                        onClick={() => handleRemoveLabel(row.id)}
                        className="rounded border border-red-300 px-3 py-1 text-xs font-semibold text-red-800 hover:bg-red-50"
                      >
                        Remove
                      </button>
                    ) : null}
                  </div>
                </div>

                {row.previewUrl ? (
                  <img
                    src={row.previewUrl}
                    alt={`Preview of ${row.file?.name ?? "label"}`}
                    className="max-h-32 rounded border border-[var(--color-base-lighter)] object-contain"
                  />
                ) : null}

                <LabelFileInput
                  label="Label image (PNG or JPEG)"
                  fileName={row.file?.name ?? null}
                  onChange={(event) => void handleFileChange(row.id, event)}
                  onRemove={() => handleFileRemove(row.id)}
                  ariaLabel={`Label ${index + 1} image upload`}
                />

                {row.fieldsExpanded ? (
                  <LabelVerificationFields
                    key={`${row.id}-${row.prefillRevision}`}
                    expected={row.expected}
                    onChange={(field, value) => handleExpectedChange(row.id, field, value)}
                    fancifulName={row.fancifulName}
                    onFancifulNameChange={(value) =>
                      setRows((current) =>
                        current.map((item) =>
                          item.id === row.id ? { ...item, fancifulName: value } : item,
                        ),
                      )
                    }
                    prefillRevision={row.prefillRevision}
                    idPrefix={`label-${index + 1}`}
                  />
                ) : null}
              </section>
            ))}

            {error ? (
              <p
                className="rounded border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800"
                role="alert"
              >
                {error}
              </p>
            ) : null}

            <button
              type="submit"
              disabled={loading || readyCount === 0}
              className="rounded bg-[var(--color-primary-darker)] px-6 py-3 text-lg font-semibold text-white hover:bg-[var(--color-primary-dark)] disabled:opacity-60"
            >
              {loading ? loadingMessage : `Verify labels (${readyCount})`}
            </button>
          </form>

          {results.length > 0 ? (
            <section className="mt-10 space-y-6" aria-label="Verification results">
              <h3 className="text-xl font-bold text-[var(--color-primary-darker)]">Results</h3>
              {results.map((item) => (
                <div key={item.fileName} className="parameter-card">
                  <h4 className="font-bold text-[var(--color-primary-darker)]">{item.fileName}</h4>
                  {item.error && !item.result ? (
                    <div className="mt-4 space-y-3">
                      <VerificationStatusBadge status="processing_error" />
                      <ProcessingErrorPanel error={item.error} />
                    </div>
                  ) : null}
                  {item.result ? (
                    <div className="mt-4 space-y-3">
                      <div className="flex flex-wrap items-center gap-3">
                        <VerificationStatusBadge status={item.result.overallStatus} />
                        {item.result.overallStatus !== "unreadable" ? (
                          <ConfidenceScore value={item.result.overallConfidence} label="Overall" />
                        ) : null}
                        <span className="text-xs text-[var(--color-base)]">
                          {item.result.processingTimeMs} ms
                        </span>
                      </div>
                      {item.result.overallStatus === "unreadable" ? (
                        <UnreadableResultPanel
                          statusMessage={item.result.statusMessage}
                          agentGuidance={item.result.agentGuidance}
                          rawOcrText={item.result.rawOcrText}
                          fields={item.result.fields}
                        />
                      ) : (
                        <ul className="space-y-2 text-sm">
                          {item.result.fields.map((field) => (
                            <FieldResultDetail key={field.fieldName} field={field} />
                          ))}
                        </ul>
                      )}
                    </div>
                  ) : null}
                </div>
              ))}
            </section>
          ) : null}
        </div>
      </TreasuryLayout>
    </AuthGuard>
  );
}
