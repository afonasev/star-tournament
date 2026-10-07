# Default profile label

Implementation: `157b8b5`. Standard Lab profile now displays Default in new and legacy histories, preserving custom names, snapshot values, revisions and hashes.

Validation: `git diff --check` and strict OpenSpec validation passed. Required `make check-ui` could not start Unity: shared admission was held by a foreign Star Racing exclusive Player. Cancelled only the owned queued runner (exit 130; no Unity started; no XML). Lab tests, native build/visual smoke and integration remain pending. Automated results do not constitute human acceptance.

Resume from branch `codex/rename-lab-default-profile`: run `make check-ui`, `unity/tools.sh test-edit-filter DesignLabHistoryTests`, `unity/tools.sh test-play-filter DesignLabWorkspaceTests`, `make build`, muted exclusive native `-labReview` smoke; then integrate after gates. Native smoke must include legacy and new profile labels and must use isolated history copies. User persistent history has not been written.
