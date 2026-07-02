import type { AccessProfile } from "../types/rbac";

export type WorkspaceKey = "warehouse" | "finance" | "management" | "security";

export type WorkspaceNavigationItem = {
  label: string;
  href: string;
  workspace: WorkspaceKey;
  permission?: string;
};

export const workspaceNavigation: WorkspaceNavigationItem[] = [
  { label: "Warehouse", href: "/warehouse", workspace: "warehouse", permission: "Warehouse.View" },
  { label: "Receipts", href: "/warehouse/receipts", workspace: "warehouse", permission: "Warehouse.View" },
  { label: "Finance", href: "/financial", workspace: "finance", permission: "Finance.View" },
  { label: "Invoices", href: "/financial/invoices", workspace: "finance", permission: "Finance.View" },
  { label: "Management", href: "/management-dashboard", workspace: "management", permission: "Reports.View" },
  { label: "Security", href: "/admin/roles-management", workspace: "security", permission: "Security.Manage" },
];

export function resolveWorkspace(profile: AccessProfile): WorkspaceKey {
  if (profile.permissions.includes("Warehouse.View")) return "warehouse";
  if (profile.permissions.includes("Finance.View")) return "finance";
  if (profile.permissions.includes("Security.Manage")) return "security";
  return "management";
}

export function getWorkspaceNavigation(profile: AccessProfile) {
  const workspace = resolveWorkspace(profile);
  return workspaceNavigation.filter((item) =>
    item.workspace === workspace && (!item.permission || profile.permissions.includes(item.permission)),
  );
}
