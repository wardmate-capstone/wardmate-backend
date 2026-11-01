# DocumentForm deployment history

## Document Readiness & Notification Hardening Deployment — 2026-10-17 22:45:00 +07:00

Deployed 3 updated microservice images to Azure Container Registry `acrwardmate2026.azurecr.io`:
1. **ApplicationWorkflow**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-application-workflow:workflow-20261017-01`
   - Digest: `sha256:74cf71f74f1111cf130298dcba0dae582f5015eeb3f3cb181f2a22896ad3a111`
   - Features: Document Readiness workflow (`DOCUMENTS_READY`, `documents_ready_at`, `POST /api/v1/officer/applications/{id}/confirm-readiness`).
2. **DocumentForm**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-documentform:task25-readiness-20261017-01`
   - Digest: `sha256:802784655ef943bdf3fe18981c47ad548182f6065711675bcbdd08cef787dcb6`
   - Features: Alignment of submissions with `ReadyForDownload` (status 4) and pure Gotenberg PDF generation.
3. **AnalyticsSystem**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-analytics-system:harden-20261017-01`
   - Digest: `sha256:bc63f963c0f016e86c294a84a7f0b7f612c8d200abbac9d0fcf42bca31e17aac`
   - Features: Event consumer adaptation for readiness; inbox worker rollback on failure with bounded retries (Dead after 10); per-connection IAM & ward authorization guards for SignalR realtime delivery.

Git synchronization:
- Synchronized branches `kha`, `main`, and `deploy` to commit `43479d6`.

Container App update notes:
- Update Container App `wardmate-application-workflow` in `rg-wardmate-prod` to image tag `workflow-20261017-01`.
- Update Container App `wardmate-documentform` in `rg-wardmate-prod` to image tag `task25-readiness-20261017-01`.
- Update Container App `wardmate-analytics-system` in `rg-wardmate-prod` to image tag `harden-20261017-01`.
- Set `Database__AutoMigrate` to `false` across Container Apps as DB migrations are verified in Supabase.

## Cross-Service Validation & Dual-Role Access Review Deployment — 2026-10-15 07:25:00 +07:00

Deployed 3 updated microservice images to Azure Container Registry `acrwardmate2026.azurecr.io`:
1. **DocumentForm**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-documentform:task18-review-20261015-01`
   - Digest: `sha256:7fa22a3ecf88f877cb57066553c0e81a7bdcc1458d22ff8a1785888f34417ad0`
   - Features: Preserved owner access on shared read routes for dual-role accounts (citizen + Front Desk officer); officer review checks remain strictly scoped.
2. **ApplicationWorkflow**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-application-workflow:workflow-20261015-01`
   - Digest: `sha256:d4e7f933464882ef4a91b22ab55ed57106fff9d0e56da1435c18caa4162014a0`
   - Features: Pre-validation on resubmit commands against nulls/duplicates, rejecting malformed document evidence with 503 instead of corrupting counts.
3. **ProcedureCatalog**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-procedure-catalog:v8-checklist-review-20261015`
   - Digest: `sha256:6f5d9dacd30a1dab6727dae7a25a53ebbd22f4a5123c9183671eb631bb2da331`
   - Features: Case reference validation across checklist items and form definitions; whitespace trimming; max 500 checklist items constraint.

Container App update notes:
- Update Container App `wardmate-documentform` in `rg-wardmate-prod` to image tag `task18-review-20261015-01`.
- Update Container App `wardmate-application-workflow` in `rg-wardmate-prod` to image tag `workflow-20261015-01`.
- Update Container App `wardmate-procedure-catalog` in `rg-wardmate-prod` to image tag `v8-checklist-review-20261015`.

## Scoped Officer Review, Workflow-Governed Approval & Gotenberg PDF Deployment — 2026-10-14 16:30:00 +07:00

Deployed 3 core microservices images to Azure Container Registry `acrwardmate2026.azurecr.io`:
1. **DocumentForm**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-documentform:task18-gotenberg-20261014-01`
   - Digest: `sha256:5e920dd0e9943a61516eba80c037486b19f27322398b544dfd3f442e7d5b3b2d`
   - Features: Scoped officer access (same ward & assigned officer), single source of truth approval via Workflow, pure Gotenberg PDF rendering (comments stripped, dummy PDF fallback removed), lock citizen edit and officer annotation after approval.
2. **ApplicationWorkflow**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-application-workflow:workflow-20261014-02`
   - Digest: `sha256:bcd333a5ff163185b836162b31d0f4cef52487288a64611dc100239d27e4af01`
   - Features: Officer review controller scoping and assignment check, checklist / version details loading in DTO.
3. **ProcedureCatalog**:
   - Image: `acrwardmate2026.azurecr.io/wardmate-procedure-catalog:v7-checklist-20261014`
   - Digest: `sha256:5f68dec0a47f964de17a712fd8cdaf63a5b3a1db408be0b24e3d0256e1915b27`
   - Features: Checklist management and form template bindings.

Git synchronization:
- Synchronized branches `kha`, `main`, and `deploy` to commit `dc5e9f3bd9785f734408b5718ae1ec84dcd8b587`.

Container App update notes:
- Update Container App `wardmate-documentform` in `rg-wardmate-prod` to image tag `task18-gotenberg-20261014-01`.
  - Ensure env var `Gotenberg__Url` points to the internal Gotenberg service.
- Update Container App `wardmate-application-workflow` in `rg-wardmate-prod` to image tag `workflow-20261014-02`.
- Update Container App `wardmate-procedure-catalog` in `rg-wardmate-prod` to image tag `v7-checklist-20261014`.

## TASK-17 & TASK-18 deployment (Supporting & Generated Documents) — 2026-10-12 07:31:00 +07:00

User requested Azure deployment for supporting documents and PDF generation features. Deployed image
`acrwardmate2026.azurecr.io/wardmate-documentform:task17-18-20261012-01`, digest
`sha256:3b3971e41d4200775cb48db828c67e71f3d484d9b3a5c812022a9bfe5d2d58eb`.
Container App `wardmate-documentform` in `rg-wardmate-prod` reports `provisioningState=Succeeded`, `runningStatus=Running`.
Built image locally and pushed to ACR; updated image only, preserved existing secrets/config.
All public endpoints verified:
- `/health` -> 200 Healthy
- Swagger JSON confirms endpoints:
  - `POST /api/v1/applications/{applicationId}/supporting-documents`
  - `GET /api/v1/applications/{applicationId}/supporting-documents`
  - `DELETE /api/v1/supporting-documents/{documentId}`
  - `GET /api/v1/supporting-documents/{documentId}/download`
  - `POST /api/v1/applications/{applicationId}/generate-pdf`
  - `GET /api/v1/applications/{applicationId}/generated-documents`
  - `GET /api/v1/generated-documents/{documentId}/download`
Swagger: https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html

## DOCX editor deployment — 2026-10-07 15:40:24 +07:00

User approved local tests and explicitly requested Azure deployment. Deployed image
`acrwardmate2026.azurecr.io/wardmate-documentform:editor-20261007-01`, digest
`sha256:0e67569f1b39dad6ca3360253a05daee7629daaeb8ca608e63b435cf91fb0f26`.
Container App wardmate-documentform in rg-wardmate-prod reports Succeeded. Previous image was
online-20261007-0854; previous provisioning error was BuildFailed: no build sandbox capacity.
Built image locally and pushed to ACR; updated image only, preserved existing secrets/config.
Gateway unchanged; no restart required. No Git commands or branch pushes.

Five Azure HTTP checks passed200: /api/document-form/health, Swagger UI, Swagger JSON,
/api/v1/form-templates?page=1&pageSize=1 and citizen list for synthetic applicant UUID.
OpenAPI confirms multipart draft and absence of online-config. No cloud test records created;
full save/download/submit verified locally earlier (76 tests,60 HTTP checks). Docker release build
this session0 warnings/errors. Cloud file round-trip still available for owner testing, not claimed tested here.

Current contract: template create JSON/upload DOCX/download binary; citizen POST draft multipart
{templateId,applicantId,file}201, PUT draft multipart {applicantId,file}200,
GET list/detail/download200, POST submit JSON {applicantId}200. See docs/api-guide.md sections3/6
for errors and FE adapter. No JWT/identity or database migration changes in this deployment.

Swagger: https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html


## Historical notes (superseded contracts)

# DocumentForm: local recovery and Azure handoff

> 2026-10-07: the source now implements JSON online drafts (breaking API change). Apply
> `OnlineFormDrafts` to the DocumentForm database before running the new image. Citizen POST/PUT draft
> consumes JSON, not DOCX multipart. Configure each original through `docx-structure` and `online-config`.
> Officer review and template `docx-url` endpoints are removed. See `docs/api-guide.md`, sections 3 and 6.
> Deployed 2026-10-07: see the verified deployment record immediately below. Remaining sections are historical notes, not the current API contract.


## Verified Azure deployment — 2026-10-07 08:12 +07:00

- User explicitly requested direct Azure deployment of current local source for Swagger testing; no Git commands or branch pushes.
- DocumentForm image: `acrwardmate2026.azurecr.io/wardmate-documentform:online-20261007-0755`.
- ACR digest: `sha256:9b32a1a6c00ca1fe010cf3f8278cc5fec0369abf23e32cc9247c1edff3b74bfb`.
- Container App `wardmate-documentform`, resource group `rg-wardmate-prod`: Succeeded. Existing DB/Blob secret references preserved. Swagger__Enabled=true, Database__AutoMigrate=true. New version-table query and submission query succeed against existing Azure database; old rows preserved.
- Gateway image unchanged. Deduplicated DocumentForm destination to HTTPS FQDN and added `/api/v1/citizen/{**catch-all}` route to cluster `document-form`. Existing template route preserved. Express config update returned success while runtime retained old environment; stopped/started Gateway after explicit user approval for downtime. Runtime HTTP checks then passed.
- Swagger: https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html
- Seven final public HTTP checks: health200, Swagger UI200, Swagger JSON200, templates200, citizen list200, template detail200, empty JSON draft400. Swagger confirms online-config + JSON draft and no officer/docx-url endpoints. No cloud test records created. Full upload/fill/submit remains verified locally, available for owner testing on Azure.
- Docker build: 0 warnings/errors; local Docker health and Swagger checks passed; temporary verification container removed. Previous final source verification: whole solution Release 0 warnings/errors, 76 tests and 29 local HTTP checks passed.
- User clarified account/login belongs to the shared system. No separate DocumentForm login added; no JWT/identity integration changes in this deployment.
- Historical multipart/officer examples below describe the previous version only. Use docs/api-guide.md for the current JSON online workflow.

## Local environment

Run from the repository root. The old root `docker-compose.yml` was removed on 30/10/2026 (CLEANUP-02); PostgreSQL (pgvector), pgAdmin and Azurite now come from `docker/docker-compose.yml`.

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d postgres azurite pgadmin document-form
```

- If containers created by the old root Compose (`wardmate_postgres`, `wardmate_pgadmin`, `wardmate_azurite`) still exist, stop/remove them first to free ports 5432, 5050 and 10000–10002. Removing a container does not delete its volume; the new Compose uses its own volumes (`postgres_data`, `azurite_data` under the `docker` project), so old local data is not carried over automatically.
- pgAdmin: http://localhost:5050 (login configured in `docker/docker-compose.yml`).
- Register PostgreSQL in pgAdmin with host `postgres`, port `5432`, username `wardmate_admin`, and the local PostgreSQL password. Use database `wardmate_documentform_db`.
- From Windows/Visual Studio, use host `localhost`; from the API container, use `postgres`.
- The recovered API uses a new, separate `wardmate_documentform_db`. Old `wardmate_db` remains intact; its records have not been copied.
- For Visual Studio, override `ConnectionStrings__DocumentDb` to select the new database. The checked-in appsettings still selects the old `wardmate_db`.
- Stop the API container before running Visual Studio on the same port 5004.
- API: http://localhost:5004/health. Swagger is disabled in this Production container.
- API Blob configuration defaults to `UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://host.docker.internal` (Azurite ports published on the host). Bare `UseDevelopmentStorage=true` would address the API container's own loopback.

Build the image with repository-root context:

```powershell
docker build -t wardmate-documentform:v1 -f src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Dockerfile .
```

## Azure checkpoints (not yet deployed)

1. Confirm actual resource names. The supplied screenshot shows `rg-wardmate-prod`, IAM in Japan East, and environment `managedEnvironment-rgwardmateprod-bd51`. It does not establish Gateway's environment, PostgreSQL, ACR or Storage names. Repository and `deploy` branch are user-supplied; no Git commands were run.
2. Confirm PostgreSQL FQDN and administrator login from Overview; create `wardmate_documentform_db` with UTF8. Verify network access and any extensions required by EF migrations. Do not use local development credentials on Azure.
3. Confirm Storage account and create private `form-templates` and `submissions`. Current draft handler uses the configured default container (`form-templates`) and a `user-submissions/` prefix; creating `submissions` alone does not move draft files there.
4. Confirm actual ACR login server and push a uniquely tagged, locally verified image via the owner's release workflow. Do not create the app pointing at a nonexistent image. Antigravity handles all Git operations and the `deploy` branch.
5. Create `wardmate-documentform` in Gateway's actual Container Apps Environment. Internal ingress, target port 8080. Use secret references for the complete DB connection string and Azure Blob connection string.
6. Set `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://+:8080`, `Database__AutoMigrate=true`, `AzureBlob__ContainerName=form-templates`, `ConnectionStrings__DocumentDb` and `AzureBlob__ConnectionString`. Use the actual PostgreSQL host, database and administrator; never localhost. Validate TLS according to the server configuration.
7. Verify revision, logs, EF migration history and database-backed APIs. Startup currently catches migration errors; `/health` checks process liveness only and cannot prove DB readiness.
8. Gateway cluster is already `document-form`, destination `primary`. Override `ReverseProxy__Clusters__document-form__Destinations__primary__Address` with the verified internal address. App-name routing requires the same environment; verify HTTP/HTTPS ingress policy (HTTP can redirect when insecure traffic is disabled).
9. Verify `GET /api/document-form/health`, then template and submission APIs through Gateway. Configure CI/CD only after these checkpoints pass, using the Dockerfile above and context `.`.

## Actual API contracts and release gaps

| Method and route | Request | Expected response |
| --- | --- | --- |
| GET /health | None | 200 plain text Healthy |
| GET /api/v1/form-templates | Optional page/pageSize | 200 paginated object with items, not a bare array |
| POST /api/v1/form-templates | JSON code, title | 201 template DTO; 400/409 validation/conflict |
| POST /api/v1/citizen/submissions/draft | multipart/form-data: templateId, applicantId, file (.docx) | 201 SaveSubmissionResultDto; 400 validation |
| GET /api/v1/officer/submissions?status=Submitted | No body | 200 paginated submission DTOs; 400 unknown status |

The draft JSON in the proposed plan is not the current controller contract. Status filtering compares the mapped enum; unknown statuses return `{code:"document.invalid_submission_status",message:...}` with HTTP 400. General exceptions/model-binding failures use ProblemDetails; existing controller business errors use code/message objects.

DocumentForm and Gateway currently have no JWT authentication/authorization middleware or role enforcement for these routes. Do not treat a 200 response as evidence of access control. Authentication and ownership checks must be addressed before exposing real citizen records through the public gateway. This session does not change token contracts or implement authorization.

References: [Container Apps communication](https://learn.microsoft.com/en-us/azure/container-apps/connect-apps), [secret references in environment variables](https://learn.microsoft.com/en-us/azure/container-apps/environment-variables).

## Confirmed handoff configuration (2026-10-06)

- Registry login server supplied by owner: acrwardmate2026.azurecr.io.
- Prepared local image: acrwardmate2026.azurecr.io/wardmate-documentform:demo-20261006 (not yet pushed).
- Separate Supabase project: tdqmyzscflmlslfftxpe; session pooler aws-0-ap-northeast-1.pooler.supabase.com:5432; database postgres; username postgres.tdqmyzscflmlslfftxpe. Password belongs only in the Azure secret, not this repository. Connection/migrations not yet verified.
- Gateway: wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io; environment managedEnvironment-rgwardmateprod-bd51, Japan East.
- Set Swagger__Enabled=true on the demo Container App while keeping ASPNETCORE_ENVIRONMENT=Production. It is disabled in Production unless explicitly enabled.
- Expected Swagger URL after successful deployment/routing: https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html. This URL is not yet verified live.
- Swagger JSON uses a relative URL; generated requests use origin-root /api/v1 routes already defined in Gateway. No gateway config file changes required for those routes.
- Azure CLI was unavailable locally. Authenticate on the workstation before pushing the image. Do not send credentials in chat.



## DF-AZURE-ROLES — 2026-10-08T07:33:04.759580+07:00 (Asia/Saigon)
Owner requested immediate Azure deployment using shared IAM JWT configuration. Deployed acrwardmate2026.azurecr.io/wardmate-documentform:roles-20261008-01; digest sha256:a18fc3db2053af513707df5a5deca0f4c9b94b3f942f6e3db9c20a89bab126e5. Copied IAM signing key in memory into DocumentForm secret iam-jwt-key; configured Jwt__Key secretref and same IAM issuer/audience. No keys written into source/log/chat. IAM and Gateway unchanged. Local container remains old (local IAM key absent); user selected Azure instead of local key setup.
Cloud verification5 HTTP checks: health200, Swagger UI200, OpenAPI200 with Bearer definition/operation security, templates unauthenticated401, citizen list unauthenticated401. Does not claim real IAM login tested; role/identity checks were tested locally34 HTTP checks and76 existing tests. Docker build0 warnings/errors. All5 template routes require PROCEDURE_MANAGER, all6 citizen routes REGISTERED_CITIZEN and applicantId=JWT sub. No request DTO changes; auth401/403 ProblemDetails. Template reads are manager-only per request; citizen first-open template integration requires a separate read-access design.
Changed documentation paths: docs/documentform-deployment.md, docs/api-guide.md, PROGRESS.md. No Git/push or database writes. Suggested commit: feat(documentform): deploy IAM-backed role authorization.


## DocumentForm + Workflow deployment preflight — 2026-10-14T09:30:01+07:00
This checkpoint supersedes older statements that the deployed API has no authentication.
Scope authorized by owner: deploy Workflow dependency and updated DocumentForm, update only required Gateway routes;
do not redeploy IAM, ProcedureCatalog or unrelated services.
Observed Azure: DocumentForm image task18-annotate-20261013-01, no ApplicationWorkflow Container App.
DocumentForm has document-db-connection, blob-connection-string, iam-jwt-key secret references;
no Services__ApplicationWorkflow__BaseUrl. Registry acrwardmate2026.azurecr.io; existing shared Express environment
managedEnvironment-rgwardmateprod-bd51. Do not set revision suffix on this environment.
Read-only HTTP verification: health200, Swagger JSON200, unauthenticated templates401/citizen401,
application contents404 and absent from OpenAPI. Thus current cloud image does not contain the new linkage API.

Deployment sequence once Workflow database configuration is provided:
1. Obtain separate Workflow cloud database connection via ignored local WORKFLOW_AZURE_CONNECTION_STRING or secure secret input;
   never reuse DocumentForm tables/database credentials by assumption and never commit secrets.
2. Publish locally tested Workflow/DocumentForm images with unique tags. Configure Workflow JWT to match deployed IAM,
   Iam__BaseUrl to wardmate-iam HTTPS FQDN, ProcedureCatalog__BaseUrl to catalog HTTPS FQDN,
   Services__DocumentForm__BaseUrl to DocumentForm HTTPS FQDN; Swagger__Enabled=true for requested testing.
3. Apply Workflow migrations in its database and verify readiness; then deploy DocumentForm migration/image with
   Services__ApplicationWorkflow__BaseUrl set to verified Workflow URL. Preserve all existing DB/blob/JWT secrets.
4. Add only required Gateway Workflow destination/routes and document contents/supporting/generated routes; preserve other services.
5. Test actual IAM citizen/manager tokens: ownership, wrong-role403, anonymous401, app/checklist mismatch,
   original DOCX save/load, supporting files and aggregate contents. Use synthetic records only.
No deployment performed at this checkpoint: Workflow cloud database not yet identified/configured.
