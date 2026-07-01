# ♠ MTT Tracker

A self-hostable poker **multi-table tournament (MTT)** tracker. Record your
tournament results and watch your profit & loss over time. You deploy your **own
private instance** to Azure and lock it to **your** GitHub account — your data
lives in your Cosmos DB, and nobody else can sign in.

Built with **.NET 10 Blazor WebAssembly** on **Azure Static Web Apps**, a small
**Azure Functions** API, and **Azure Cosmos DB**. Runs comfortably on Azure's
**free tiers**.

## Features

- **Track every event** — date, location/event, buy-in, re-entries, cash, and
  finishing place. Re-entries are costed automatically. Adding a new entry
  pre-fills the date from your last tournament and suggests recently-used
  locations; deleting one asks for confirmation first.
- **Multi-currency with automatic historical FX.** Enter a buy-in or cash in any
  supported currency and the app fetches the exchange rate **for that
  tournament's date** (free [Frankfurter](https://frankfurter.dev) ECB data — no
  API key) and converts it to your home currency. The rate is *frozen* onto each
  entry, so your historical stats never drift when rates move later.
- **Dashboard** — net profit, ROI, in-the-money %, average buy-in, best cash
  streak, worst no-cash streak, an interactive cumulative P&L chart (hover for
  the date, running total, and that tournament's result), and a by-year
  breakdown, all expressed in your home currency.
- **Private by design** — GitHub login, locked to a single allow-listed account;
  the API fails closed if the allowlist is unset.

> The home/reporting currency defaults to **GBP (£)**. Changing it is a one-line
> edit — see [Customizing](#customizing).

## Architecture

```
Browser ──► Blazor WASM (static files on Azure Static Web Apps)
                │
                ├─ /.auth/*   ── SWA built-in GitHub login (EasyAuth)
                │
                └─ /api/*     ── Azure Functions (MttTracker.Api)
                                     │
                                     └─► Azure Cosmos DB (container: entries, pk /userId)
```

| Project   | What it is | Notes |
|-----------|------------|-------|
| `Client/` | Blazor WebAssembly UI | Served as static files; calls `/api/*`. Fetches FX rates directly from Frankfurter. |
| `Api/`    | Azure Functions (.NET isolated) | CRUD over Cosmos; enforces the single-user allowlist. |
| `Shared/` | Class library | `TournamentEntry`, `StatsCalculator`, `Currencies`, `Format` — shared by Client and Api. |

### Why an API at all?

A WebAssembly client runs entirely in the browser, so it can't safely hold a
Cosmos DB key. The Functions API is the only component that touches Cosmos and is
where the "only my account" rule is enforced — even if another GitHub user signed
in, the API rejects them.

## Privacy & the single-user lock

Static Web Apps handles GitHub login (`/.auth/login/github`). Two layers keep
your instance private:

1. **`staticwebapp.config.json`** requires the `authenticated` role for every
   route and blocks all non-GitHub providers, so anonymous visitors are
   redirected to GitHub login.
2. **The API** independently re-checks the authenticated principal against an
   allowlist (`AllowedUserDetails` = your GitHub username, and/or `AllowedUserId`).
   This is the real security boundary — set it, or the API fails closed and
   rejects everyone.

## Data model

Each `TournamentEntry` is one event, recorded in its own `Currency`. A *re-entry*
means you busted and bought back in, so each re-entry costs another buy-in:

```
Entries        = 1 + ReEntries
TotalBuyin     = Buyin * Entries              (in the entry's currency)
Profit         = Cash  - TotalBuyin           (in the entry's currency)
ExchangeRate   = home-currency value of 1 unit of Currency, on the event date
TotalBuyinGbp  = TotalBuyin * ExchangeRate    (home currency; used for all stats)
ROI            = NetProfit / TotalBuyin
```

Stored as Cosmos documents in container `entries`, partitioned by `/userId`.

---

## Deploy your own instance

### Prerequisites

- An **Azure subscription** (the free tiers are enough for a single user).
- A **GitHub account** — used as the login provider.
- The **[Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)** (`az`).
- For local development or machine-based deploys: the **.NET 10 SDK** (it also
  builds and runs the `net8.0`-targeted `Api`/`Shared` projects — see the
  runtime note in step 3), the
  **[Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)**,
  the **[Static Web Apps CLI](https://azure.github.io/static-web-apps-cli/)**
  (`npm i -g @azure/static-web-apps-cli`), and the
  **[Cosmos DB emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator)**
  (or a real Cosmos account).

> The commands below are written in **bash**. On **Windows PowerShell**, use
> `$VAR = "value"` instead of `VAR=value` and a backtick `` ` `` for line
> continuation instead of `\` — or just run them in **Git Bash** or the
> **Azure Cloud Shell** (Bash), where they work verbatim.

### 1. Get the code

Fork this repository to your own GitHub account (so the deploy workflow can run
from your fork), then clone your fork:

```bash
git clone https://github.com/<you>/MttTracker.git
cd MttTracker
```

### 2. Create the Azure resources

```bash
az login
RG=mtt-rg
LOC=uksouth                 # any region near you (e.g. eastus, westeurope)
COSMOS=mtt-cosmos-<unique>  # globally unique, lowercase
SWA=mtt-tracker-<unique>    # globally unique, lowercase

az group create -n $RG -l $LOC

# Cosmos DB (NoSQL). Free tier covers a single-user app; drop --enable-free-tier
# if this subscription already uses its one free-tier account.
az cosmosdb create -g $RG -n $COSMOS --enable-free-tier true \
  --default-consistency-level Session
az cosmosdb sql database create -g $RG -a $COSMOS -n MttTracker
az cosmosdb sql container create -g $RG -a $COSMOS -d MttTracker \
  -n entries --partition-key-path /userId --throughput 400
```

> **First time on a subscription?** If you see
> `MissingSubscriptionRegistration`, register the resource providers once (takes
> a minute), then re-run the failed command:
> ```bash
> az provider register --namespace Microsoft.DocumentDB   # Cosmos DB
> az provider register --namespace Microsoft.Web          # Static Web Apps + Functions
> ```

### 3. Create the Static Web App and deploy

Pick **one** option.

**Option A — GitHub Actions (recommended).** In the
[Azure Portal](https://portal.azure.com) → *Create Static Web App* → choose
**GitHub** as the source and select your fork → build preset **Blazor**, app
location `Client`, api location `Api`, output location `wwwroot`. Azure adds the
deployment-token secret to your repo and commits a workflow named after the app
(`.github/workflows/azure-static-web-apps-<name>.yml`), wired to your repo's
default branch. From then on, **every push to that branch builds the Client + Api
and deploys** — Azure's build pipeline handles the full .NET publish for you. Let
Azure own this file; don't add your own alongside it or you'll get double deploys.

**Option B — Deploy from your machine (no fork/Actions needed).** Create a
standalone app, then publish and push the build with the SWA CLI:

```bash
az staticwebapp create -n $SWA -g $RG -l westeurope     # SWA region, e.g. westeurope / eastus2
TOKEN=$(az staticwebapp secrets list -n $SWA -g $RG --query "properties.apiKey" -o tsv)

dotnet publish Client -c Release
swa deploy Client/bin/Release/net10.0/publish/wwwroot \
  --api-location Api \
  --deployment-token "$TOKEN" \
  --env production
```

(`swa deploy` uploads what you give it — point it at the **published** `wwwroot`,
not the `Client` source folder.)

### 4. Configure application settings

In the Static Web App → *Configuration* (or `az staticwebapp appsettings set`),
add:

| Name | Value |
|------|-------|
| `CosmosConnectionString` | the Cosmos account's primary connection string (`az cosmosdb keys list -g $RG -n $COSMOS --type connection-strings`) |
| `CosmosDatabase` | `MttTracker` |
| `CosmosContainer` | `entries` |
| `AllowedUserDetails` | **your GitHub username** (the allowlist) |

### 5. Sign in

Browse to the app's URL, sign in with GitHub, and you're in:

```bash
az staticwebapp show -n $SWA -g $RG --query "defaultHostname" -o tsv
```

> **Runtime note:** `Api/` and `Shared/` target `net8.0` (not `net10.0` like
> `Client/`), since SWA-managed Functions don't yet support a `net10.0` isolated
> worker. If a future SWA release adds `net10.0` support, all three projects can
> be retargeted together — no code changes needed — or use the Standard plan's
> "bring your own Functions" option today if you need it sooner.

---

## Customizing

- **Home / reporting currency.** Defaults to GBP. Edit `Shared/Currencies.cs`:
  ```csharp
  public const string Home = "GBP";   // e.g. "USD", "EUR" — any code in All
  ```
  Set it to any currency in the `All` list. To add a currency, append it to
  `All` (it must be one the [Frankfurter/ECB](https://frankfurter.dev) feed
  supports). All existing stats recompute in the new home currency on next load,
  using each entry's frozen rate.

## Running locally

1. Configure `Api/local.settings.json` — set `CosmosConnectionString` (the
   emulator's well-known connection string is pre-filled) and `AllowedUserDetails`
   to the mock username you'll log in as. (This file is git-ignored.)

2. Run all three pieces together with the SWA CLI, which emulates auth + routing
   and proxies the API. Start the API and the client first, then point the SWA CLI
   at the **already-running** dev servers (the `*-devserver-url` flags take URLs;
   `--api-location`/positional paths expect folders):

   ```bash
   # terminal 1 – API (serves http://localhost:7071)
   cd Api && func start

   # terminal 2 – WASM client (pinned to http://localhost:5080 in launchSettings)
   cd Client && dotnet run

   # terminal 3 – SWA emulator / front door (serves http://localhost:4280)
   swa start --app-devserver-url http://localhost:5080 --api-devserver-url http://localhost:7071
   ```

   Browse to **http://localhost:4280** (the SWA front door — not 5080). Use its
   mock login page and set the username to match `AllowedUserDetails`.

   On Windows, `dev.ps1` / `dev.cmd` in the repo root start (or restart) all three
   for you.

## Cost

A single-user instance fits inside Azure's free allowances: the **Static Web Apps
Free** plan (includes managed Functions) and the **Cosmos DB free tier**
(1000 RU/s + 25 GB). The `--throughput 400` above stays within that. You only pay
if you outgrow the free tiers.

## Secrets & safety

This repo is safe to host publicly — no keys live in it, by design:

- **`Api/local.settings.json` is git-ignored.** Your dev connection string never
  gets committed. The value checked into docs is the *public* emulator key only.
- **The deployment token** lives in your repo's **GitHub Secrets**
  (`AZURE_STATIC_WEB_APPS_API_TOKEN`), referenced by the workflow — never in code.
- **The Cosmos connection string** lives in **Azure SWA → Configuration**, set at
  deploy time — never in the repo.
- On your fork, enable **GitHub secret-scanning push protection**
  (*Settings → Code security → Push protection*) as a safety net.
- Note: the browser fetches FX rates directly from `api.frankfurter.dev`. No keys
  or personal data are sent — only a currency code and a date.

> **Hardening (optional):** prefer a managed identity over a Cosmos connection
> string, and store secrets in Key Vault.

## License

MIT — see [`LICENSE.txt`](LICENSE.txt). Provided as-is, without warranty. If you
publish a fork, fill in the copyright holder placeholder in `LICENSE.txt` (or
swap in your own license).
