export type AuditLogListItem = {
  id: string;
  userId: string | null;
  userDisplayName: string | null;
  action: "Create" | "Update" | "Delete" | string;
  tableName: string;
  dateTime: string;
  oldValues: string | null;
  newValues: string | null;
  affectedColumns: string | null;
  userIP: string | null;
  userAgent: string | null;
};

export type AuditLogFilterOption = {
  value: string;
  label: string;
};

export type AuditLogFilterOptions = {
  users: AuditLogFilterOption[];
  actions: string[];
  tableNames: string[];
};

export type AuditLogPage = {
  items: AuditLogListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type AuditLogFilters = {
  userId?: string;
  action?: string;
  tableName?: string;
  from?: string;
  to?: string;
  page?: string;
  pageSize?: string;
};

export type AuditLogDiffRow = {
  key: string;
  oldValue: string;
  newValue: string;
  changed: boolean;
};
