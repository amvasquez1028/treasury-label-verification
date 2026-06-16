# Approach — Label Verification

## Problem

Agents compare **label image files** (flat artwork or bottle photos) to **treasury application metadata** the agent enters (brand, class, ABV, warnings, etc.). TTB ID and prior COLA approval are **not** required for matching.

## Intelligent verification pipeline

1. **Photo-aware preprocessing** — classify flat artwork vs bottle photo; upscale if shortest side < 800px; CLAHE for photos
2. **Multi-pass OCR ensemble** — global, footer band (warning), numeric band; merged corpus
3. **Readability classifier** — blur, contrast, OCR yield → `unreadable` before false pass/fail
4. **Field-specialized matchers** — fuzzy brand/class/address; exact warning; bold phrase heuristic
5. **Confidence triage** — pass ≥90% min confidence; review band 0.65–0.75 for near-miss; partial fields on unreadable

All processing runs **in-container** with no outbound ML API calls (firewall-safe).

## Trade-offs

| Decision | Rationale | Limitation |
|----------|-----------|------------|
| Server Tesseract vs cloud Vision | Firewall-safe, predictable latency | Hard glare may still fail |
| Multi-pass ensemble | Better warning/ABV on curved bottles | CPU-bound; P2v3 on Azure for ODP stacks |
| Review band | Matches agent judgment (Dave) | Near-threshold cases need human review |
| Static COLA cache | Demo prefill + autonomous regression tests | Not used for production verify path |
| Synthetic perf gate vs golden photos | Deterministic CI | Real photos tested separately |

## Out of scope

Live COLA integration, in-app camera capture, HEIC native support, perfect OCR on all submissions.
