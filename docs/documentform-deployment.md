# DocumentForm deployment history

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

Run from the repository root. Existing PostgreSQL, pgAdmin and Azurite volumes must be preserved.

```powershell
docker start wardmate_postgres wardmate_pgadmin wardmate_azurite wardmate_documentform
```

- pgAdmin: http://localhost:5050 (existing root Compose login: `admin@admin.com`; password is the local value in root `docker-compose.yml`).
- Register PostgreSQL in pgAdmin with host `postgres`, port `5432`, username `wardmate_admin`, and the existing local PostgreSQL password. Use database `wardmate_documentform_db`.
- From Windows/Visual Studio, use host `localhost`; from the API container, use `postgres`.
- The recovered API uses a new, separate `wardmate_documentform_db`. Old `wardmate_db` remains intact; its records have not been copied.
- For Visual Studio, override `ConnectionStrings__DocumentDb` to select the new database. The checked-in appsettings still selects the old `wardmate_db`.
- Stop the API container before running Visual Studio on the same port 5004.
- API: http://localhost:5004/health. Swagger is disabled in this Production container.
- API Blob configuration uses `UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://azurite`. Bare `UseDevelopmentStorage=true` would address the API container's own loopback.

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

