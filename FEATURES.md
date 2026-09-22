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

### 1.5 Logs
- **Purpose**: System and API operation observability.
- **Visual State**: A chronological, console-like list of system events, API requests, and errors.
- **Business Rules**: Displays logs for fetching status, authentication events, and API responses to aid in observability and debugging.
