# Hosting & cost

_Last updated: 2026-10-12_

## Where it runs

| Item | Value |
|---|---|
| App | Azure App Service `label-verify-trackc` (https://label-verify-trackc.azurewebsites.net) |
| Resource group | `label-verify-rg` (westus2) |
| App Service plan | `label-verify-plan-westus2` — **Basic B1** (Linux) |
| Container image | `labelverify52075.azurecr.io/label-verify-trackc:latest` |

## 2026-10-12 scale-down

- The plan `label-verify-plan-westus2` was scaled down from **Premium P2v3** (about $223/mo, ~96% of the ~$233/mo subscription bill) to **Basic B1** (about $13/mo).
- Reason: the app was an idle test deployment, last changed 2026-09-05. Premium plans bill every hour whether or not the app is used.
- The duplicate container registry `labelverify79931` was deleted. The app pulls from `labelverify52075` only.
- Expected total cost is now **about $18/mo** (B1 plan + the remaining registry).

## B1 limits

- No autoscale.
- No deployment slots.
- Fewer cores and less memory than P2v3, so multi-page OCR runs slower.

**Scale back up before any real load** (reviews, demos, benchmarks).

## Scale back up

```bash
# Back to Premium P2v3 (the tier used for the 2026-06 benchmarks)
az appservice plan update \
  --resource-group label-verify-rg \
  --name label-verify-plan-westus2 \
  --sku P2V3

# Confirm the tier and that the app is running
az appservice plan show -g label-verify-rg -n label-verify-plan-westus2 --query "sku" -o table
az webapp show -g label-verify-rg -n label-verify-trackc --query "state" -o tsv

# Scale back down to B1 when finished
az appservice plan update -g label-verify-rg -n label-verify-plan-westus2 --sku B1
```

## Observability

HTTP logging and Application Insights are **not enabled**, so there is no request or visitor history. To turn on HTTP logs:

```bash
az webapp log config -g label-verify-rg -n label-verify-trackc \
  --web-server-logging filesystem
```
