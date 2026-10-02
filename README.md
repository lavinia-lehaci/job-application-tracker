# Job Application Tracker [![CI](https://github.com/lavinia-lehaci/job-application-tracker/actions/workflows/ci.yml/badge.svg)](https://github.com/lavinia-lehaci/job-application-tracker/actions/workflows/ci.yml)

A REST API for tracking job applications through the hiring pipeline. Built as a learning project, with a focus on authentication, data isolation, testing, and containerization.

## Tech stack
C#/.NET, ASP.NET Core Web API, EF Core, PostgreSQL, JWT authentication, Docker, XUnit, Testcontainers, GitHub Actions
 
## Features

- JWT-based registration and login, with hashed passwords 
- Full CRUD on job applications, scoped per user
- Filtering, sorting and pagination on the applications list
- Per-user data isolation enforced on every request 

## Running locally

Prerequisites: .NET SDK, Docker Desktop

1. Copy `.env.example` to `.env` and fill in your own values.
2. Run:
```powershell
   docker compose up --build
```
3. Open `http://localhost:8080/scalar/v1` to explore and test the API.

Migrations are applied automatically on startup, so a fresh clone needs no manual database setup.

## Running tests

```powershell
dotnet test
```

Integration tests use xUnit and `WebApplicationFactory`, run against a disposable PostgreSQL instance via Testcontainers, and require Docker to be running. Tests cover authentication, CRUD, filtering, sorting, pagination, and cross-user data isolation.

Tests run automatically on every push via GitHub Actions.

## Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | /api/auth/register | no | Create an account |
| POST | /api/auth/login | no | Returns a JWT |
| GET | /api/applications | yes | List applications |
| POST | /api/applications | yes | Create an application |
| GET | /api/applications/{id} | yes | Get a single application |
| PUT | /api/applications/{id} | yes | Update an application (without status) |
| PUT | /api/applications/{id}/status | yes | Update an application's status |
| DELETE | /api/applications/{id} | yes | Delete an application |
| DELETE | /api/applications | yes | Delete all of the caller's applications |

## Design decisions

- **DTOs instead of exposing entities**, to prevent over-posting and avoid leaking internal fields like password hashes or navigation properties.
- **Passwords hashed** with ASP.NET Core's `PasswordHasher`, never stored or logged in plain text.
- **Stateless JWT auth**: the signing key and database connection string are kept in user-secrets (local) or environment variables (Docker), never committed to source control.
- **Per-user data isolation**: every query filters by the user ID extracted from the token's claims. A request for another user's resource returns 404.
- **Controllers use `DbContext` directly**, with no generic repository layer, since `DbContext` already functions as a repository and unit of work for a project this size.
- **PATCH-style partial updates on `PUT`**: only fields present in the request body are changed; omitted fields are left untouched.
- **`UpdatedDate` only changes on a real modification**, using EF Core's change tracker to detect updates.

## Next steps

- Refresh tokens (current JWTs are short-lived with no revocation)
- A simple front end to interact with the API visually
