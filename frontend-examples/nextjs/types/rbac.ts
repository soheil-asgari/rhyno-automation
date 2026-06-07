export type PermissionItem = {
  key: string;
  displayName: string;
  category: string;
  description: string | null;
};

export type RoleItem = {
  id: string;
  name: string;
  description: string | null;
  dataAccessScope: "Global" | "Department" | string;
  permissions: string[];
  userCount: number;
};

export type RoleUserItem = {
  id: string;
  displayName: string;
  email: string | null;
  departmentId: number | null;
  departmentName: string | null;
  roles: string[];
  permissions: string[];
};

export type LookupItem = {
  value: string;
  label: string;
};

export type RolesManagementOverview = {
  roles: RoleItem[];
  permissions: PermissionItem[];
  users: RoleUserItem[];
  dataAccessScopes: LookupItem[];
};

export type AccessProfile = {
  userId: string;
  displayName: string;
  departmentId: number | null;
  hasGlobalAccess: boolean;
  roles: string[];
  permissions: string[];
};

export type CreateRolePayload = {
  name: string;
  description?: string;
  dataAccessScope: string;
};

export type UpdateRolePayload = CreateRolePayload;

export type UpdateRolePermissionsPayload = {
  permissionKeys: string[];
};

export type UpdateUserRolesPayload = {
  roleIds: string[];
};
