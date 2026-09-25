# WardMate engineering workflow

- Scope each task to the user's requested feature. Use .NET 8 and preserve Clean Architecture and database-per-service boundaries.
- Start from a clean working tree; preserve unrelated user changes. Fetch origin, checkout `kha`, and pull `origin kha` with `--ff-only`.
- Create a feature branch named `kha-feat-<feature>` (or the exact name requested by the user).
- Implement the feature and relevant unit/integration tests. Require a solution-wide Release build with 0 errors and 0 warnings, and all tests passing before merge.
- Maintain root `PROGRESS.md` after each completed task/session: task ID/name, completion time with timezone, changed/new paths, endpoint methods/routes/request bodies/status codes, test counts/results, and frontend DTO/ProblemDetails/token notes. Record incomplete work honestly.
- Commit with Conventional Commits. Merge the feature into `kha` and push `origin kha`; then merge `kha` into `deploy` and push `origin deploy`. These pushes are authorized by the repository owner's standing workflow request. Never force push or discard remote work. Resolve conflicts deliberately and rerun affected checks.
- Commit granularity: every newly created file gets its own commit. For existing files, commit each functional update separately. Do not bundle unrelated file additions or features into a single commit and do not squash these commits during merge. Validate the complete feature before merging; dependency-building intermediate commits may not compile independently.
- `deploy` is the requested staging/production handoff branch. Do not claim deployment succeeded merely because Git push succeeded; report CI/CD status only when verified.
- Do not commit secrets, generated build outputs, or real user data.
