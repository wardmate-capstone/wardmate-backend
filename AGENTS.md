# WardMate engineering workflow

- Scope each task to the user's requested feature. Use .NET 8 and preserve Clean Architecture and database-per-service boundaries.
- Start from a clean working tree; preserve unrelated user changes. Fetch origin, checkout `kha`, and pull `origin kha` with `--ff-only`.
- Create a feature branch named `kha-feat-<feature>` (or the exact name requested by the user).
- The owner removed the test projects on 2026-09-30. Validate changes with appropriate available checks and require a solution-wide Release build with 0 errors and 0 warnings before merge. Do not claim automated test coverage or recreate test projects without a new user request.
- Maintain root `PROGRESS.md` after each completed task/session: task ID/name, completion time with timezone, changed/new paths, endpoint methods/routes/request bodies/status codes, test counts/results, and frontend DTO/ProblemDetails/token notes. Record incomplete work honestly.
- Commit with Conventional Commits. Merge sequence: merge the feature into `kha` and push `origin kha`; then merge `kha` into `main` and push `origin main`; finally merge `main` into `deploy` and push `origin deploy`. The `deploy` branch is designated for Azure continuous deployment so Frontend can consume live APIs without running backend locally. These pushes are authorized by the repository owner's standing workflow request. Never force push or discard remote work. Resolve conflicts deliberately and rerun affected checks.
- Commit granularity: every newly created file gets its own commit. For existing files, commit each functional update separately. Do not bundle unrelated file additions or features into a single commit and do not squash these commits during merge. Validate the complete feature before merging; dependency-building intermediate commits may not compile independently.
- `deploy` is the requested staging/production handoff branch connected to Azure. Do not claim deployment succeeded merely because Git push succeeded; report CI/CD status only when verified.
- Do not commit secrets, generated build outputs, or real user data.
