import type {
  AccessProfile,
  CreateRolePayload,
  RoleItem,
  RoleUserItem,
  RolesManagementOverview,
  UpdateRolePayload,
  UpdateRolePermissionsPayload,
  UpdateUserRolesPayload,
} from "../types/rbac";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:5001";

async function request<T>(path: string, init?: RequestInit) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    cache: "no-store",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(body || `RBAC API request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function fetchRolesManagementOverview() {
  return request<RolesManagementOverview>("/api/admin/roles-management/overview");
}

export function createRole(payload: CreateRolePayload) {
  return request<RoleItem>("/api/admin/roles-management/roles", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function updateRole(roleId: string, payload: UpdateRolePayload) {
  return request<RoleItem>(`/api/admin/roles-management/roles/${roleId}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

export function deleteRole(roleId: string) {
  return request<void>(`/api/admin/roles-management/roles/${roleId}`, {
    method: "DELETE",
  });
}

export function updateRolePermissions(roleId: string, payload: UpdateRolePermissionsPayload) {
  return request<RoleItem>(`/api/admin/roles-management/roles/${roleId}/permissions`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

export function updateUserRoles(userId: string, payload: UpdateUserRolesPayload) {
  return request<RoleUserItem>(`/api/admin/roles-management/users/${userId}/roles`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

export function fetchCurrentAccessProfile() {
  return request<AccessProfile>("/api/admin/roles-management/me/access-profile");
}
