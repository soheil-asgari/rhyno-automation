import { RolesManagementDashboard } from "../../../components/admin/roles-management-dashboard";
import { fetchRolesManagementOverview } from "../../../lib/rbac-api";

export default async function RolesManagementPage() {
  const overview = await fetchRolesManagementOverview();
  return <RolesManagementDashboard initialData={overview} />;
}
