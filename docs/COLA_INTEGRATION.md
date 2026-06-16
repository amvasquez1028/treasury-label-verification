# COLA integration

## Production verification (primary)

Agents enter **treasury application values** (brand, class, ABV, net contents, warning text, etc.) in the Verify UI. The server OCRs the label image and **matches extracted text to those expected values**. No TTB ID or prior COLA approval is required — this supports labels that are still in application review.

```
POST /api/v1/verify
POST /api/v1/verify/batch
```

Request body includes the image plus `expected` / `expectedList` JSON with `ExpectedLabelFields`.

## Demo / testing only

The following are **not** required for production verification:

| Feature | Purpose |
|---------|---------|
| `GET /api/v1/cola/{ttbId}/expected-fields` | Optional UI prefill from a **local cache** of approved labels |
| `POST /api/v1/verify/autonomous` | Demo mode: extract fields, look up TTB in cache, compare to cached approved metadata |
| Reviewer sample pack | Regression tests using known approved artwork + application values |

Approved-label cache and autonomous verify exist to **prove the OCR + matcher pipeline** against ground-truth COLA records. They do not gate whether an unapproved label can be verified.

## Future live COLA integration

A production COLA connection could:

1. Prefill `ExpectedLabelFields` from an application record (server-side, not client-only JSON)
2. Store verification audit records linked to COLA application ID
3. Support batch verification against a single application for multi-panel artwork

## Data mapping (when prefill is used)

| COLA field | Verification field |
|------------|-------------------|
| Brand name | `brandName` |
| Alcohol content | `abvPercent` |
| Health warning text | `ttbWarningText` |
| Required emphasis phrase | `boldWarningPhrase` |

Until live COLA connectivity exists, agents paste or edit expected values in the UI for every verification.
