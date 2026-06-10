"use client";

import type { ReactNode } from "react";
import { useDeferredValue, useEffect, useMemo, useState, useTransition } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { fetchAllAuditLogs } from "../../lib/audit-log-api";
import type {
  AuditLogDiffRow,
  AuditLogFilterOptions,
  AuditLogFilters,
  AuditLogListItem,
  AuditLogPage,
} from "../../types/audit-log";

type Props = {
  initialData: AuditLogPage;
  filterOptions: AuditLogFilterOptions;
  initialFilters: AuditLogFilters;
};

type ActionTone = "create" | "update" | "delete" | "default";

type SummaryCardProps = {
  title: string;
  value: string;
  hint: string;
  tone: ActionTone;
};

const tehranFormatter = new Intl.DateTimeFormat("fa-IR-u-ca-persian", {
  dateStyle: "long",
  timeStyle: "short",
  timeZone: "Asia/Tehran",
});

const actionMap: Record<string, { label: string; tone: ActionTone; ring: string; badge: string }> = {
  Create: {
    label: "ثبت جدید",
    tone: "create",
    ring: "from-emerald-500/20 via-emerald-400/10 to-transparent",
    badge: "bg-emerald-100 text-emerald-700 ring-1 ring-emerald-200",
  },
  Update: {
    label: "ویرایش",
    tone: "update",
    ring: "from-amber-500/20 via-amber-400/10 to-transparent",
    badge: "bg-amber-100 text-amber-700 ring-1 ring-amber-200",
  },
  Delete: {
    label: "حذف",
    tone: "delete",
    ring: "from-rose-500/20 via-rose-400/10 to-transparent",
    badge: "bg-rose-100 text-rose-700 ring-1 ring-rose-200",
  },
};

function formatDateTime(value: string) {
  return tehranFormatter.format(new Date(value));
}

function normalizeInputDate(value: string | undefined) {
  if (!value) {
    return "";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "";
  }

  const timezoneOffset = parsed.getTimezoneOffset() * 60_000;
  return new Date(parsed.getTime() - timezoneOffset).toISOString().slice(0, 16);
}

function toApiDate(value: string) {
  if (!value) {
    return "";
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? "" : parsed.toISOString();
}

function parseValue(raw: string | null) {
  if (!raw || raw.trim().length === 0) {
    return null;
  }

  try {
    return JSON.parse(raw) as unknown;
  } catch {
    return raw;
  }
}

function flattenRecord(value: unknown, prefix = ""): Record<string, string> {
  if (value === null || value === undefined) {
    return prefix ? { [prefix]: "-" } : {};
  }

  if (typeof value !== "object") {
    return prefix ? { [prefix]: String(value) } : { value: String(value) };
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return prefix ? { [prefix]: "[]" } : {};
    }

    return value.reduce<Record<string, string>>((accumulator, item, index) => {
      Object.assign(accumulator, flattenRecord(item, `${prefix}[${index}]`));
      return accumulator;
    }, {});
  }

  return Object.entries(value as Record<string, unknown>).reduce<Record<string, string>>((accumulator, [key, nested]) => {
    const nextKey = prefix ? `${prefix}.${key}` : key;
    const nestedMap = flattenRecord(nested, nextKey);
    if (Object.keys(nestedMap).length === 0) {
      accumulator[nextKey] = "-";
    } else {
      Object.assign(accumulator, nestedMap);
    }

    return accumulator;
  }, {});
}

function buildDiffRows(log: AuditLogListItem): AuditLogDiffRow[] {
  const oldValues = flattenRecord(parseValue(log.oldValues));
  const newValues = flattenRecord(parseValue(log.newValues));
  const keys = Array.from(new Set([...Object.keys(oldValues), ...Object.keys(newValues)]));

  if (keys.length === 0) {
    return [];
  }

  return keys
    .sort((left, right) => left.localeCompare(right, "fa"))
    .map((key) => {
      const oldValue = oldValues[key] ?? "-";
      const newValue = newValues[key] ?? "-";

      return {
        key,
        oldValue,
        newValue,
        changed: oldValue !== newValue,
      };
    });
}

function actionMeta(action: string) {
  return actionMap[action] ?? {
    label: action,
    tone: "default" as const,
    ring: "from-slate-500/20 via-slate-400/10 to-transparent",
    badge: "bg-slate-100 text-slate-700 ring-1 ring-slate-200",
  };
}

function stringifyForCsv(value: string | null) {
  if (!value) {
    return "";
  }

  const parsed = parseValue(value);
  return typeof parsed === "string" ? parsed : JSON.stringify(parsed, null, 2);
}

function downloadCsv(rows: AuditLogListItem[], fileName: string) {
  const header = [
    "شناسه",
    "کاربر",
    "شناسه کاربر",
    "عملیات",
    "بخش/جدول",
    "زمان",
    "ستون های متاثر",
    "مقادیر قبلی",
    "مقادیر جدید",
    "IP",
    "User Agent",
  ];

  const csvRows = rows.map((item) => [
    item.id,
    item.userDisplayName ?? "",
    item.userId ?? "",
    actionMeta(item.action).label,
    item.tableName,
    formatDateTime(item.dateTime),
    item.affectedColumns ?? "",
    stringifyForCsv(item.oldValues),
    stringifyForCsv(item.newValues),
    item.userIP ?? "",
    item.userAgent ?? "",
  ]);

  const escapeCell = (cell: unknown) => `"${String(cell ?? "").replace(/"/g, "\"\"")}"`;
  const content = [header, ...csvRows].map((row) => row.map(escapeCell).join(",")).join("\n");
  const blob = new Blob(["\uFEFF", content], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function SummaryCard({ title, value, hint, tone }: SummaryCardProps) {
  const toneClasses: Record<ActionTone, string> = {
    create: "border-emerald-200 bg-emerald-50/80 text-emerald-900",
    update: "border-amber-200 bg-amber-50/80 text-amber-900",
    delete: "border-rose-200 bg-rose-50/80 text-rose-900",
    default: "border-slate-200 bg-white/90 text-slate-900",
  };

  return (
    <div className={`rounded-none border p-3 shadow-none ${toneClasses[tone]}`}>
      <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{title}</div>
      <div className="mt-1 text-2xl font-black tracking-tight">{value}</div>
      <div className="mt-1 text-[11px] text-slate-500">{hint}</div>
    </div>
  );
}

function FilterField({
  label,
  children,
  hint,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
}) {
  return (
    <label className="space-y-2">
      <span className="block text-xs font-semibold text-slate-700">{label}</span>
      {children}
      {hint ? <span className="block text-[11px] text-slate-500">{hint}</span> : null}
    </label>
  );
}

function PaginationButton({
  disabled,
  children,
  onClick,
}: {
  disabled?: boolean;
  children: ReactNode;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      className="rounded-none border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-45"
      disabled={disabled}
      onClick={onClick}
    >
      {children}
    </button>
  );
}

function ChangesModal({
  log,
  onClose,
}: {
  log: AuditLogListItem | null;
  onClose: () => void;
}) {
  const rows = useMemo(() => (log ? buildDiffRows(log) : []), [log]);

  useEffect(() => {
    if (!log) {
      return;
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
      }
    }

    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleEscape);

    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", handleEscape);
    };
  }, [log, onClose]);

  if (!log) {
    return null;
  }

  const meta = actionMeta(log.action);
  const affectedColumns = log.affectedColumns
    ?.split(",")
    .map((column) => column.trim())
    .filter(Boolean) ?? [];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/55 p-4" onClick={onClose}>
      <div
        className="max-h-[88vh] w-full max-w-6xl overflow-hidden rounded-none bg-white shadow-2xl shadow-slate-900/25"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="border-b border-slate-200 bg-slate-50/80 px-4 py-3">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className={`inline-flex rounded-none px-2 py-0.5 text-[11px] font-bold ${meta.badge}`}>{meta.label}</span>
                <span className="rounded-none bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600">{log.tableName}</span>
              </div>
              <h3 className="text-base font-black text-slate-900">مشاهده تغییرات</h3>
              <p className="text-xs text-slate-500">
                {log.userDisplayName ?? log.userId ?? "سیستم"} در {formatDateTime(log.dateTime)}
              </p>
            </div>

            <button
              type="button"
              className="inline-flex h-8 items-center justify-center rounded-none border border-slate-200 px-3 text-xs font-semibold text-slate-700 transition hover:bg-white"
              onClick={onClose}
            >
              بستن
            </button>
          </div>
        </div>

        <div className="overflow-y-auto px-4 py-4">
          <div className="grid gap-3 lg:grid-cols-3">
            <div className="rounded-none border border-slate-200 bg-slate-50 p-3">
              <div className="text-xs font-bold uppercase tracking-[0.2em] text-slate-500">کاربر</div>
              <div className="mt-2 text-sm font-semibold text-slate-900">{log.userDisplayName ?? "کاربر نامشخص"}</div>
              <div className="mt-1 break-all text-xs text-slate-500">{log.userId ?? "-"}</div>
            </div>
            <div className="rounded-none border border-slate-200 bg-slate-50 p-3">
              <div className="text-xs font-bold uppercase tracking-[0.2em] text-slate-500">شبکه</div>
              <div className="mt-2 text-sm font-semibold text-slate-900" dir="ltr">
                {log.userIP ?? "-"}
              </div>
              <div className="mt-1 text-xs text-slate-500">{log.userAgent ?? "-"}</div>
            </div>
            <div className="rounded-none border border-slate-200 bg-slate-50 p-3">
              <div className="text-xs font-bold uppercase tracking-[0.2em] text-slate-500">ستون های متاثر</div>
              <div className="mt-3 flex flex-wrap gap-2">
                {affectedColumns.length > 0 ? (
                  affectedColumns.map((column) => (
                    <span key={column} className="rounded-none bg-white px-2 py-0.5 text-[11px] font-medium text-slate-700 ring-1 ring-slate-200">
                      {column}
                    </span>
                  ))
                ) : (
                  <span className="text-sm text-slate-500">موردی ثبت نشده است.</span>
                )}
              </div>
            </div>
          </div>

          <div className="mt-4 rounded-none border border-slate-200">
            <div className="hidden grid-cols-[minmax(180px,1fr)_minmax(220px,1fr)_minmax(220px,1fr)] border-b border-slate-200 bg-slate-50 px-4 py-2 text-xs font-bold text-slate-700 md:grid">
              <div>فیلد</div>
              <div>مقدار قبلی</div>
              <div>مقدار جدید</div>
            </div>

            {rows.length > 0 ? (
              <div className="divide-y divide-slate-200">
                {rows.map((row) => (
                  <div
                    key={row.key}
                    className={`grid gap-2 px-4 py-2 md:grid-cols-[minmax(180px,1fr)_minmax(220px,1fr)_minmax(220px,1fr)] ${
                      row.changed ? "bg-white" : "bg-slate-50/60"
                    }`}
                  >
                    <div className="space-y-1">
                      <div className="text-xs font-bold text-slate-500 md:hidden">فیلد</div>
                      <div className="break-words font-semibold text-slate-900">{row.key}</div>
                    </div>
                    <div className="space-y-1">
                      <div className="text-xs font-bold text-slate-500 md:hidden">مقدار قبلی</div>
                      <div
                        className={`min-h-12 rounded-none border px-2 py-1.5 text-xs ${
                          row.changed
                            ? "border-rose-200 bg-rose-50/70 text-rose-900"
                            : "border-slate-200 bg-white text-slate-600"
                        }`}
                      >
                        <pre className="whitespace-pre-wrap break-words font-sans">{row.oldValue}</pre>
                      </div>
                    </div>
                    <div className="space-y-1">
                      <div className="text-xs font-bold text-slate-500 md:hidden">مقدار جدید</div>
                      <div
                        className={`min-h-12 rounded-none border px-2 py-1.5 text-xs ${
                          row.changed
                            ? "border-emerald-200 bg-emerald-50/70 text-emerald-900"
                            : "border-slate-200 bg-white text-slate-600"
                        }`}
                      >
                        <pre className="whitespace-pre-wrap break-words font-sans">{row.newValue}</pre>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="px-5 py-12 text-center text-sm text-slate-500">
                برای این رویداد جزئیات قابل مقایسه‌ای ثبت نشده است.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export function AuditLogDashboard({ initialData, filterOptions, initialFilters }: Props) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [isPending, startTransition] = useTransition();
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState("");
  const [isFiltersOpen, setIsFiltersOpen] = useState(
    Boolean(initialFilters.userId || initialFilters.action || initialFilters.tableName || initialFilters.from || initialFilters.to),
  );
  const [selectedLog, setSelectedLog] = useState<AuditLogListItem | null>(null);
  const [filters, setFilters] = useState<AuditLogFilters>({
    userId: initialFilters.userId ?? "",
    action: initialFilters.action ?? "",
    tableName: initialFilters.tableName ?? "",
    from: normalizeInputDate(initialFilters.from),
    to: normalizeInputDate(initialFilters.to),
    page: initialFilters.page ?? String(initialData.page),
    pageSize: initialFilters.pageSize ?? String(initialData.pageSize),
  });

  const deferredItems = useDeferredValue(initialData.items);
  const totalPages = Math.max(1, Math.ceil(initialData.totalCount / initialData.pageSize));
  const activePage = Number(filters.page ?? initialData.page);
  const visibleRangeStart = initialData.totalCount === 0 ? 0 : (initialData.page - 1) * initialData.pageSize + 1;
  const visibleRangeEnd = Math.min(initialData.page * initialData.pageSize, initialData.totalCount);

  const summary = useMemo(() => {
    const counts = deferredItems.reduce(
      (accumulator, item) => {
        if (item.action === "Create") {
          accumulator.create += 1;
        } else if (item.action === "Update") {
          accumulator.update += 1;
        } else if (item.action === "Delete") {
          accumulator.delete += 1;
        }

        return accumulator;
      },
      { create: 0, update: 0, delete: 0 },
    );

    return counts;
  }, [deferredItems]);

  function pushFilters(next: AuditLogFilters) {
    const params = new URLSearchParams(searchParams.toString());
    const normalized: AuditLogFilters = {
      ...next,
      from: toApiDate(next.from ?? ""),
      to: toApiDate(next.to ?? ""),
    };

    Object.entries(normalized).forEach(([key, value]) => {
      if (value && value.trim().length > 0) {
        params.set(key, value);
      } else {
        params.delete(key);
      }
    });

    startTransition(() => {
      router.replace(params.toString() ? `${pathname}?${params.toString()}` : pathname);
    });
  }

  function updateFilter(key: keyof AuditLogFilters, value: string) {
    setFilters((current) => ({
      ...current,
      [key]: value,
      page: "1",
    }));
  }

  function handleApplyFilters() {
    pushFilters({
      ...filters,
      page: "1",
    });
  }

  function handleClearFilters() {
    const cleared = {
      userId: "",
      action: "",
      tableName: "",
      from: "",
      to: "",
      page: "1",
      pageSize: filters.pageSize ?? String(initialData.pageSize),
    };

    setFilters(cleared);
    pushFilters(cleared);
  }

  function goToPage(page: number) {
    const next = {
      ...filters,
      page: String(page),
    };

    setFilters(next);
    pushFilters(next);
  }

  function handlePageSizeChange(value: string) {
    const next = {
      ...filters,
      pageSize: value,
      page: "1",
    };

    setFilters(next);
    pushFilters(next);
  }

  function handleExport() {
    setExportError("");
    setIsExporting(true);

    void (async () => {
      try {
        const exportItems = await fetchAllAuditLogs({
          userId: filters.userId || undefined,
          action: filters.action || undefined,
          tableName: filters.tableName || undefined,
          from: toApiDate(filters.from ?? "") || undefined,
          to: toApiDate(filters.to ?? "") || undefined,
        });

        const stamp = new Date().toISOString().slice(0, 10);
        downloadCsv(exportItems, `audit-logs-${stamp}.csv`);
      } catch (error) {
        setExportError(error instanceof Error ? error.message : "خروجی فایل با خطا مواجه شد.");
      } finally {
        setIsExporting(false);
      }
    })();
  }

  return (
    <div
      className="min-h-screen bg-slate-50 p-3 sm:p-4"
      dir="rtl"
      style={{ fontFamily: "Vazirmatn, Yekan Bakh, Yekan, sans-serif" }}
    >
      <div className="mx-auto max-w-7xl space-y-3">
        {exportError ? (
          <div className="rounded-none border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-medium text-rose-700">
            {exportError}
          </div>
        ) : null}

        <section className="overflow-hidden rounded-none border border-slate-200 bg-white shadow-none">
          <div className="relative px-4 py-4 sm:px-5">
            <div className="absolute inset-x-0 top-0 h-px bg-slate-900" />
            <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
              <div className="space-y-2">
                <div className="inline-flex items-center gap-2 rounded-none bg-slate-900 px-3 py-1.5 text-[11px] font-bold text-white shadow-none">
                  <span className="h-2 w-2 rounded-none bg-emerald-400" />
                  داشبورد لاگ های حسابرسی سیستم
                </div>
                <div>
                  <h1 className="text-xl font-black tracking-tight text-slate-950 sm:text-2xl">نظارت دقیق بر عملیات کاربران</h1>
                  <p className="mt-1 max-w-3xl text-xs leading-6 text-slate-600">
                    رخدادهای ثبت، ویرایش و حذف را با فیلترهای پیشرفته، نمایش خوانای تغییرات و خروجی اکسل بررسی کنید.
                  </p>
                </div>
              </div>

              <div className="grid gap-3 rounded-none border border-slate-200 bg-slate-50/80 p-3 sm:grid-cols-2">
                <div>
                  <div className="text-xs font-bold uppercase tracking-[0.24em] text-slate-400">نمایش</div>
                  <div className="mt-2 text-sm font-semibold text-slate-800">
                    {visibleRangeStart} تا {visibleRangeEnd} از {initialData.totalCount}
                  </div>
                  <div className="mt-1 text-xs text-slate-500">بر اساس فیلترهای فعال</div>
                </div>
                <div>
                  <div className="text-xs font-bold uppercase tracking-[0.24em] text-slate-400">آخرین صفحه</div>
                  <div className="mt-2 text-sm font-semibold text-slate-800">
                    صفحه {initialData.page} از {totalPages}
                  </div>
                  <div className="mt-1 text-xs text-slate-500">اندازه صفحه {initialData.pageSize} ردیف</div>
                </div>
              </div>
            </div>

            <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              <SummaryCard title="کل عملیات" value={initialData.totalCount.toLocaleString("fa-IR")} hint="تعداد کل لاگ های منطبق با فیلتر" tone="default" />
              <SummaryCard title="ثبت های جدید" value={summary.create.toLocaleString("fa-IR")} hint="تعداد در صفحه جاری" tone="create" />
              <SummaryCard title="ویرایش ها" value={summary.update.toLocaleString("fa-IR")} hint="تعداد در صفحه جاری" tone="update" />
              <SummaryCard title="حذف ها" value={summary.delete.toLocaleString("fa-IR")} hint="تعداد در صفحه جاری" tone="delete" />
            </div>
          </div>
        </section>

        <section className="overflow-hidden rounded-none border border-slate-200 bg-white shadow-none">
          <div className="border-b border-slate-200 px-4 py-3 sm:px-5">
            <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
              <button
                type="button"
                className="inline-flex items-center justify-between rounded-none border border-slate-200 bg-slate-50 px-3 py-2 text-right text-xs font-bold text-slate-800 transition hover:bg-slate-100 lg:min-w-[260px]"
                onClick={() => setIsFiltersOpen((current) => !current)}
              >
                <span>فیلترهای پیشرفته</span>
                <span className={`text-xl transition ${isFiltersOpen ? "rotate-180" : ""}`}>⌄</span>
              </button>

              <div className="flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  className="inline-flex items-center gap-2 rounded-none bg-slate-900 px-3 py-2 text-xs font-bold text-white shadow-none transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
                  disabled={isExporting}
                  onClick={handleExport}
                >
                  <span>خروجی اکسل</span>
                  <span className="text-base">{isExporting ? "…" : "⬇"}</span>
                </button>
              </div>
            </div>
          </div>

          <div className={`grid transition-all duration-300 ${isFiltersOpen ? "grid-rows-[1fr]" : "grid-rows-[0fr]"}`}>
            <div className="overflow-hidden">
              <div className="border-b border-slate-200 bg-slate-50/70 px-4 py-3 sm:px-5">
                <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-5">
                  <FilterField label="کاربر">
                    <select
                      className="h-9 w-full rounded-none border border-slate-200 bg-white px-3 text-xs text-slate-800 outline-none ring-0 transition focus:border-sky-400"
                      value={filters.userId ?? ""}
                      onChange={(event) => updateFilter("userId", event.target.value)}
                    >
                      <option value="">همه کاربران</option>
                      {filterOptions.users.map((user) => (
                        <option key={user.value} value={user.value}>
                          {user.label}
                        </option>
                      ))}
                    </select>
                  </FilterField>

                  <FilterField label="نوع عملیات">
                    <select
                      className="h-9 w-full rounded-none border border-slate-200 bg-white px-3 text-xs text-slate-800 outline-none transition focus:border-sky-400"
                      value={filters.action ?? ""}
                      onChange={(event) => updateFilter("action", event.target.value)}
                    >
                      <option value="">همه عملیات</option>
                      {filterOptions.actions.map((action) => (
                        <option key={action} value={action}>
                          {actionMeta(action).label}
                        </option>
                      ))}
                    </select>
                  </FilterField>

                  <FilterField label="بخش یا جدول" hint="قابل انتخاب از لیست یا ورود دستی">
                    <input
                      list="audit-log-table-names"
                      className="h-9 w-full rounded-none border border-slate-200 bg-white px-3 text-xs text-slate-800 outline-none transition focus:border-sky-400"
                      placeholder="مثلا Invoices"
                      value={filters.tableName ?? ""}
                      onChange={(event) => updateFilter("tableName", event.target.value)}
                    />
                    <datalist id="audit-log-table-names">
                      {filterOptions.tableNames.map((tableName) => (
                        <option key={tableName} value={tableName} />
                      ))}
                    </datalist>
                  </FilterField>

                  <FilterField label="از تاریخ" hint="سازگار با تاریخ و زمان API">
                    <input
                      type="datetime-local"
                      lang="fa-IR"
                      dir="ltr"
                      className="h-9 w-full rounded-none border border-slate-200 bg-white px-3 text-xs text-slate-800 outline-none transition focus:border-sky-400"
                      value={filters.from ?? ""}
                      onChange={(event) => updateFilter("from", event.target.value)}
                    />
                  </FilterField>

                  <FilterField label="تا تاریخ" hint="سازگار با تاریخ و زمان API">
                    <input
                      type="datetime-local"
                      lang="fa-IR"
                      dir="ltr"
                      className="h-9 w-full rounded-none border border-slate-200 bg-white px-3 text-xs text-slate-800 outline-none transition focus:border-sky-400"
                      value={filters.to ?? ""}
                      onChange={(event) => updateFilter("to", event.target.value)}
                    />
                  </FilterField>
                </div>

                <div className="mt-3 flex flex-wrap gap-2">
                  <button
                    type="button"
                    className="rounded-none bg-sky-600 px-4 py-2 text-xs font-bold text-white shadow-none transition hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-60"
                    disabled={isPending}
                    onClick={handleApplyFilters}
                  >
                    {isPending ? "در حال اعمال..." : "اعمال فیلترها"}
                  </button>
                  <button
                    type="button"
                    className="rounded-none border border-slate-200 bg-white px-4 py-2 text-xs font-bold text-slate-700 transition hover:bg-slate-50"
                    onClick={handleClearFilters}
                  >
                    پاک کردن فیلترها
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div className="px-3 py-3 sm:px-4 lg:px-5">
            <div className="overflow-hidden rounded-none border border-slate-200">
              <div className="overflow-x-auto">
                <table className="min-w-full text-right text-xs">
                  <thead className="bg-slate-900 text-white">
                    <tr>
                      <th className="px-3 py-2 font-bold">زمان</th>
                      <th className="px-3 py-2 font-bold">کاربر</th>
                      <th className="px-3 py-2 font-bold">عملیات</th>
                      <th className="px-3 py-2 font-bold">بخش</th>
                      <th className="px-3 py-2 font-bold">ستون های متاثر</th>
                      <th className="px-3 py-2 font-bold">IP</th>
                      <th className="px-3 py-2 font-bold">جزئیات</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 bg-white">
                    {deferredItems.map((item, index) => {
                      const meta = actionMeta(item.action);
                      const affectedColumns = item.affectedColumns
                        ?.split(",")
                        .map((column) => column.trim())
                        .filter(Boolean) ?? [];

                      return (
                        <tr key={item.id} className={index % 2 === 0 ? "bg-white" : "bg-slate-50/60"}>
                          <td className="px-3 py-2 align-top text-slate-700">
                            <div className="font-semibold text-slate-900">{formatDateTime(item.dateTime)}</div>
                            <div className="mt-1 text-xs text-slate-500">{item.id.slice(0, 8)}</div>
                          </td>
                          <td className="px-3 py-2 align-top text-slate-700">
                            <div className="font-semibold text-slate-900">{item.userDisplayName ?? "سیستم"}</div>
                            <div className="mt-1 text-xs text-slate-500" dir="ltr">
                              {item.userId ?? "-"}
                            </div>
                          </td>
                          <td className="px-3 py-2 align-top">
                            <div className={`inline-flex rounded-none px-2 py-0.5 text-[11px] font-bold ${meta.badge}`}>{meta.label}</div>
                          </td>
                          <td className="px-3 py-2 align-top">
                            <div className={`rounded-none bg-gradient-to-l p-[1px] ${meta.ring}`}>
                              <div className="rounded-none bg-white px-2 py-1 font-semibold text-slate-900">{item.tableName}</div>
                            </div>
                          </td>
                          <td className="px-3 py-2 align-top">
                            <div className="flex max-w-xs flex-wrap gap-1">
                              {affectedColumns.length > 0 ? (
                                affectedColumns.map((column) => (
                                  <span
                                    key={column}
                                    className="rounded-none bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-700"
                                  >
                                    {column}
                                  </span>
                                ))
                              ) : (
                                <span className="text-xs text-slate-400">ثبت نشده</span>
                              )}
                            </div>
                          </td>
                          <td className="px-3 py-2 align-top text-[11px] text-slate-600" dir="ltr">
                            {item.userIP ?? "-"}
                          </td>
                          <td className="px-3 py-2 align-top">
                            <button
                              type="button"
                              className="rounded-none border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 transition hover:border-sky-300 hover:bg-sky-50 hover:text-sky-700"
                              onClick={() => setSelectedLog(item)}
                            >
                              مشاهده تغییرات
                            </button>
                          </td>
                        </tr>
                      );
                    })}

                    {deferredItems.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="px-4 py-10 text-center">
                          <div className="mx-auto max-w-md space-y-3">
                            <div className="text-lg font-bold text-slate-900">لاگی برای نمایش پیدا نشد</div>
                            <p className="text-sm leading-7 text-slate-500">
                              فیلترها را تغییر دهید یا بازه زمانی را گسترده‌تر انتخاب کنید تا رخدادهای ثبت شده نمایش داده شوند.
                            </p>
                          </div>
                        </td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="mt-3 flex flex-col gap-3 rounded-none border border-slate-200 bg-slate-50/70 px-3 py-2 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-3">
                <div className="text-xs text-slate-600">
                  نمایش <span className="font-bold text-slate-900">{visibleRangeStart}</span> تا{" "}
                  <span className="font-bold text-slate-900">{visibleRangeEnd}</span> از{" "}
                  <span className="font-bold text-slate-900">{initialData.totalCount}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-medium text-slate-600">تعداد در هر صفحه</span>
                  <select
                    className="h-8 rounded-none border border-slate-200 bg-white px-3 text-xs text-slate-800 outline-none transition focus:border-sky-400"
                    value={filters.pageSize ?? String(initialData.pageSize)}
                    onChange={(event) => handlePageSizeChange(event.target.value)}
                  >
                    {[25, 50, 100, 200].map((size) => (
                      <option key={size} value={String(size)}>
                        {size}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <PaginationButton disabled={activePage <= 1 || isPending} onClick={() => goToPage(1)}>
                  اولین
                </PaginationButton>
                <PaginationButton disabled={activePage <= 1 || isPending} onClick={() => goToPage(activePage - 1)}>
                  قبلی
                </PaginationButton>
                <div className="rounded-none bg-white px-3 py-1.5 text-xs font-bold text-slate-800 ring-1 ring-slate-200">
                  صفحه {initialData.page} از {totalPages}
                </div>
                <PaginationButton disabled={activePage >= totalPages || isPending} onClick={() => goToPage(activePage + 1)}>
                  بعدی
                </PaginationButton>
                <PaginationButton disabled={activePage >= totalPages || isPending} onClick={() => goToPage(totalPages)}>
                  آخرین
                </PaginationButton>
              </div>
            </div>
          </div>
        </section>
      </div>

      <ChangesModal log={selectedLog} onClose={() => setSelectedLog(null)} />
    </div>
  );
}
