# Multi-Tenancy Isolation

This system no longer treats row-level security as the primary tenancy boundary. `RLS` remains useful for intra-tenant department scoping, but tenant isolation is enforced through dedicated resource descriptors.

## Isolation contract

Each tenant now resolves to a `TenantIsolationDescriptor` with:

- database connection string
- optional schema name for `SchemaPerTenant`
- message queue namespace
- cache prefix and optional Redis database number
- storage root
- log prefix and log root
- settings namespace
- background-job namespace

All tenant-aware infrastructure must resolve resource names through `ITenantIsolationService` instead of falling back to a shared default implicitly.

## Database

- `DatabasePerTenant` uses a dedicated connection string.
- `SchemaPerTenant` reuses the connection string but applies a tenant-specific EF default schema via `TenantDbContextModelCacheKeyFactory`.
- Startup migration/bootstrap should be executed per tenant in production operations if multiple tenant databases/schemas are configured.

## Queues, cache, and storage

- RabbitMQ exchange/routing names are prefixed by tenant queue namespace.
- Cache keys are prefixed by tenant cache namespace.
- File storage paths are rooted under a tenant-specific storage root.

## Tenant settings

`SystemSettings` remains the base shape for the effective setting model, but per-tenant overrides are persisted in `TenantSettings`. This keeps operational settings isolated without duplicating the entire settings table for every tenant.

## Background jobs

`TenantBackgroundJobCoordinator` acquires per-tenant job leases using `TenantBackgroundJobStates`. Outbox publishing and SLA processing run inside isolated tenant scopes and do not share queue names, settings, or locks.
