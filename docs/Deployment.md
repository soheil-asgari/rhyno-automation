# Deployment Guide

## Recommended model

- Build in CI
- Deploy published artifacts only
- Configure secrets outside the repository

## Docker

```bash
docker build -t rhyno-automation .
docker run -p 8080:8080 rhyno-automation
```

## Environment variables

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection`
- `BootstrapAdmin__Email`
- `BootstrapAdmin__Password`
- `OpenAI__ApiKey`

## Operational checks

- `/health`
- login
- one finance workflow
- one warehouse workflow
- audit log entry creation
