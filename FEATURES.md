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
- **State Machine Architecture (`azureConnectionStore.ts`)**:
  - The application connection lifecycle strictly follows a deterministic 5-state state machine:
    - `'disconnected'`: Unauthenticated. No background or active Azure/Key Vault requests are dispatched; encrypted cache reads are disabled.
    - `'checking'`: Fast non-blocking single-probe (`GET /api/auth/status`) during startup or reconnection.
    - `'login_prompt'`: Prompting state. Displays the non-blocking top banner with a 1-click **"Sign in with Microsoft"** action immediately on boot.
    - `'logging_in'`: Interactive browser login in progress.
    - `'connected'`: Authenticated. Token validated, cache session key unsealed, profile loaded, and cloud operations authorized.
- **Fast Single-Probe Startup**:
  - Cold boot issues exactly ONE fast probe (`GET /api/auth/status`) with configurable timeout (`azureAuthTimeoutSeconds`, default: 4s).
  - Eliminates multi-request 30-second cold boot hangs by deferring subscriptions, profile photo resolution, and Key Vault inventory until connection is confirmed.
- **Primary Method (1-Click SSO)**: Interactive browser login via `POST /api/auth/login-interactive` (`DynamicAzureCredential`). Uses Microsoft's pre-consented public Client ID (`04b07795-8ddb-461a-bbee-02f9e1bf7b46`). Zero Azure Administrator rights or tenant app registrations required.
- **Local Fallback**: Local Azure CLI session (`az login`).
- **Automated / Headless Runner**: Service Principal configured in Settings (`AzureTenantId`, `AzureClientId`, and encrypted `AzureClientSecret`) for Docker or CI/CD pipelines.
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
- **Ribbon 3-Row Architecture (`PipelineRibbonBar.vue`)**:
  - **Row 1 (Source & Catalog)**: Context label (**`ADO`**), `Org` and `Project` input fields, `Fetch / Re-fetch Pipelines` button, and pipeline search combobox. Cache status and relative timestamps are surfaced strictly via hover tooltips (`title`) on the button and combobox.
  - **Row 2 (Selected Entities / Dynamic Chips)**: Directly beneath the search input, displays `Matrix Rows:` with selected pipeline chips, external template YAML copy shortcuts, and removal triggers. The container expands dynamically as items are added without shifting subsequent control tiers.
  - **Row 3 (Matrix Controls & Action Triggers)**: Positioned immediately adjacent to the grid surface. Houses the view mode toggle (**"Environment"** and **"Expanded"**), Scan depth selector (`15`, `25`, `50`, `100`), `Only Succeeded (✓)` toggle, and `Parametrization` settings button on the left, paired with the **"Refresh Matrix"** execution button and **"Clear All"** action docked on the right.
- **Environment Matrix View**: Matrix mapping pipelines to target deployment environments (`dev`, `uat`, `stg`, `prd`, etc.).
- **Expanded Stages (Hierarchical Run DAG) View**: Unfolds all executed stages in topological order (prerequisites to dependents) according to Azure DevOps YAML stage dependencies and timeline sequences.

### 3.2 Target Environments Matrix & Interchangeable Layout Modes
- **Integrated Service / Pipeline Column**:
  - The standalone `REPOSITORY` column is eliminated and merged into the sticky left `SERVICE / PIPELINE` column, displaying the pipeline name, definition link, YAML AST shortcuts, repository link, and default branch. Raw pipeline IDs are omitted from display to maintain high information density without clutter.
- **Minimalist Status & Progressive Disclosure**:
  - Replaces bulky "SUCCEEDED" pill badges with minimalist Azure-style circular indicators (`✓` green check, `✕` red cross, pulsing blue dot).
  - Compact author badges hide verbose names from the default cell view while retaining full user and trigger details in native hover tooltips.
- **Three Interchangeable Layout Modes**:
  - **Rows Mode (`rows`)**: Default view. Strict cross-environment row alignment. Enforces identical Y-positions for Version, Commit, Branch, and Time across all environment columns, facilitating rapid horizontal eye scanning.
  - **Cards Mode (`cards`)**: Compact active card view featuring prominent top-line version tags, active deployment metadata, and a docked compact $N-1$ previous run ribbon.
  - **Columns Mode (`columns`)**: Temporal in-environment comparison. Splits each environment cell vertically into two sub-columns (Prior Run $N-1$ on the left, Active Run $N$ on the right) for side-by-side auditing without vertical scrolling.
- **Environment Column Context Menu & Dynamic Column Hiding**:
  - Environment column headers (`<th>`) feature a right-click context menu matching the comparison grid pattern (`Hide this column`, `Hide Others`, `Show hidden columns`).
  - Right-clicking any deployment cell within an environment also provides column-hiding shortcuts in the shared context menu.
  - Whenever one or more environments are hidden, an interactive button `Show all hidden columns ({N})` is rendered in the sub-header controls bar, as well as an option in the context menu.
  - Column hidden state is persisted across reloads in `localStorage` under `adoHiddenEnvironments`.
- **Streamlined Previous Run ($N-1$) Deployment**:
  - Prior deployments ($N-1$) in `EnvironmentCellRows.vue`, `EnvironmentCellColumns.vue`, and `RollbackRibbon.vue` do NOT display raw or abstract build/run numbers (`#...`).
  - Prior runs display the version tag (`v...`) or commit SHA directly. Clicking this version tag navigates straight to that specific prior run in Azure DevOps.
- **Pipeline Selection Decoupling & Manual Refresh Flow**:
  - Selected pipeline chips and definitions are persisted across sessions in `localStorage` under `adoCachedPipelines` and `adoCachedPipelineRows`.
  - On application startup, if pipelines were previously fetched, the search combobox is immediately pre-filled and enabled (e.g. `Search 497 pipelines...`), and the fetch button is automatically labeled **"Re-fetch Pipelines"** with cache details surfaced via hover tooltip.
  - Selecting a new pipeline or modifying the scan depth / only-succeeded filter never triggers an automated network query. Newly selected pipelines mount immediately with a placeholder row in the grid.
  - The **"Refresh Matrix"** button is styled with colored active borders, a ring, and an indicator pulse (`hasPendingChanges: true`) to inform the user that changes are pending manual execution.
  - Clicking "Refresh Matrix" passes `forceRefresh: true` to bypass server-side cache and re-scans all selected rows.
- **Per-Pipeline Circular Refresh (`↻`)**:
  - Each pipeline row in both the cross-environment matrix (`PipelineServiceColumn.vue`) and hierarchical DAG view (`ExpandedStagesView.vue`) features a dedicated circular refresh button (`↻`).
  - Clicking this button refreshes only that specific pipeline row at the currently selected scan depth (`scanDepth`), rendering an inline spinning icon and updating its environments upon completion.
- **Stage-Level Multi-Stage Pipeline Evaluation & Scan Depth Tracking**:
  - ADO build exploration evaluates run results at the individual stage level rather than filtering at the top-level build endpoint, preventing multi-stage pipelines with downstream failures from hiding upstream environments.
  - Discovered active ($N$) and prior ($N-1$) deployments track their exact 1-based run position in the scanned build window (`scanPosition`), displayed via hover tooltips for run telemetry.
- **On-Demand Per-Pipeline Deep Scan (`EmptyEnvironmentCell.vue`)**:
  - Sparse environments where no deployment was found within the initial scan window (`scanDepth: 15/25/50`) display a recoverable empty state (`—` with an actionable `Scan deeper` or `Scan pipeline` link).
  - Hovering provides full context via native tooltip (`No deployment found in the last {N} runs for '{pipeline}' on '{env}'. Click 'Scan deeper' to search up to {N+50} runs.`).
  - Clicking `Scan deeper` triggers an isolated, non-blocking single-pipeline query (`POST /api/devops/pipelines/{id}/deep-scan`) without reloading or freezing the rest of the matrix.
  - Replaces the dash with an inline micro-spinner (`Scanning +50...`).
  - Upon completion, the returned pipeline row hydrates in-place in Pinia (`releaseMatrix.rows`), automatically populating the clicked environment as well as any other sparse environments found in the extended run history for that pipeline.
- **Acceptance Criteria**:
  - **Given** an initial load or reset of the environment matrix, **Then** "Rows (Cross-Env)" is selected as the default layout mode.
  - **Given** a user opens the application after a previous session where pipelines were retrieved, **Then** the combobox is active with the pipeline catalog count, the button shows "Re-fetch Pipelines", hover tooltips reveal cache status, and selected matrix row chips render their full pipeline names.
  - **Given** a newly selected pipeline, **When** chosen from the combobox, **Then** it mounts immediately in the grid without an automated network fetch, and the "Refresh Matrix" button displays colored active borders and a pending indicator.
  - **Given** an individual pipeline row, **When** clicking its circular refresh button (`↻`), **Then** only that row is queried and refreshed against Azure DevOps at the current scan depth.
  - **Given** an environment cell with a prior deployment, **When** the user clicks the previous version tag (`v...`), **Then** that previous run opens directly in Azure DevOps in a new tab without rendering an abstract build number.
  - **Given** an environment cell with no deployment found within the current scan depth, **When** the cell is viewed, **Then** it renders a dash (`—`) and a "Scan deeper" link with an explanatory tooltip.
  - **Given** the user clicks "Scan deeper", **Then** only that specific pipeline executes a deep scan for up to 50 additional runs, displaying an inline micro-spinner and hydrating all discovered environments in-place without triggering a global matrix reload.
  - **Given** an active deployment and a prior deployment, **When** "Columns" mode is enabled, **Then** the cell splits vertically showing Prior Run $N-1$ on the left and Active Run $N$ on the right.
  - **Given** an environment column header, **When** right-clicking on the header, **Then** a context menu appears with options to "Hide this column", "Hide Others", and if any columns are hidden, "Show hidden columns".
  - **Given** one or more hidden environment columns, **Then** a button "Show all hidden columns ({N})" is displayed in the sub-header toolbar and clicking it restores all hidden columns.
  - **Given** the "Prior Run (N-1)" checkbox is toggled off, **Then** the previous run elements are hidden across all 3 visualization modes, collapsing row height.

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

### 3.4 Architecture & Component Decomposition
- **Frontend Matrix Components (`keyvaultcomparer-ui/src/components/Pipelines/matrix/`)**:
  - `PipelineServiceColumn.vue`: Pinned column coordinating pipeline identification, AST actions, and repository deep-linking.
  - `EnvironmentCellCards.vue`: Card-based cell with top-line version anchor and docked $N-1$ ribbon.
  - `EnvironmentCellRows.vue`: Tabular cell enforcing uniform vertical spacing across all environments for straight horizontal version comparison.
  - `EnvironmentCellColumns.vue`: Side-by-side temporal split comparing present vs past deployments within each environment.
  - `RollbackRibbon.vue`: Reusable prior run delta component displaying return arrow, prior version, commit SHA, and time.
  - `DeploymentStatusIndicator.vue`: Minimalist Azure-green status icon eliminating text noise.
  - `pipelineUrlHelpers.ts`: Shared URL resolution logic for Azure DevOps builds, commits, PRs, and GitHub integration.
- **State Management (`devopsDataStore.ts`)**:
  - Centralizes `matrixLayoutMode` (`'cards' | 'rows' | 'columns'`), `viewMode` (`'environments' | 'stages'`), `scanDepth`, `onlySucceeded`, and `showRollbackTarget`, persisted in `localStorage`.

## 4. Local Session-Bound Encrypted Cache (Option 1) & Cold-Start Acceleration

### 4.1 Cryptographic & Memory Architecture
- **Encryption Algorithm**: AES-256-GCM authenticated encryption (`1 byte version [1] + 12-byte random nonce + 16-byte authentication tag + ciphertext`).
- **Session-Bound Key Derivation (RAM Only)**:
  - Ephemeral master key is derived via `HKDF(SHA256)` using the active Azure Entra ID bearer token + machine-specific entropy (`ProtectedData.Protect` DPAPI on Windows).
  - The derived key resides exclusively in volatile memory (`byte[]` zeroized via `Array.Clear` on user logout or process termination).
  - Zero plain text on disk: `cache.enc` is stored in `%AppData%/KeyVaultComparer/cache.enc`. Without an active, authenticated Azure SSO session, disk contents cannot be decrypted.
  - Zero Cloud Costs: 100% local cryptographic operations; no Key Vault HSM, Azure Managed Keys, or external cloud resources are created or billed.

### 4.2 Cached Domains & Intelligent Invalidation
- **Pipelines Catalog (`Pipelines`)**: Pipeline definitions and IDs across the organization/project.
- **Pipeline Deployment Matrix (`PipelineRuns`)**: Historical runs, active and prior deployment metadata, and environment matrices.
- **Vault Secret Inventory (`VaultKeys`)**: Secret names and discovery metadata per Key Vault URI. Automatically invalidated when staged changes are committed via `ApplyChangesAsync`.
- **Vault Discovery (`VaultDiscovery`)**: Discovered Key Vault resources per Azure subscription.
- **Subscriptions (`Subscriptions`)**: Azure subscription directory listings.
- **Variable Groups (`VariableGroups`)**: Azure DevOps variable group definitions and variables. Automatically invalidated when variables are modified.

### 4.3 Settings Modal Integration (`SettingsModal.vue`)
- **Navigation**: Dedicated "Encrypted Cache" tab accessible from the global settings modal (`Ctrl+,`).
- **Live Security Badge**: Real-time indicator displaying whether the cache is "Unlocked (Active Azure Session)" or "Sealed (Login Required)".
- **Metrics Dashboard**: Displays live counts of cached entries, encrypted file size in bytes/KB, cache mode (Enabled/Bypassed), and protection level.
- **Configurable Time-To-Live (TTL)**:
  - Pipelines Catalog TTL (minutes, default: 60)
  - Matrix Runs & Rollback History TTL (minutes, default: 30)
  - Vault Secret Names & Keys TTL (minutes, default: 120)
  - ADO Variable Groups TTL (minutes, default: 60)
- **Granular & Global Purge Controls**:
  - Per-category "Purge" buttons to wipe specific domains without losing others.
  - "Clear Entire Cache" button to instantly purge all stored records and wipe `cache.enc`.
  - In-DOM feedback notifications without native browser dialogs.

### 4.4 Acceptance Criteria
- **Given** an authenticated user who previously fetched pipelines or vault keys, **When** restarting or refreshing the application, **Then** all previous data renders immediately upon sign-in from `cache.enc` without redundant Azure/ADO API latency.
- **Given** an unauthenticated state (logged out), **When** reading `cache.enc`, **Then** the file is inaccessible and cannot be decrypted without completing Azure SSO.
- **Given** a user changes TTL values or clicks "Purge", **Then** the cache updates dynamically without requiring an application restart.


