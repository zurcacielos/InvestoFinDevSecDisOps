# KeyVault Comparer - Roadmap & TODOs

This document outlines the priorities for improving the tool, specifically targeting the workflows of QA, DevOps, and Developers working in lower environments (DEV/QA/UAT).

## Prioridad Alta (High Priority)

### 1. Secret Metadata: Last Used & Expiration
- **Problem:** DevOps needs to identify orphaned secrets or credentials that are about to expire.
- **Action:** Fetch and display metadata (Last Used timestamp, Expiration Date) for each secret.
- **UI:** Display this metadata contextually (e.g., an hourglass icon for expiring secrets, or directly in the cell header).

### 2. Anti-Production Visual Warnings
- **Problem:** We want to prevent accidental modifications to Production environments since this tool targets lower environments.
- **Action:** Implement a safeguard that parses the vault name (looking for `prod`, `prd`, `production`, etc.).
- **UI:** If a production vault is selected, clearly warn the user by highlighting the column header in bold red and displaying a warning/lock icon.

---

## Prioridad Media (Medium Priority)

### 3. Project Workspaces (Session State)
- **Problem:** Developers and QA often compare the exact same 3-4 vaults every day. Re-selecting subscriptions and vaults on every page reload is tedious.
- **Action:** Implement a `New, Open, Save` project management system.
- **Mechanism:** Allow users to save their current selections (Subscription, Vaults, regex filters) into a local configuration file (e.g., a `.json` file) and load it later to restore the workspace instantly.

### 4. Soft-Delete / Row Hiding
- **Problem:** QA might only be interested in 5 specific secrets out of a 300-secret list, but the regex filter isn't always enough to isolate them perfectly.
- **Action:** Add a local "Soft Delete" or "Hide" mechanism.
- **UI:** A button (e.g., an eye with a slash) on each row to temporarily hide it from the grid during the current session.

---

## Backlog / Prioridad Baja (Low Priority)

### 5. Risk Acceptance (False Positives)
- Allow users to right-click a Security Inspection warning (Entropy/Dangerous Pattern) and mark it as "Accepted Risk". 
- Save this preference locally so the alert doesn't show up in future sessions.

### 6. Export Security Reports
- Export the current view (including security warnings and staged changes) to a Markdown/PDF/CSV report to attach to Jira tickets for QA certification.
