# TTB references

Official Alcohol and Tobacco Tax and Trade Bureau (TTB) resources used by this prototype for label verification rules, COLA research, and reviewer guidance.

## COLA and label approval

| Resource | URL | Use |
|----------|-----|-----|
| Certificates of Label Approval (COLAs) | https://www.ttb.gov/regulated-commodities/labeling/colas | COLA program overview and regulatory context |
| Labeling resources | https://www.ttb.gov/regulated-commodities/labeling/labeling-resources | TTB labeling guides, forms, and reference material |
| Public COLA Registry | https://ttbonline.gov/colasonline/publicSearchColasBasic.do | Search approved labels; printable certificate views (public record) |
| How to search the COLA Registry | https://www.ttb.gov/news/using-cola-registry-search-certificates | Step-by-step search guidance for reviewers |

## Beverage alcohol labeling (by product type)

| Resource | URL |
|----------|-----|
| Beverage alcohol overview | https://www.ttb.gov/regulated-commodities/beverage-alcohol |
| Distilled spirits labeling | https://www.ttb.gov/regulated-commodities/beverage-alcohol/distilled-spirits/labeling |
| Wine labeling | https://www.ttb.gov/regulated-commodities/beverage-alcohol/wine/labeling |
| Malt beverage labeling | https://www.ttb.gov/regulated-commodities/beverage-alcohol/beer/labeling |
| Distilled spirits health warning | https://www.ttb.gov/regulated-commodities/beverage-alcohol/distilled-spirits/ds-labeling-home/ds-health-warning |

## Test fixtures

Approved-label examples used in automated tests are documented on the [Guidelines](/guidelines/) page and stored under `testdata/colas/`. Fixture metadata is derived from public COLA registry fields; representative ABV and net-contents values are documented in each `{ttbId}.meta.json` for OCR testing only.
