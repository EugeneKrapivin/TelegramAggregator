# Deployment Guide

Reference for deploying TelegramAggregator to Azure using Azure Container Apps and Azure AI Foundry.

---

## Table of Contents

1. [Why Azure Container Apps](#1-why-azure-container-apps)
2. [Prerequisites](#2-prerequisites)
3. [One-time Setup: Azure AI Foundry](#3-one-time-setup-azure-ai-foundry)
4. [Model Selection for Summarization](#4-model-selection-for-summarization)
5. [First Deployment with azd](#5-first-deployment-with-azd)
6. [Secrets and Parameters](#6-secrets-and-parameters)
7. [Observability: OTLP in Production](#7-observability-otlp-in-production)
8. [Wiring Everything into AppHost.cs](#8-wiring-everything-into-apphostcs)
9. [Redeploying After Changes](#9-redeploying-after-changes)
10. [Tearing Down](#10-tearing-down)

---

## 1. Why Azure Container Apps

This project runs three persistent processes (api, worker, migrations) and a containerised Postgres database. ACA is the right target for this shape of application.

| | Azure Container Apps | Azure App Service |
|---|---|---|
| Aspire support | **First-class** — `azd init` defaults to ACA | Added recently, still maturing |
| Background worker | **Native** — non-HTTP containers are a core use-case | Designed for HTTP; worker wastes a plan slot |
| Migration service (run-and-exit) | **Native Jobs** — supported by Aspire's `WaitForCompletion` | No equivalent; requires workarounds |
| Internal networking | Services talk over Aspire-managed internal VNET | **Broken for Aspire** — all services must expose external HTTPS endpoints for service-to-service calls |
| Scale to zero | Yes — API idles free when unused | Plan-dependent |
| Postgres | ACA provisions Azure Database for PostgreSQL Flexible Server automatically via `azd` | Manual setup required |

The internal networking limitation is the dealbreaker for App Service: the Aspire docs explicitly state that App Service does not manage internal traffic between apps. The worker and migration service would need externally exposed endpoints, which makes no sense.

---

## 2. Prerequisites

Install once on your machine:

```bash
# Azure Developer CLI
winget install microsoft.azd

# Azure CLI (for key retrieval and diagnostics)
winget install Microsoft.AzureCLI

# Docker Desktop (required for local dev and for azd to build container images)
# https://www.docker.com/products/docker-desktop
```

Verify:

```bash
azd version    # should be 1.x or later
az version     # should be 2.x or later
docker info    # should show server running
```

---

## 3. One-time Setup: Azure AI Foundry

Azure AI Foundry (formerly Azure OpenAI Studio) is where you provision the OpenAI resource and create a model deployment. You do this once per environment.

### 3a. Create the Resource

**Portal (recommended for first-time setup):**

1. Go to [https://ai.azure.com](https://ai.azure.com)
2. Sign in with your Azure account
3. Click **+ Create project** → select **Azure AI Foundry** hub type
4. Fill in:
   - **Subscription**: your Azure subscription
   - **Resource group**: use the same one `azd` will create, or a dedicated one
   - **Region**: `swedencentral` or `eastus2` — both have the full model catalogue including `gpt-4.1-mini` and `gpt-4o-mini`
   - **Name**: e.g. `telegram-aggregator-ai`
5. Click **Create**

> **Note on region choice:** Pick the same region you will use for your ACA deployment (`azd up` asks you). Co-locating the AI resource and the Container Apps environment avoids cross-region egress costs and reduces latency.

**CLI alternative:**

```bash
az cognitiveservices account create \
  --name telegram-aggregator-ai \
  --resource-group <your-rg> \
  --kind OpenAI \
  --sku S0 \
  --location swedencentral
```

### 3b. Create a Model Deployment

Once the resource exists, deploy a model:

**Portal:**

1. Open the resource in [https://ai.azure.com](https://ai.azure.com)
2. Go to **Deployments** → **+ Deploy model**
3. Select your chosen model (see [Section 4](#4-model-selection-for-summarization))
4. Give the deployment a name — use something simple like `summarizer` (this name goes into your app config)
5. Set **Tokens per minute** quota — 40K TPM is sufficient for summarising batches of Telegram posts every 10 minutes
6. Click **Deploy**

**CLI alternative:**

```bash
az cognitiveservices account deployment create \
  --name telegram-aggregator-ai \
  --resource-group <your-rg> \
  --deployment-name summarizer \
  --model-name gpt-4.1-mini \
  --model-version 2025-04-14 \
  --model-format OpenAI \
  --sku-capacity 40 \
  --sku-name Standard
```

### 3c. Retrieve the Endpoint and Key

**Portal:**

1. Open the resource in the Azure Portal (not AI Foundry portal)
2. Go to **Resource Management** → **Keys and Endpoint**
3. Copy **Endpoint** (looks like `https://telegram-aggregator-ai.openai.azure.com/`) and **KEY 1**

**CLI:**

```bash
# Endpoint
az cognitiveservices account show \
  --name telegram-aggregator-ai \
  --resource-group <your-rg> \
  --query properties.endpoint -o tsv

# Key
az cognitiveservices account keys list \
  --name telegram-aggregator-ai \
  --resource-group <your-rg> \
  --query key1 -o tsv
```

You will need three values when setting up secrets:
- **Endpoint** — e.g. `https://telegram-aggregator-ai.openai.azure.com/`
- **API Key** — a 32-character hex string
- **Deployment name** — e.g. `summarizer`

> **Security note:** The docs recommend Managed Identity over API keys for production. With ACA + Aspire, Managed Identity requires additional Bicep customisation. Start with API keys; the secret is stored in Azure Container Apps secrets (not in code or source control), which is acceptable for most workloads. Rotate the key every 90 days using Azure Portal → Keys and Endpoint → Regenerate.

---

## 4. Model Selection for Summarization

The summarization task is: batch up ~10–50 Telegram posts (plain text), produce a readable digest, post to a Telegram channel. This runs every 10 minutes.

This is **not** a complex reasoning task. Avoid o-series (reasoning) models — they are slower, more expensive, and built for multi-step problem solving, not text summarization.

### Recommendation: `gpt-4.1-mini`

| | gpt-4.1-mini | gpt-4o-mini | gpt-4.1-nano |
|---|---|---|---|
| Released | April 2025 | July 2024 | April 2025 |
| Context window | 1M tokens | 128K tokens | 1M tokens |
| Input cost (per 1M tokens) | ~$0.40 | ~$0.15 | ~$0.10 |
| Output cost (per 1M tokens) | ~$1.60 | ~$0.60 | ~$0.40 |
| Quality for summarization | Excellent | Very good | Good |
| Availability | `swedencentral`, `eastus`, `eastus2`, most regions | Universal | `swedencentral`, `eastus`, `eastus2` |

**Use `gpt-4.1-mini`** as the default. It has a 1M token context window (useful if you ever need to include more posts), strong instruction-following for formatting the summary, and is cheap enough that at 10-minute intervals the monthly cost is negligible (a few dollars).

**Fallback: `gpt-4o-mini`** if `gpt-4.1-mini` is unavailable in your chosen region. Essentially the same price point, very widely available, very capable for summarization.

**Avoid for this task:**
- `gpt-4.1`, `gpt-4o` — 10–25× more expensive, no meaningful quality improvement for summarization
- `o4-mini`, `o3-mini` — reasoning models, 3–5× more expensive, slower (reasoning tokens add latency)
- `gpt-35-turbo` — being deprecated, avoid new deployments

### Cost Estimate

Assuming 50 posts × 200 tokens each = ~10K input tokens per run, 500 token output, 6 runs/hour, 24h/day, 30 days:

```
Input:  6 × 24 × 30 × 10,000  = 43.2M tokens  × $0.40/1M = ~$17/month
Output: 6 × 24 × 30 × 500     =  2.16M tokens  × $1.60/1M = ~$3.50/month
Total: ~$20/month for gpt-4.1-mini
```

With `gpt-4o-mini` that drops to ~$7/month. Both are negligible.

---

## 5. First Deployment with azd

### 5a. Install the azd Aspire extension (if needed)

```bash
azd extension install aspire
```

### 5b. Initialise

Run from the repo root:

```bash
azd init
```

When prompted:
1. Select **Scan current directory**
2. Select **Confirm and continue initialising my app** — azd will detect `TelegramAggregator.AppHost`
3. Enter an environment name, e.g. `prod` or `staging`

This creates:
- `azure.yaml` — points azd at the AppHost project
- `.azure/prod/.env` — stores environment-specific variable values (gitignored)
- `.azure/config.json` — tracks the active environment

### 5c. Set secrets

All Aspire `AddParameter(..., secret: true)` values must be provided before deployment. Set them once per environment; azd stores them encrypted in `.azure/<env>/.env`.

```bash
# Telegram credentials
azd env set telegram-bot-token    "<your-bot-token>"
azd env set telegram-api-id       "<your-api-id>"
azd env set telegram-api-hash     "<your-api-hash>"
azd env set telegram-user-phone-number "+<country-code><number>"

# Azure OpenAI
azd env set azure-openai-endpoint "https://telegram-aggregator-ai.openai.azure.com/"
azd env set azure-openai-api-key  "<your-api-key>"
```

> **Tip:** `azd env set` stores values in `.azure/<env>/.env` which is already gitignored. Never commit this file.

### 5d. Deploy

```bash
azd auth login   # opens browser for Azure login
azd up           # provision infrastructure + build containers + deploy
```

`azd up` does the following in sequence:
1. Runs the AppHost to produce a manifest of all resources
2. Generates Bicep from the manifest
3. Provisions Azure resources via ARM:
   - Container Apps Environment
   - Azure Container Registry
   - Azure Database for PostgreSQL Flexible Server (from `AddPostgres`)
   - One Container App per project (api, worker, migrations)
4. Builds container images via `dotnet publish`
5. Pushes images to ACR
6. Deploys images to the Container Apps

When complete, azd prints the URLs for each service.

### 5e. Verify startup order in Azure Portal

In the Azure Portal → Container Apps → your environment:
1. `postgres` → Running
2. `migrations` → Finished (exit code 0) — if it shows Failed, check logs for migration errors
3. `api` → Running
4. `telegramaggregator` → Running

---

## 6. Secrets and Parameters

### How azd maps Aspire parameters to Azure

Aspire parameters (`builder.AddParameter("name", secret: true)`) become Container Apps secrets. `azd` reads the values from `.azure/<env>/.env` and injects them at provision time.

### Rotating a secret

```bash
# Update the value locally
azd env set azure-openai-api-key "<new-key>"

# Re-provision to apply (only updates config, does not rebuild containers)
azd provision

# Or redeploy everything
azd up
```

### Viewing current environment values

```bash
azd env get-values
```

### Multiple environments

```bash
# Create a staging environment
azd env new staging
azd env select staging
azd env set telegram-bot-token "<staging-bot-token>"
# ... set all other secrets ...
azd up

# Switch back to prod
azd env select prod
```

---

## 7. Observability: OTLP in Production

In local dev, Aspire auto-wires `OTEL_EXPORTER_OTLP_ENDPOINT` on every service to point at the Aspire Dashboard. In production that env var is absent and telemetry goes nowhere unless you configure it.

### Option A: External OTLP backend (Grafana Cloud, Datadog, Honeycomb, etc.)

This works with any OTLP-compatible backend. The three standard environment variables control it:

| Variable | What it does |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Your backend's OTLP ingest URL |
| `OTEL_EXPORTER_OTLP_HEADERS` | Auth header(s) in `key=value,key=value` format |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` or `http/protobuf` (check your backend's docs) |

Example for Grafana Cloud:
```
OTEL_EXPORTER_OTLP_ENDPOINT = https://otlp-gateway-prod-eu-west-0.grafana.net/otlp
OTEL_EXPORTER_OTLP_HEADERS  = Authorization=Basic <base64(instance-id:api-key)>
OTEL_EXPORTER_OTLP_PROTOCOL = http/protobuf
```

Wire them in `AppHost.cs` as Aspire parameters (see [Section 8](#8-wiring-everything-into-apphostcs)), then set values with azd:

```bash
azd env set otlp-endpoint "https://otlp-gateway-prod-eu-west-0.grafana.net/otlp"
azd env set otlp-headers  "Authorization=Basic <base64token>"
azd env set otlp-protocol "http/protobuf"
```

### Option B: Azure Container Apps managed OpenTelemetry agent

ACA has a built-in OTel agent that can forward to Azure Monitor, Datadog, or a custom OTLP endpoint — no code changes required. You configure it in the Container Apps Environment settings (portal or Bicep).

When the ACA managed agent is enabled, it auto-injects `OTEL_EXPORTER_OTLP_ENDPOINT` into your containers pointing at a local sidecar, which then forwards to your configured destinations.

**Portal setup:**
1. Azure Portal → Container Apps Environments → your environment
2. **Monitoring** → **OpenTelemetry**
3. Enable agent → configure destinations (Azure Monitor or custom OTLP endpoint)

This is the simpler option if you're already using Azure Monitor / Application Insights.

### Option C: Application Insights (Azure Monitor)

Requires adding the `Azure.Monitor.OpenTelemetry.AspNetCore` package and configuring the Azure Monitor exporter in `ServiceDefaults`. The AppHost then provisions an Application Insights resource automatically:

```csharp
// In AppHost.cs
builder.AddAzureApplicationInsights("insights");
```

This is the most Azure-native option but couples you to Azure Monitor pricing.

---

## 8. Wiring Everything into AppHost.cs

Complete `AppHost.cs` with all secrets, OTLP, and AI Foundry wired:

```csharp
using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// ── Telegram secrets ──────────────────────────────────────────────────────────
var telegramBotToken = builder.AddParameter("telegram-bot-token", secret: true)
    .WithDescription("Telegram bot token obtained from @BotFather");
var telegramApiId = builder.AddParameter("telegram-api-id", secret: true)
    .WithDescription("Telegram API ID from https://my.telegram.org/apps");
var telegramApiHash = builder.AddParameter("telegram-api-hash", secret: true)
    .WithDescription("Telegram API hash for user client authentication");
var telegramUserPhoneNumber = builder.AddParameter("telegram-user-phone-number", secret: true)
    .WithDescription("Phone number for Telegram user client login");

// ── Azure OpenAI (AI Foundry) ─────────────────────────────────────────────────
var azureOpenAiEndpoint = builder.AddParameter("azure-openai-endpoint", secret: true)
    .WithDescription("Azure OpenAI endpoint, e.g. https://<resource>.openai.azure.com/");
var azureOpenAiApiKey = builder.AddParameter("azure-openai-api-key", secret: true)
    .WithDescription("API key for Azure OpenAI resource");
// Deployment name is not secret — it's just a label like "summarizer"
var azureOpenAiDeployment = builder.AddParameter("azure-openai-deployment")
    .WithDescription("Model deployment name in Azure AI Foundry, e.g. summarizer");

// ── OTLP observability (optional — omit if not using an external backend) ─────
var otlpEndpoint = builder.AddParameter("otlp-endpoint")
    .WithDescription("OTLP ingest endpoint for metrics/traces/logs");
var otlpHeaders = builder.AddParameter("otlp-headers", secret: true)
    .WithDescription("OTLP auth headers, e.g. Authorization=Basic <token>");
var otlpProtocol = builder.AddParameter("otlp-protocol")
    .WithDescription("grpc or http/protobuf");

// ── PostgreSQL ────────────────────────────────────────────────────────────────
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("postgres");

// ── Migration service ─────────────────────────────────────────────────────────
var migrations = builder.AddProject<Projects.TelegramAggregator_MigrationService>("migrations")
    .WithReference(postgres)
    .WaitFor(postgres);

// ── API ───────────────────────────────────────────────────────────────────────
var api = builder.AddProject<Projects.TelegramAggregator_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrations)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS",  otlpHeaders)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

// ── Worker ────────────────────────────────────────────────────────────────────
builder.AddProject<Projects.TelegramAggregator>("telegramaggregator")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrations)
    .WithEnvironment("Telegram__BotToken", telegramBotToken)
    .WithEnvironment("Telegram__ApiId", telegramApiId)
    .WithEnvironment("Telegram__ApiHash", telegramApiHash)
    .WithEnvironment("Telegram__UserPhoneNumber", telegramUserPhoneNumber)
    .WithEnvironment("SemanticKernel__AzureOpenAI__Endpoint",       azureOpenAiEndpoint)
    .WithEnvironment("SemanticKernel__AzureOpenAI__ApiKey",         azureOpenAiApiKey)
    .WithEnvironment("SemanticKernel__AzureOpenAI__DeploymentName", azureOpenAiDeployment)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS",  otlpHeaders)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

// ── Scalar API docs ───────────────────────────────────────────────────────────
var scalar = builder.AddScalarApiReference();
scalar.WithApiReference(api, options =>
{
    options
       .AddDocument("v1", "Telegram Aggregator API")
       .WithOpenApiRoutePattern("/openapi/{documentName}.json")
       .WithTheme(ScalarTheme.Mars);
});

builder.Build().Run();
```

Then set all values:

```bash
# Already set from Section 5c, add the new ones:
azd env set azure-openai-deployment "summarizer"
azd env set otlp-endpoint           "https://your-backend/otlp"
azd env set otlp-headers            "Authorization=Basic <token>"
azd env set otlp-protocol           "http/protobuf"
```

> **Note:** If you are not using an external OTLP backend yet, you can omit the `otlp-*` parameters from `AppHost.cs` entirely. The services will simply not export telemetry in production until you add them.

### Semantic Kernel configuration in the worker

The worker's `Program.cs` (or wherever Semantic Kernel is registered) should read the deployment name from config:

```csharp
builder.Services.AddAzureOpenAIChatCompletion(
    deploymentName: builder.Configuration["SemanticKernel:AzureOpenAI:DeploymentName"]!,
    endpoint:       builder.Configuration["SemanticKernel:AzureOpenAI:Endpoint"]!,
    apiKey:         builder.Configuration["SemanticKernel:AzureOpenAI:ApiKey"]!);
```

The double-underscore env var convention (`SemanticKernel__AzureOpenAI__DeploymentName`) maps to the colon-separated config key (`SemanticKernel:AzureOpenAI:DeploymentName`) automatically in .NET.

---

## 9. Redeploying After Changes

| Scenario | Command |
|---|---|
| Code change only | `azd deploy` |
| Infrastructure change (new resource, new parameter) | `azd provision` then `azd deploy`, or just `azd up` |
| Secret value changed | `azd env set <name> <value>` then `azd provision` |
| Full rebuild and redeploy | `azd up` |

`azd deploy` is much faster than `azd up` — it skips infrastructure provisioning and just rebuilds + redeploys containers.

### CI/CD with GitHub Actions

```bash
azd pipeline config
```

This command creates a GitHub Actions workflow (`.github/workflows/azure-dev.yml`) and registers the required Azure credentials as GitHub secrets. After that, every push to `main` triggers `azd up` automatically.

---

## 10. Tearing Down

To delete all Azure resources for an environment:

```bash
azd down
```

This deletes the resource group and everything in it (Container Apps, ACR, Postgres, managed identities). The local `.azure/<env>/.env` file with your secrets is not deleted — you can redeploy later with `azd up`.

To tear down and remove the local azd environment config too:

```bash
azd down --purge
```

---

## Quick Reference

```bash
# First-time deployment
azd init
azd env set <param> <value>   # repeat for all secrets
azd auth login
azd up

# Subsequent deployments (code only)
azd deploy

# Redeploy after infrastructure change
azd up

# View logs
az containerapp logs show \
  --name telegramaggregator \
  --resource-group <rg> \
  --follow

# Rotate OpenAI key
azd env set azure-openai-api-key "<new-key>"
azd provision

# Tear down
azd down
```
