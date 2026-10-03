# WardMate engineering workflow

- Scope each task to the user's requested feature. Use .NET 8 and preserve Clean Architecture and database-per-service boundaries.
- Preserve unrelated user changes. Codex must not run any Git commands. Antigravity exclusively manages branches, commits, merges and remote operations.
- Work only on local source/configuration/documentation and run local build, test and EF migration tooling. Do not create or switch branches.
- The owner removed the test projects on 2026-09-30. Validate changes with appropriate available checks and require a solution-wide Release build with 0 errors and 0 warnings before merge. Do not claim automated test coverage or recreate test projects without a new user request.
- Maintain root `PROGRESS.md` after each completed task/session: task ID/name, completion time with timezone, changed/new paths, endpoint methods/routes/request bodies/status codes, test counts/results, and frontend DTO/ProblemDetails/token notes. Record incomplete work honestly.
- On completion, report changed paths, build/test results and a suggested Conventional Commit message for Antigravity. Stop without executing Git. Previous standing authorization for Git operations is revoked by the owner's 2026-10-01 instruction.
- Commit granularity: every newly created file gets its own commit. For existing files, commit each functional update separately. Do not bundle unrelated file additions or features into a single commit and do not squash these commits during merge. Validate the complete feature before merging; dependency-building intermediate commits may not compile independently.
- Branch, Push & Deployment gate (Owner's 2026-10-03 instruction):
  1. ONLY push to branch `kha`. NEVER push to `main` or `deploy` without explicit user confirmation after testing Docker.
  2. Prior to any push to `kha`, MUST report the exact number of commits, the commit types, and commit messages to the user, and WAIT for user confirmation before pushing.
  3. After pushing to `kha`, build Docker image for the user to test. Push to `main` and `deploy` is ONLY allowed after user tests on Docker and gives explicit approval.
- `deploy` is the requested staging/production handoff branch connected to Azure. Do not claim deployment succeeded merely because Git push succeeded; report CI/CD status only when verified.
- Do not commit secrets, generated build outputs, or real user data.
