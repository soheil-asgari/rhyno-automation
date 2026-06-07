"use client";

import { useMemo, useState, useTransition } from "react";
import {
  createRole,
  deleteRole,
  updateRole,
  updateRolePermissions,
  updateUserRoles,
} from "../../lib/rbac-api";
import type { RolesManagementOverview } from "../../types/rbac";

type Props = {
  initialData: RolesManagementOverview;
};

type ToastState = {
  tone: "success" | "error";
  message: string;
} | null;

export function RolesManagementDashboard({ initialData }: Props) {
  const [isPending, startTransition] = useTransition();
  const [data, setData] = useState(initialData);
  const [selectedRoleId, setSelectedRoleId] = useState(initialData.roles[0]?.id ?? "");
  const [toast, setToast] = useState<ToastState>(null);
  const [roleForm, setRoleForm] = useState({
    name: "",
    description: "",
    dataAccessScope: initialData.dataAccessScopes[0]?.value ?? "Department",
  });

  const selectedRole = useMemo(
    () => data.roles.find((role) => role.id === selectedRoleId) ?? data.roles[0] ?? null,
    [data.roles, selectedRoleId],
  );

  const groupedPermissions = useMemo(() => {
    return data.permissions.reduce<Record<string, typeof data.permissions>>((accumulator, permission) => {
      accumulator[permission.category] ??= [];
      accumulator[permission.category].push(permission);
      return accumulator;
    }, {});
  }, [data.permissions]);

  function showToast(tone: "success" | "error", message: string) {
    setToast({ tone, message });
    window.setTimeout(() => setToast(null), 3000);
  }

  function syncRole(updatedRoleId: string, updater: (current: RolesManagementOverview) => RolesManagementOverview) {
    setData((current) => updater(current));
    setSelectedRoleId(updatedRoleId);
  }

  async function handleCreateRole() {
    startTransition(async () => {
      try {
        const role = await createRole(roleForm);
        syncRole(role.id, (current) => ({
          ...current,
          roles: [...current.roles, role].sort((left, right) => left.name.localeCompare(right.name)),
        }));
        setRoleForm({
          name: "",
          description: "",
          dataAccessScope: roleForm.dataAccessScope,
        });
        showToast("success", "Role created.");
      } catch (error) {
        showToast("error", error instanceof Error ? error.message : "Role creation failed.");
      }
    });
  }

  async function handleSaveRole() {
    if (!selectedRole) {
      return;
    }

    startTransition(async () => {
      try {
        const updated = await updateRole(selectedRole.id, {
          name: selectedRole.name,
          description: selectedRole.description ?? "",
          dataAccessScope: selectedRole.dataAccessScope,
        });

        syncRole(updated.id, (current) => ({
          ...current,
          roles: current.roles.map((role) => (role.id === updated.id ? updated : role)),
        }));
        showToast("success", "Role updated.");
      } catch (error) {
        showToast("error", error instanceof Error ? error.message : "Role update failed.");
      }
    });
  }

  async function handleDeleteRole(roleId: string) {
    startTransition(async () => {
      try {
        await deleteRole(roleId);
        setData((current) => ({
          ...current,
          roles: current.roles.filter((role) => role.id !== roleId),
        }));
        setSelectedRoleId((current) => (current === roleId ? data.roles.find((role) => role.id !== roleId)?.id ?? "" : current));
        showToast("success", "Role deleted.");
      } catch (error) {
        showToast("error", error instanceof Error ? error.message : "Role deletion failed.");
      }
    });
  }

  async function handleTogglePermission(permissionKey: string, checked: boolean) {
    if (!selectedRole) {
      return;
    }

    const nextPermissionKeys = checked
      ? [...selectedRole.permissions, permissionKey]
      : selectedRole.permissions.filter((item) => item !== permissionKey);

    startTransition(async () => {
      try {
        const updated = await updateRolePermissions(selectedRole.id, { permissionKeys: nextPermissionKeys });
        syncRole(updated.id, (current) => ({
          ...current,
          roles: current.roles.map((role) => (role.id === updated.id ? updated : role)),
        }));
        showToast("success", "Permissions updated.");
      } catch (error) {
        showToast("error", error instanceof Error ? error.message : "Permission update failed.");
      }
    });
  }

  async function handleUserRoleChange(userId: string, roleId: string, checked: boolean) {
    const currentUser = data.users.find((user) => user.id === userId);
    if (!currentUser) {
      return;
    }

    const targetRoleIds = data.roles
      .filter((role) => currentUser.roles.includes(role.name))
      .map((role) => role.id);

    const nextRoleIds = checked
      ? Array.from(new Set([...targetRoleIds, roleId]))
      : targetRoleIds.filter((item) => item !== roleId);

    startTransition(async () => {
      try {
        const updatedUser = await updateUserRoles(userId, { roleIds: nextRoleIds });
        setData((current) => ({
          ...(() => {
            const nextUsers = current.users.map((user) => (user.id === updatedUser.id ? updatedUser : user));
            return {
              users: nextUsers,
              roles: current.roles.map((role) => ({
                ...role,
                userCount: nextUsers.filter((user) => user.roles.includes(role.name)).length,
              })),
            };
          })(),
        }));
        showToast("success", "User roles updated.");
      } catch (error) {
        showToast("error", error instanceof Error ? error.message : "User role update failed.");
      }
    });
  }

  return (
    <div className="space-y-6 p-6">
      {toast ? (
        <div
          className={`rounded-xl border px-4 py-3 text-sm ${
            toast.tone === "success"
              ? "border-emerald-200 bg-emerald-50 text-emerald-700"
              : "border-rose-200 bg-rose-50 text-rose-700"
          }`}
        >
          {toast.message}
        </div>
      ) : null}

      <section className="grid gap-6 xl:grid-cols-[360px_minmax(0,1fr)]">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h1 className="text-xl font-semibold text-slate-900">Roles Management</h1>
          <p className="mt-1 text-sm text-slate-500">Create roles and control their data isolation scope from the UI.</p>

          <div className="mt-5 space-y-3">
            <input
              className="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm"
              placeholder="Role name"
              value={roleForm.name}
              onChange={(event) => setRoleForm((current) => ({ ...current, name: event.target.value }))}
            />
            <input
              className="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm"
              placeholder="Description"
              value={roleForm.description}
              onChange={(event) => setRoleForm((current) => ({ ...current, description: event.target.value }))}
            />
            <select
              className="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm"
              value={roleForm.dataAccessScope}
              onChange={(event) => setRoleForm((current) => ({ ...current, dataAccessScope: event.target.value }))}
            >
              {data.dataAccessScopes.map((scope) => (
                <option key={scope.value} value={scope.value}>
                  {scope.label}
                </option>
              ))}
            </select>
            <button
              className="w-full rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
              disabled={isPending || !roleForm.name.trim()}
              onClick={handleCreateRole}
            >
              Create role
            </button>
          </div>

          <div className="mt-6 space-y-2">
            {data.roles.map((role) => (
              <div
                key={role.id}
                className={`flex w-full items-center justify-between rounded-xl border px-3 py-3 text-left ${
                  selectedRole?.id === role.id
                    ? "border-slate-900 bg-slate-900 text-white"
                    : "border-slate-200 bg-white text-slate-900"
                }`}
              >
                <button className="flex-1 text-left" onClick={() => setSelectedRoleId(role.id)}>
                  <div className="text-sm font-semibold">{role.name}</div>
                  <div className={`text-xs ${selectedRole?.id === role.id ? "text-slate-300" : "text-slate-500"}`}>
                    {role.dataAccessScope} • {role.userCount} users
                  </div>
                </button>
                <button
                  className={`rounded-lg px-2 py-1 text-xs ${selectedRole?.id === role.id ? "bg-white/10 text-white" : "bg-rose-50 text-rose-600"}`}
                  onClick={() => handleDeleteRole(role.id)}
                >
                  Delete
                </button>
              </div>
            ))}
          </div>
        </div>

        <div className="space-y-6">
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="text-lg font-semibold text-slate-900">Role Profile</h2>
                <p className="text-sm text-slate-500">Edit role metadata and permission assignments.</p>
              </div>
            </div>

            {selectedRole ? (
              <div className="mt-5 grid gap-4 lg:grid-cols-3">
                <input
                  className="rounded-xl border border-slate-200 px-3 py-2 text-sm"
                  value={selectedRole.name}
                  onChange={(event) =>
                    setData((current) => ({
                      ...current,
                      roles: current.roles.map((role) =>
                        role.id === selectedRole.id ? { ...role, name: event.target.value } : role,
                      ),
                    }))
                  }
                />
                <input
                  className="rounded-xl border border-slate-200 px-3 py-2 text-sm"
                  value={selectedRole.description ?? ""}
                  onChange={(event) =>
                    setData((current) => ({
                      ...current,
                      roles: current.roles.map((role) =>
                        role.id === selectedRole.id ? { ...role, description: event.target.value } : role,
                      ),
                    }))
                  }
                />
                <select
                  className="rounded-xl border border-slate-200 px-3 py-2 text-sm"
                  value={selectedRole.dataAccessScope}
                  onChange={(event) =>
                    setData((current) => ({
                      ...current,
                      roles: current.roles.map((role) =>
                        role.id === selectedRole.id ? { ...role, dataAccessScope: event.target.value } : role,
                      ),
                    }))
                  }
                >
                  {data.dataAccessScopes.map((scope) => (
                    <option key={scope.value} value={scope.value}>
                      {scope.label}
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              <div className="mt-4 rounded-xl border border-dashed border-slate-200 px-4 py-10 text-center text-sm text-slate-500">
                Create a role to start assigning permissions.
              </div>
            )}

            <div className="mt-4">
              <button
                className="rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
                disabled={isPending || !selectedRole}
                onClick={handleSaveRole}
              >
                Save role
              </button>
            </div>
          </section>

          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Permissions Matrix</h2>
            <div className="mt-5 space-y-6">
              {Object.entries(groupedPermissions).map(([category, permissions]) => (
                <div key={category}>
                  <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{category}</h3>
                  <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                    {permissions.map((permission) => {
                      const checked = selectedRole?.permissions.includes(permission.key) ?? false;
                      return (
                        <label
                          key={permission.key}
                          className="flex items-start gap-3 rounded-xl border border-slate-200 px-4 py-3"
                        >
                          <input
                            type="checkbox"
                            className="mt-1 h-4 w-4 rounded border-slate-300 text-slate-900"
                            checked={checked}
                            onChange={(event) => handleTogglePermission(permission.key, event.target.checked)}
                            disabled={!selectedRole || isPending}
                          />
                          <div>
                            <div className="text-sm font-medium text-slate-900">{permission.displayName}</div>
                            <div className="text-xs text-slate-500">{permission.key}</div>
                          </div>
                        </label>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          </section>
        </div>
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">User Role Assignment</h2>
        <div className="mt-5 overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-slate-600">
              <tr>
                <th className="px-4 py-3 font-semibold">User</th>
                <th className="px-4 py-3 font-semibold">Department</th>
                <th className="px-4 py-3 font-semibold">Roles</th>
                <th className="px-4 py-3 font-semibold">Permissions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {data.users.map((user) => (
                <tr key={user.id} className="align-top">
                  <td className="px-4 py-3">
                    <div className="font-medium text-slate-900">{user.displayName}</div>
                    <div className="text-xs text-slate-500">{user.email ?? user.id}</div>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{user.departmentName ?? "-"}</td>
                  <td className="px-4 py-3">
                    <div className="grid gap-2 lg:grid-cols-2">
                      {data.roles.map((role) => (
                        <label key={role.id} className="flex items-center gap-2 text-slate-700">
                          <input
                            type="checkbox"
                            className="h-4 w-4 rounded border-slate-300 text-slate-900"
                            checked={user.roles.includes(role.name)}
                            onChange={(event) => handleUserRoleChange(user.id, role.id, event.target.checked)}
                            disabled={isPending}
                          />
                          <span>{role.name}</span>
                        </label>
                      ))}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-2">
                      {user.permissions.map((permission) => (
                        <span key={permission} className="rounded-full bg-slate-100 px-2.5 py-1 text-xs text-slate-700">
                          {permission}
                        </span>
                      ))}
                      {user.permissions.length === 0 ? <span className="text-slate-400">No permissions</span> : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
