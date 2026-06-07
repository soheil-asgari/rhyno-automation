import type {
  AuditLogFilterOptions,
  AuditLogFilters,
  AuditLogListItem,
  AuditLogPage,
} from "../types/audit-log";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:5001";

function buildQuery(filters: AuditLogFilters) {
  const params = new URLSearchParams();

  Object.entries(filters).forEach(([key, value]) => {
    if (value && value.trim().length > 0) {
      params.set(key, value);
    }
  });

  return params.toString();
}

async function getJson<T>(path: string) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    cache: "no-store",
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Audit API request failed: ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function fetchAuditLogs(filters: AuditLogFilters) {
  const query = buildQuery(filters);
  return getJson<AuditLogPage>(`/api/admin/audit-logs${query ? `?${query}` : ""}`);
}

export async function fetchAuditLogFilterOptions() {
  return getJson<AuditLogFilterOptions>("/api/admin/audit-logs/filter-options");
}

export async function fetchAllAuditLogs(filters: AuditLogFilters) {
  const pageSize = 200;
  const baseFilters = {
    ...filters,
    pageSize: String(pageSize),
  };

  const firstPage = await fetchAuditLogs({
    ...baseFilters,
    page: "1",
  });

  const totalPages = Math.max(1, Math.ceil(firstPage.totalCount / pageSize));
  if (totalPages === 1) {
    return firstPage.items;
  }

  const remainingPages = await Promise.all(
    Array.from({ length: totalPages - 1 }, (_, index) =>
      fetchAuditLogs({
        ...baseFilters,
        page: String(index + 2),
      }),
    ),
  );

  return [firstPage, ...remainingPages].flatMap((page): AuditLogListItem[] => page.items);
}
