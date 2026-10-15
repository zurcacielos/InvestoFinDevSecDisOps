# KeyVaultComparer - Functional Specification & Source of Truth

This document serves as the absolute source of truth for the system's structural behavior, user interface logic, and business rules. 

## 1. Ribbon Tabs & Navigation
The application features a top ribbon with distinct tabs that define the main views of the application:

### 1.1 Select Vaults
- **Purpose**: Interface for managing the target vaults.
- **Visual State**: Interface allowing the user to input and select multiple Azure Key Vault URIs (and AWS Secrets Manager environments). Contains input fields and a list of currently selected vaults.
- **Business Rules**:
  - **State Persistence**: Selected vault URIs are saved to `localStorage` and synchronized with the URL via Base64 encoding to allow state-sharing.
  - **Data Persistence**: Raw vault data (`vaultData`), metadata (`knownSecretNames`), and timestamps (`lastFetched`) are persisted in `localStorage`. Pressing `Ctrl+R` rehydrates the DOM without requiring re-authentication or re-fetching.
- **Acceptance Criteria**:
  - **Given** a user inputs a valid Vault URI, **When** they add it to the selection, **Then** the URI is added to `dataStore`, persisted to `localStorage`, and the URL is updated.
  - **Given** an active session with fetched secrets, **When** the user reloads the page (`Ctrl+R`), **Then** the UI must rehydrate the grid immediately using data from `localStorage`.

### 1.2 Analyze Data
- **Purpose**: The primary workspace for viewing, filtering, and comparing secrets across vaults.
- **Visual State**: Contains the filtering controls (Regex input, recent searches dropdown) and the main Data Grid (tabular view of secrets across vaults). 
- **Business Rules**:
  - **Dropdown Trigger**: Clicking the regex input field MUST always trigger the recent searches dropdown, regardless of whether it has a current value or not.
  - **Recent Filter Deletion**: The "X" button uses pure Vue reactivity (`@mouseenter`, `@mouseleave`, `v-show`) to ensure maximum reliability, bypassing CSS pseudo-class cascade issues. Clicking it removes the filter from `localStorage` and the recent list.
  - **Reused Values Indicator (Identicons)**: The grid displays an Identicon (Emoji) next to secret values that are identical. Controlled by UI toggles for row-level (cross-vault) or column-level (intra-vault) duplication.
  - **Highlighting**: Clicking an identicon highlights all identical values in the grid with a yellow background.

### 1.3 Staged Changes
- **Purpose**: Reviewing pending edits before committing them to the live vaults.
- **Visual State**:
  - **Controls**: Ribbon buttons to "Download script (PS1)" to apply changes locally via Azure CLI, and "Apply {X} changes to Azure" to commit batches of 5 directly via the API.
  - **Grid**: A dedicated view showing cells/secrets with modified but unsaved values. Marked visually distinct.
- **Business Rules**: Users can edit a secret's value directly in the Data Grid. Edits do not mutate live vaults immediately; they are placed in `stagedStore` and displayed here for review. Bulk actions apply the changes to the live vaults.
- **Acceptance Criteria**:
  - **Given** a user edits a cell, **Then** the new value is stored in `stagedChanges` and the cell reflects the local modification pending commit, visible in this tab.
  - **Given** the user clicks "Apply changes", **Then** a maximum of 5 staged changes are sent to the Azure Key Vault API after confirmation.

### 1.4 Inspections Report
- **Purpose**: A centralized security audit view detailing all flagged secrets.
- **Visual State**: 
  - **Controls**: Checkboxes to filter inspections by severity (`Critical`, `High`, `Medium`, `Low`) and export action buttons ("Copy Markdown", "Download as CSV").
  - **Findings List**: A vertical list of security inspection findings/cards. Each report item displays:
    - Severity badge (`Low`, `Medium`, `High`, `Critical`).
    - Secret Name and the Vault where it resides (e.g., `ANOTHER-APIKEY @ realet-test-rbac-kv`).
    - Inspection Title (e.g., "Reused Secret", "Suspicious Name (High Entropy)").
    - Description and actionable advice.
- **Business Rules**: Aggregates all security inspections across all vaults into a unified report:
  - **Metadata Inspections**: Secrets without an expiration date (Low) or not rotated in > 180 days (Medium).
  - **Pattern Matching**: Secrets matching hardcoded dangerous patterns (AWS Keys, JWTs, Connection Strings) trigger High/Critical alerts.
  - **Entropy & Complexity**: Password-like secrets are analyzed for minimum length (< 8) and Shannon entropy (< 3.0) to flag weak secrets.
  - **Reused Secrets**: High severity if a complex secret value is reused across different secrets.
  - **Filtering & Export**: Users can filter by severity. The filtered list can be exported to CSV or copied to the clipboard as Markdown.
- **Acceptance Criteria**:
  - **Given** a vulnerable or duplicated secret exists, **Then** it must be listed in this report with the correct severity, name, and vault context.
  - **Given** a user unchecks a severity filter, **Then** findings with that severity are hidden from the list and from any exported output.

### 1.5 Usage Stats
- **Purpose**: Visibility into when secrets were last accessed (Data Plane operations).
- **Visual State**: 
  - **Controls**: Ribbon button to "Fetch Usage Stats", which calls Azure Monitor Query. If audit logs are missing, displays a warning and an actionable "Download setup script (PS1)" button.
  - **Grid**: Displays a conditionally visible column "Last Used" right after the secret name.
- **Business Rules**:
  - The UI discovers the ARM Resource ID of the Key Vaults and their configured Log Analytics Workspaces.
  - It runs a KQL query on Azure Monitor filtering out traffic generated by `KeyVaultComparerApp` (`clientInfo_s !contains 'KeyVaultComparerApp'`).
  - The maximum `TimeGenerated` date is mapped back to the grid.

### 1.6 Logs
- **Purpose**: System and API operation observability.
- **Visual State**: A chronological, console-like list of system events, API requests, and errors.
- **Business Rules**: Displays logs for fetching status, authentication events, and API responses to aid in observability and debugging.

## 2. Authentication & Connection Architecture (Pinia authStore)

The connection to Azure Cloud and external platforms is strictly centralized in Pinia under `useAuthStore` as the single source of truth:

### 2.1 Azure Cloud Authentication (Key Vaults & Subscriptions)
- **Scope**: Azure Resource Manager (`management.azure.com`) and Key Vault Data Plane (`vault.azure.net`).
- **Primary Method (1-Click SSO)**: Interactive browser login via `POST /api/auth/login-interactive` (`DynamicAzureCredential`). Uses Microsoft's pre-consented public Client ID (`04b07795-8ddb-461a-bbee-02f9e1bf7b46`). Zero Azure Administrator rights or tenant app registrations required.
- **Local Fallback**: Local Azure CLI session (`az login`).
- **Automated / Headless Runner**: Service Principal configured in Settings (`AzureTenantId`, `AzureClientId`, and encrypted `AzureClientSecret`) for Docker or CI/CD pipelines.
- **Visual Notification**: If Azure Cloud is disconnected, a non-blocking top banner displays a 1-click **"Sign in with Microsoft"** action and a link to Settings.
- **Connection Method Transparency**: The UI explicitly shows the active connection method:
  - `Azure SSO Identity (Interactive / az login)` OR
  - `Service Principal (App ID & Secret)`
- **Profile Identity & Avatar**: Displays the authenticated user identity in the header and settings via `CurrentUserAvatar.vue`. Dynamically resolves the user's photo from Microsoft Graph (`https://graph.microsoft.com/v1.0/me/photo/$value`) with fallback to Azure DevOps profile avatar and initials badge (`{{ profile.initials }}`). Includes online indicator dot and connection details tooltip.

### 2.2 Azure DevOps Authentication (ADO Pipelines & Variable Groups)
- **Scope**: Azure DevOps REST API (`dev.azure.com`).
- **Default Method**: Automatically inherits the authenticated **Azure SSO Identity** (0 tokens required) via OAuth bearer token exchange (`499b84ac-1321-427f-aa17-267ca6975798`).
- **Override Mechanism**: If the target ADO organization is cross-tenant or requires dedicated access, the user can configure a Personal Access Token (`AdoPat`).
- **Visual Method Transparency**: Settings displays whether ADO is connected via:
  - `Azure SSO Identity (Recommended)` OR
  - `Personal Access Token (PAT Override)`

### 2.3 Secret Storage (Zero Plaintext)
- All client secrets, PATs, and GitHub tokens are encrypted at rest using `SecretStoreManager`.
  - Windows: Windows Credential Manager (`Advapi32.dll` DPAPI).
  - Linux/Docker: AES-256-GCM authenticated encrypted file (`secrets.enc`).

## 3. Azure DevOps Pipelines & Release Matrix

The application provides continuous delivery and deployment visibility across Azure DevOps pipelines via the Pipelines view:

### 3.1 Views & Modes
- **Target Environments View**: Matrix mapping pipelines to target deployment environments (`dev`, `uat`, `stg`, `prd`, etc.).
- **Expanded Stages (Hierarchical Run DAG) View**: Unfolds all executed stages in topological order (prerequisites to dependents) according to Azure DevOps YAML stage dependencies and timeline sequences.

### 3.2 Target Environments Matrix & Rollback (N-1) Architecture
- **In-Cell Stacked Deployment Cards**:
  - **Active Deployment (N)**: Displays the latest deployed build, tag/version, operation, branch pill, commit SHA, PR badge, upstream promotion lag status, author avatar badge, and time ago.
  - **Rollback Candidate (N-1)**: Secondary amber card displaying the immediate prior deployment for that environment, providing instant visibility into what version to roll back to.
  - If no previous deployment exists within the scanned window, displays a subtle notice (`No prior version in scan range`).
- **Scan Depth & Quality Filters**:
  - Configurable scan depth selector in the toolbar (`15`, `25`, `50`, `100` recent runs per pipeline).
  - `Only Succeeded (✓)` toggle to restrict deployment detection to successful pipeline runs.
  - Dynamic `Apply Filters` button that highlights with pending indicator when filters are modified.
- **Pipeline Selection Decoupling**:
  - Selected pipeline chips are rendered in the ribbon even prior to fetching the full catalog of pipelines.
  - Catalog retrieval (`Fetch Pipelines`) is decoupled from matrix querying, allowing the user to select rows without triggering automatic refresh.

### 3.3 Expanded Stages (Hierarchical Run DAG) Architecture
- **Hierarchical Layout (Option A)**:
  - **Main Pipeline Row**: Displays pipeline name, ID, repository information, action shortcuts (View YAML, Copy Full YAML, Open GitHub/ADO YAMLs), a run count badge, and a summary status across stages.
  - **Run Sub-Rows (1 row per build run)**: Expanding a pipeline unfolds its recent historical runs cleanly without inter-run stage collision.
- **Factored Run-Level Metadata**:
  - Run-level constants (Build # link to Azure DevOps, overall status badge, branch pill, commit SHA link, PR badge, author avatar badge via `PipelineAuthorBadge.vue`, and deployment timestamp) are factored directly into the left sticky columns.
  - Eliminates visual noise and redundant repetition across the 20+ stage columns.
- **Stage Cells**:
  - Show focused stage status badges (`Succeeded`, `Failed`, `In Progress`, `Partial`, `Canceled`) only for stages that were executed during that specific run.
  - When start and finish timestamps are available, display stage-level execution duration (e.g., `1m 24s`, `45s`).
  - Stages not run in a specific build display an elegant, subtle dash (`—`).
- **Global & Row-Level Controls**:
  - "Expand All" / "Collapse All" toggle in the sub-header bar.
  - Individual chevron toggle per pipeline.


