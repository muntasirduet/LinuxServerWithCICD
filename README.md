# MyApp — .NET 8 Web API on Linux with CI/CD

This repository contains a production-oriented .NET 8 Web API setup with:

- ASP.NET Core Web API
- PostgreSQL via EF Core
- JWT Bearer authentication
- Serilog JSON console logging
- Nginx reverse proxy and systemd service templates
- GitHub Actions build/test/deploy workflow

## Project structure

- `src/MyApp.Api` — API startup, controllers, configuration
- `src/MyApp.Core` — core entities and business services
- `src/MyApp.Infrastructure` — EF Core DbContext and repositories
- `tests/MyApp.Tests` — unit tests
- `deploy/linux/myapp.service` — systemd service template
- `deploy/nginx/myapp.conf` — Nginx site config template
- `.github/workflows/deploy.yml` — CI/CD pipeline

## Local commands

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/MyApp.Api
```
