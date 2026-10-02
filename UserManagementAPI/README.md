# User Management API

ASP.NET Core Web API for basic user management. Users have a generated ID, name, email, department, and creation timestamp. The in-memory store is thread-safe, but data is cleared whenever the API process restarts; use a persistent database before relying on this API for real HR or IT records.

## Run

The Development launch profile supplies the local-only token `local-development-only-token`. With the ASP.NET Core 10 runtime installed:

```sh
dotnet run --project UserManagementAPI
```

The HTTP profile listens at `http://localhost:5253`. The OpenAPI document is available at `/openapi/v1.json` in Development. Set `Authentication__ApiToken` through a secret provider or environment variable outside local development. Do not reuse the development token in a deployed environment. If the ASP.NET Core shared runtime is unavailable, publish and run a self-contained Linux build:

```sh
dotnet publish UserManagementAPI/UserManagementAPI.csproj --configuration Release --runtime linux-x64 --self-contained true
Authentication__ApiToken=local-development-only-token ASPNETCORE_URLS=http://localhost:5253 ASPNETCORE_ENVIRONMENT=Development ./UserManagementAPI/bin/Release/net10.0/linux-x64/publish/UserManagementAPI
```

## Endpoints

| Method | Route | Result |
| --- | --- | --- |
| GET | `/api/users` | List users |
| GET | `/api/users/{id}` | Retrieve one user, or `404` |
| POST | `/api/users` | Create a user; returns `201` and a `Location` header |
| PUT | `/api/users/{id}` | Replace a user's editable fields, or `404` |
| DELETE | `/api/users/{id}` | Delete a user; returns `204`, or `404` |

POST and PUT require a name and department of at most 100 characters and a valid email address of at most 254 characters. Invalid payloads return `400` with field-level validation details. Unhandled exceptions and otherwise-empty error responses are converted to Problem Details by centralized middleware.

All `/api` routes require `Authorization: Bearer <token>`. Missing or invalid tokens return `401` and a `WWW-Authenticate` challenge. The configured token is an opaque shared development token, not a user identity or production JWT; use an identity provider and JWT bearer authentication for production deployments. The pipeline runs exception handling first, token validation next, and request logging last. The auth middleware audits rejected requests itself because those short-circuit before the request logger. Logs include method, path, response status, and duration, but never token values or request bodies.

## Test requests

Start the API, then run the requests in `UserManagementAPI.http` from VS Code with the REST Client extension or another HTTP client. The sequence covers missing and invalid tokens, authenticated CRUD, empty fields, display-name email input, malformed JSON, and missing IDs for retrieval, update, and deletion.

## Copilot assistance

During this debugging pass, Copilot reproduced a bug where display-name email syntax was accepted and stored with `201 Created`. It tightened email and length validation, replaced repeated list sorting with an array snapshot, added global Problem Details handling for unhandled exceptions, and expanded the HTTP regression requests for malformed data and missing IDs. Existing `404` behavior for missing users was confirmed rather than redundantly changed.

For this middleware phase, Copilot added structured request/response logging, fail-closed Bearer token validation with constant-time hash comparison, and pipeline ordering that runs exception handling before auth and logging. It also identified that auth short-circuits bypass downstream logging, so rejected requests are explicitly audited in the auth middleware. The HTTP request file now exercises missing, invalid, and valid tokens.