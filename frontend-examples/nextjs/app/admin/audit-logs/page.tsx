import { AuditLogDashboard } from "../../../components/admin/audit-log-dashboard";
import { fetchAuditLogFilterOptions, fetchAuditLogs } from "../../../lib/audit-log-api";
import type { AuditLogFilters } from "../../../types/audit-log";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function toSingle(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function AuditLogsPage({ searchParams }: PageProps) {
  const resolvedParams = await searchParams;

  const filters: AuditLogFilters = {
    userId: toSingle(resolvedParams.userId),
    action: toSingle(resolvedParams.action),
    tableName: toSingle(resolvedParams.tableName),
    from: toSingle(resolvedParams.from),
    to: toSingle(resolvedParams.to),
    page: toSingle(resolvedParams.page),
    pageSize: toSingle(resolvedParams.pageSize),
  };

  const [initialData, filterOptions] = await Promise.all([
    fetchAuditLogs(filters),
    fetchAuditLogFilterOptions(),
  ]);

  return (
    <AuditLogDashboard
      initialData={initialData}
      filterOptions={filterOptions}
      initialFilters={filters}
    />
  );
}
