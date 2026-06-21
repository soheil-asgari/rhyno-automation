# Installation Guide

## Prerequisites

- .NET SDK 10
- SQL Server 2019+ or LocalDB for development
- IIS, Kestrel, Docker, or another reverse-proxy setup for production

## Configuration

Set these values through environment variables, User Secrets, or deployment-specific config:

- `ConnectionStrings:DefaultConnection`
- `BootstrapAdmin:Email`
- `BootstrapAdmin:Password`
- `OpenAI:ApiKey`

Never commit secrets into `appsettings.json`.

## Database setup

Run migrations on first deployment:

```bash
dotnet ef database update
```

The application also runs startup migration on boot.

## Verify

- App: `/`
- Health check: `/health`
