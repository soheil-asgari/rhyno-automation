namespace OfficeAutomation.Services.Security
{
    public interface ICurrentDataAccessScope
    {
        string? UserId { get; }
        string? TenantId { get; }
        int? DepartmentId { get; }
        bool HasGlobalAccess { get; }
        bool IsAuthenticated { get; }
        bool IsInitialized { get; }
        void Initialize(string? userId, PermissionAccessProfile? profile);
    }

    public sealed class CurrentDataAccessScope : ICurrentDataAccessScope
    {
        public string? UserId { get; private set; }
        public string? TenantId { get; private set; }
        public int? DepartmentId { get; private set; }
        public bool HasGlobalAccess { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public bool IsInitialized { get; private set; }

        public void Initialize(string? userId, PermissionAccessProfile? profile)
        {
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
            IsAuthenticated = !string.IsNullOrWhiteSpace(UserId);
            DepartmentId = profile?.DepartmentId;
            HasGlobalAccess = profile?.HasGlobalAccess == true;
            IsInitialized = true;
        }

        public void SetTenant(string? tenantId)
        {
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        }
    }
}
