# DevOps Releases & Pipeline Key Vault Tracker — Technical Specification

## 1. Executive Summary & Purpose

This document outlines the architectural design and implementation plan for two complementary capabilities in **KeyVaultComparer**:
1. **Release Branches & Rollback Tracker**: A cross-service matrix tracking which branch, commit, and Docker image version is deployed across environments (specifically Production), highlighting previous deployment targets for fast rollbacks.
2. **Pipeline Variable & Key Vault Pre-Flight Validator**: A tool to inspect YAML pipelines (including nested external templates), extract required variables and Helm secret overrides (`$(SECRET_NAME)`), map them to environment Variable Groups and Key Vaults, and flag missing secrets in red before deployment.

---

## 2. Feature 1: Release Branches & Rollback Tracker

### 2.1 User Need & Concept
Teams running microservices need a single operational view answering:
- What release branch is currently live in Production for each service?
- What Docker image tag and Git commit SHA does it represent?
- When was it deployed and by whom?
- If an incident occurs, **what was the previous release** that worked, and how can it be rolled back immediately?

### 2.2 Release Matrix Data Model
The matrix displays services/repositories as rows and operational deployment metadata as columns:

| Service / Repo | Current Prod Branch | Docker Image Tag | Commit SHA / PR | Deployed At / Author | Previous Branch (Rollback) | Previous Image (Rollback) | Rollback Action |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `order-service` | `release/v2.4.1` | `order-service:2.4.1-b108` | `a1c4f92` ([#342](https://github.com/org/repo/pull/342)) | 2026-10-10 14:20 (devops-bot) | `release/v2.4.0` | `order-service:2.4.0-b99` | [Rollback in ADO](https://dev.azure.com/org/proj/_build) |
| `auth-service` | `release/v1.9.0` | `auth-service:1.9.0-b45` | `e8b2110` ([#118](https://github.com/org/repo/pull/118)) | 2026-10-09 18:05 (john.doe) | `release/v1.8.4` | `auth-service:1.8.4-b42` | [Rollback in ADO](https://dev.azure.com/org/proj/_build) |

### 2.3 Additional Recommended Data Points
- **Pull Request Links**: Direct hyperlinks to PRs merged into the release to quickly view included changelogs and bug fixes.
- **Pending Approvals Status**: Highlighting if a release stage is waiting for manual gate approval.
- **Health Check & Metrics URL**: Direct link to the service's `/health` or Datadog/Azure Monitor dashboard.
- **Container Registry Links**: Deep link to Azure Container Registry (ACR) for the artifact tag.

### 2.4 Azure DevOps REST APIs Required
1. **Repository Links**:
   - `GET https://dev.azure.com/{org}/{project}/_apis/build/definitions?api-version=7.1`
   - Returns pipeline definition, repository type (GitHub / Azure Repos), and repository URL.
2. **Deployments & Runs (Classic Releases & YAML Multi-Stage)**:
   - Classic: `GET https://vsrm.dev.azure.com/{org}/{project}/_apis/release/deployments?environmentName=Production&deploymentStatus=succeeded&$top=2`
   - YAML: `GET https://dev.azure.com/{org}/{project}/_apis/pipelines/{pipelineId}/runs?api-version=7.1`
   - Stage status inspection: `GET https://dev.azure.com/{org}/{project}/_apis/build/builds/{buildId}/timeline?api-version=7.1`
3. **Artifacts & Build Details**:
   - `GET https://dev.azure.com/{org}/{project}/_apis/build/builds/{buildId}?api-version=7.1`
   - Contains `sourceBranch`, `sourceVersion` (commit SHA), `triggerInfo.pr.number`, and `requestedFor`.

---

## 3. Feature 2: Pipeline Variable & Key Vault Pre-Flight Validator

### 3.1 Problem Statement & Anatomy of Enterprise Pipelines
Deployments fail when a pipeline step or Helm chart expects a secret that does not exist in the target environment's Key Vault.

Typical enterprise setup:
1. **Pipeline entry point** (repository `azure-pipelines.yml`):
   ```yaml
   resources:
     repositories:
     - repository: azdonkotemplates
       type: github
       name: org-templates/nko-templates
       endpoint: BKCD  # Azure DevOps Service Connection

   extends:
     template: nko-spa/pipeline.yml@azdonkotemplates
     parameters:
       app_name: my-service
       helm_overrides:
         - name: DB_PASSWORD
           value: $(DbPassword)
         - name: API_KEY
           value: $(ExternalApiKey)
   ```
2. **Template repository** (`org-templates/nko-templates`):
   - Ingests parameters, links Variable Groups (`variables: - group: MyService-DEV`), and executes Helm tasks or deployment scripts.
3. **Variable Groups**:
   - Link directly to Azure Key Vaults (e.g., `MyService-DEV` -> `kv-myservice-dev`).
4. **Failure mode**:
   - If `ExternalApiKey` is added to Helm overrides but someone forgot to upload `ExternalApiKey` to `kv-myservice-prod`, the production deployment fails or crashes at runtime.

### 3.2 Template Resolution & ADO API Strategy
A key technical consideration is accessing nested YAML templates stored in external GitHub repos:
- **Direct GitHub API**: Requires GitHub Personal Access Tokens (PAT) or OAuth authentication. `az login` credentials do not grant GitHub API access. Managing tokens in local DPAPI/keychains adds friction.
- **Azure DevOps Pipeline Preview API**:
  Azure DevOps can parse and compile the full pipeline graph using its existing Service Connections:
  ```http
  POST https://dev.azure.com/{org}/{project}/_apis/pipelines/{pipelineId}/preview?api-version=7.1-preview.1
  Content-Type: application/json

  {}
  ```
  **Why Preview API is optimal:**
  1. Resolves external Git/GitHub templates (`@azdonkotemplates`) server-side using ADO's stored service connections.
  2. Expands all `parameters` and template inclusions into a single, flattened YAML AST.
  3. **Preserves runtime macro variables**: Secrets and variables remain in token format (e.g., `$(DbPassword)`), allowing regex/AST extraction.
  4. Returns all referenced `variable groups` in the YAML metadata.

### 3.3 End-to-End Resolution Workflow

```mermaid
flowchart TD
    A[User selects Pipeline in UI] --> B[Backend calls ADO Preview API]
    B --> C[ADO expands external templates via Service Connections]
    C --> D[Backend receives flattened YAML AST]
    D --> E[Extract $(VAR_NAME) patterns & Helm overrides]
    D --> F[Extract referenced Variable Groups]
    F --> G[Query Variable Groups API for Vault metadata]
    G --> H[Map Variable Groups to Azure Key Vaults per Environment]
    E & H --> I[Compare required secrets against Azure Key Vault cached secrets]
    I --> J{Secret exists in Vault?}
    J -- Yes --> K[Status: RESOLVED - Green]
    J -- No --> L[Status: MISSING - Red Alert]
```

### 3.4 Variable Group to Key Vault Mapping
The backend queries the ADO Distributed Task API:
```http
GET https://dev.azure.com/{org}/{project}/_apis/distributedtask/variablegroups?groupNames={groupName}&api-version=7.1-preview.2
```
When `type == "AzureKeyVault"`, the payload contains:
```json
{
  "id": 124,
  "name": "MyService-PROD",
  "type": "AzureKeyVault",
  "providerData": {
    "serviceEndpointId": "78a9c8f0-...",
    "vault": "kv-myservice-prod"
  },
  "variables": {
    "DbPassword": { "isSecret": true, "enabled": true }
  }
}
```
This metadata links:
- Pipeline parameter `$(DbPassword)` -> Variable Group `MyService-PROD` -> Key Vault `kv-myservice-prod`.

### 3.5 Secret Status Classifications
For each variable discovered in the pipeline:
1. **`KeyVault-Linked` (Valid)**: Secret exists in the target Key Vault and is enabled.
2. **`Static-Variable` (Valid)**: Provided directly as a literal or standard variable group variable (non-secret).
3. **`Missing-In-Vault` (Error - RED)**: Referenced in pipeline/Helm overrides and linked to a Key Vault group, but **not found** in the actual Key Vault.
4. **`Unmapped` (Warning - YELLOW)**: Used in pipeline syntax `$(VarName)`, but not declared in any linked Variable Group.

---

## 4. UI / UX Integration Approaches

### Option A: Main Key Vault Comparison Grid Injection
- **Concept**: Inject pipeline variables directly as rows into the existing multi-vault comparison table.
- **Behavior**:
  - A new selector in the Ribbon: **"Cross-check Pipeline"** (autocomplete search).
  - Secret rows derived from the pipeline display a badge: `[Pipeline: order-service]`.
  - For vaults linked to environments where the secret is present, values appear normal.
  - In vaults where the secret is missing, the cell is highlighted in **solid red** with text `[MISSING FOR PIPELINE]`.
- **Pros**: Direct reuse of the existing comparison grid, diffing, and copy-between-vaults tools.

### Option B: Dedicated "Pipelines & Releases" Tab
- **Concept**: A unified DevOps control plane with two sub-views:
  1. **Releases Matrix**: The service-by-service production release and rollback tracker table.
  2. **Pipeline Pre-Flight Check**:
     - Pipeline search and selection with branch selector (`main`, `release/*`).
     - Environment columns: `DEV`, `QA`, `STAGING`, `PROD`.
     - Matrix of all required secrets vs. each environment's Key Vault status.
     - Action buttons: "Create Secret in Vault" (pre-populating the secret name).
- **Pros**: Clear separation of concerns; avoids cluttering pure Key Vault comparisons with CI/CD pipeline logic.

---

## 5. Architectural & Implementation Roadmap

### Phase 1: ADO Pipeline & Template Discovery Backend
- [ ] Add `AdoPipelineService.cs`:
  - `GetPipelinesAsync(string org, string project, string search)`
  - `PreviewPipelineYamlAsync(string org, string project, int pipelineId)`
  - `ExtractVariablesFromYaml(string yamlContent)`
- [ ] Extend `AdoVariableGroupService.cs` to resolve `providerData.vault` name and associate it with cached Key Vault instances.

### Phase 2: Variable Cross-Referencing Engine
- [ ] Implement `PipelineVaultMatcher`:
  - Input: Pipeline ID, Environment/Variable Group mappings, Key Vault secret lists.
  - Output: Comparison model detailing required variables, resolved sources, and missing alerts.

### Phase 3: Release Branches & Rollback Engine
- [ ] Implement `AdoReleaseTrackerService.cs`:
  - Query recent production runs/releases across definitions.
  - Extract Git SHA, branch, Docker tag, author, and PR metadata.
  - Pair run `N` (current) and run `N-1` (previous/rollback).

### Phase 4: Frontend UI Components
- [ ] Implement `PipelineAutocomplete.vue` (quick search across ADO pipelines).
- [ ] Implement `PipelineSecretValidator.vue` (environment readiness grid with red missing alerts).
- [ ] Implement `ReleaseTrackerGrid.vue` (services x deployment metadata & rollback targets).
