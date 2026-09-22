# KeyVaultComparer - Functional Specification & Source of Truth

This document serves as the absolute source of truth for the system's structural behavior, user interface logic, and business rules. 

## 1. Vault Selection & Authentication (Ribbon)
### Visual State
- A top ribbon interface allowing the user to input and select multiple Azure Key Vault URIs (and AWS Secrets Manager environments).
- Input field for entering vault URIs manually.
- List of currently selected vaults.
### Business Rules
- **State Persistence**: Selected vault URIs are saved to `localStorage` and synchronized with the URL via Base64 encoding to allow state-sharing.
- **Data Persistence**: Raw vault data (`vaultData`), metadata (`knownSecretNames`), and timestamps (`lastFetched`) are persisted in `localStorage`. Pressing `Ctrl+R` rehydrates the DOM without requiring re-authentication or re-fetching.
### Acceptance Criteria
- **Given** a user inputs a valid Vault URI, **When** they add it to the selection, **Then** the URI is added to `dataStore`, persisted to `localStorage`, and the URL is updated.
- **Given** an active session with fetched secrets, **When** the user reloads the page (`Ctrl+R`), **Then** the UI must rehydrate the grid immediately using data from `localStorage`.

## 2. Filtering & Search
### Visual State
- Input field for Regex-based filtering of secret names.
- Dropdown menu showing recently used Regex filters.
- A dark gray "X" button on the right edge of each dropdown item, becoming visible only when the user hovers over the specific item.
### Business Rules
- **Dropdown Trigger**: Clicking the regex input field MUST always trigger the recent searches dropdown, regardless of whether it has a current value or not.
- **Recent Filter Deletion**: The "X" button uses pure Vue reactivity (`@mouseenter`, `@mouseleave`, `v-show`) to ensure maximum reliability, bypassing CSS pseudo-class cascade issues. Clicking it removes the filter from `localStorage` and the recent list.
### Acceptance Criteria
- **Given** the user clicks the regex input, **When** they do so, **Then** the recent filters dropdown must appear immediately.
- **Given** the user hovers over a recent filter `<li>`, **When** the mouse enters the element, **Then** the dark gray "X" button (`&times;`) must appear aligned to the right.
- **Given** the user clicks the "X" button, **Then** the specific filter must be removed from the recent history in state and `localStorage` without applying the filter.

## 3. Data Grid & Comparison
### Visual State
- A tabular grid where rows represent unique Secret Names and columns represent different Vaults.
- Cells display the secret value (or hidden asterisks) and specific statuses (Missing, Error, Forbidden).
### Business Rules
- **Reused Values Indicator (Identicons)**: The grid displays an Identicon (Emoji) next to secret values that are identical. This is controlled by UI toggles for row-level (cross-vault) or column-level (intra-vault) duplication.
- **Highlighting**: Clicking an identicon highlights all identical values in the grid with a yellow background.
### Acceptance Criteria
- **Given** the same secret value exists in two different vaults for the same name, **When** `identiconsByRow` is active, **Then** the identical values must render the same emoji identicon.

## 4. Security & Inspections
### Visual State
- Inspection badges (L, M, H, C) representing Low, Medium, High, and Critical severities rendered next to secret values.
### Business Rules
- **Metadata Inspections**: Secrets without an expiration date (Low) or not rotated in > 180 days (Medium).
- **Pattern Matching**: Secrets matching hardcoded dangerous patterns (AWS Keys, JWTs, Connection Strings) trigger High/Critical alerts.
- **Entropy & Complexity**: Password-like secrets are analyzed for minimum length (< 8) and Shannon entropy (< 3.0) to flag weak secrets.
- **Reused Secrets**: The system maintains a `vulnerableValuesMap`. If a secret value is highly complex (not a trivial boolean/number) and is reused across different secrets, it generates a "Reused Secret" inspection (High severity).
### Acceptance Criteria
- **Given** a secret value matches an AWS Access Key regex, **Then** the system must attach a Critical inspection result to that cell.
- **Given** a secret lacks an `expiresOn` metadata property, **Then** the system must attach a Low severity inspection result.

## 5. Staged Changes
### Visual State
- Cells with modified but unsaved values are visually distinct (marked as staged).
### Business Rules
- Users can edit a secret's value directly in the grid.
- Edits do not mutate the live Azure vault immediately; they are placed in `stagedStore` (Staged Changes) for review.
### Acceptance Criteria
- **Given** a user edits a cell, **Then** the new value is stored in `stagedChanges` and the cell reflects the local modification pending commit.
