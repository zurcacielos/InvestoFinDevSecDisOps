# Next Steps - Environment Matrix UX & Architecture Improvements

## 1. Space and Architecture Optimization (High Priority)
- **Static Column Merging**: Eliminate the standalone "REPOSITORY" column and integrate its metadata (repository name and branch) as secondary text beneath "SERVICE / PIPELINE". Maintain this single column pinned (*sticky*) to the left. This recovers approximately 220px of horizontal space, allowing 5 to 6 environments to be viewed simultaneously without horizontal scrolling.
- **Noise and Redundancy Reduction**: Replace the large green "SUCCEEDED" pill with a minimalist indicator (such as a small Azure DevOps-style green circle check icon), as success status is already implied by the active filter.
- **Progressive Disclosure**: Hide large avatars, long full names, and secondary run IDs from the default cell view. Move exhaustive details into native hover tooltips.

## 2. Redesign of Previous Run / Rollback Information ($N-1$) (Medium Priority)
- **Elimination of Literal Nomenclature**: Completely remove the literal label "ROLLBACK (N-1)". Visual and temporal context is sufficient for an engineer to understand that it represents the previous execution.
- **Radical Compaction**: Convert the secondary card into a minimalist bar or ribbon docked beneath the active execution. Display strictly the critical delta: previous Tag/Version, Commit SHA, and elapsed time (with full date/time on hover). This cuts the cell vertical height in half.

## 3. Visual Traceability and Alignment (Medium Priority)
- **Horizontal Scanning Anchors**: Always position the Tag/Version on the very first line of each cell with prominent typographic weight. This ensures the user's eye can follow a straight horizontal line across all environments to compare versions without navigating uneven badges or chips.

## 4. Interchangeable Visualization Modes (New Feature)
Add a segmented control in the top toolbar to toggle the matrix layout according to the analysis goal:
- **Cards Mode**: Optimized default view, prioritizing the current execution with the compact previous run ribbon underneath, suited for general status overview.
- **Rows Mode (Cross-Environment Comparison)**: Strict tabular layout inside each cell. Row 1: Version, Row 2: Commit, Row 3: Branch, Row 4: Date, etc. Aligning identical data types at the same Y-position facilitates comparing UAT against STG and PRD in a rapid horizontal sweep. This ensures that the same data type resides on the exact same visual horizontal row across all environments, regardless of text wrapping.
- **Columns Mode (Temporal In-Environment Comparison)**: Split the environment cell vertically. Displays the active run ($N$) on the left and the previous run ($N-1$) on the right. Ideal for auditing changes within a specific environment without forcing vertical scrolling across multiple pipelines. Even if runs vary in width, specific data points remain in the same vertical column.
