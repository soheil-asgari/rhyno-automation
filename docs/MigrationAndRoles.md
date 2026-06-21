# Migration & Roles

## Migration flow

1. Update the connection string.
2. Run:

```bash
dotnet ef migrations add <Name>
dotnet ef database update
```

3. Start the application once to seed core permissions and bootstrap roles.

## Bootstrap roles

The app seeds these roles:

- `Admin`
- `FinanceManager`
- `WarehouseManager`
- `HrManager`

The `Admin` role receives global data access and all core permissions.

## Permission model

Permissions are cataloged in `Services/Security/PermissionCatalog.cs`.
Role assignments are stored in `RolePermission`.
