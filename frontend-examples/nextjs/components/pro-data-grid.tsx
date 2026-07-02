"use client";

import { KeyboardEvent, ReactNode, useEffect, useMemo, useRef, useState } from "react";

export type ProGridColumn<TRow> = {
  key: string;
  header: string;
  width?: number;
  render: (row: TRow) => ReactNode;
  getText?: (row: TRow) => string;
};

export type SavedGridView = {
  id: string;
  name: string;
  search: string;
  columnOrder: string[];
};

type BulkAction<TRow> = {
  key: string;
  label: string;
  validate: (rows: TRow[]) => string | null;
  execute: (rows: TRow[]) => Promise<void>;
};

type Props<TRow extends { id: string | number }> = {
  gridKey: string;
  rows: TRow[];
  columns: ProGridColumn<TRow>[];
  bulkActions?: BulkAction<TRow>[];
};

export function ProDataGrid<TRow extends { id: string | number }>({
  gridKey,
  rows,
  columns,
  bulkActions = [],
}: Props<TRow>) {
  const storageKey = `pro-grid:${gridKey}:views`;
  const [search, setSearch] = useState("");
  const [columnOrder, setColumnOrder] = useState(columns.map((column) => column.key));
  const [savedViews, setSavedViews] = useState<SavedGridView[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string | number>>(new Set());
  const [activeCell, setActiveCell] = useState({ row: 0, column: 0 });
  const [status, setStatus] = useState<string | null>(null);
  const cellRefs = useRef(new Map<string, HTMLTableCellElement>());

  useEffect(() => {
    const raw = window.localStorage.getItem(storageKey);
    if (raw) {
      setSavedViews(JSON.parse(raw) as SavedGridView[]);
    }
  }, [storageKey]);

  const orderedColumns = useMemo(() => {
    return columnOrder
      .map((key) => columns.find((column) => column.key === key))
      .filter((column): column is ProGridColumn<TRow> => Boolean(column));
  }, [columnOrder, columns]);

  const visibleRows = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) {
      return rows;
    }

    return rows.filter((row) =>
      orderedColumns.some((column) => (column.getText?.(row) ?? String(column.render(row) ?? "")).toLowerCase().includes(term)),
    );
  }, [orderedColumns, rows, search]);

  function focusCell(row: number, column: number) {
    const boundedRow = Math.max(0, Math.min(row, Math.max(0, visibleRows.length - 1)));
    const boundedColumn = Math.max(0, Math.min(column, Math.max(0, orderedColumns.length - 1)));
    setActiveCell({ row: boundedRow, column: boundedColumn });
    window.requestAnimationFrame(() => cellRefs.current.get(`${boundedRow}:${boundedColumn}`)?.focus());
  }

  function handleCellKeyDown(event: KeyboardEvent<HTMLTableCellElement>, rowIndex: number, columnIndex: number) {
    if (event.key === "ArrowDown" || event.key === "Enter") {
      event.preventDefault();
      focusCell(rowIndex + 1, columnIndex);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      focusCell(rowIndex - 1, columnIndex);
    } else if (event.key === "ArrowRight" || event.key === "Tab") {
      event.preventDefault();
      focusCell(rowIndex, columnIndex + 1);
    } else if (event.key === "ArrowLeft") {
      event.preventDefault();
      focusCell(rowIndex, columnIndex - 1);
    } else if (event.key === " ") {
      event.preventDefault();
      toggleRow(visibleRows[rowIndex].id);
    }
  }

  function toggleRow(id: string | number) {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function saveView() {
    const name = window.prompt("View name");
    if (!name?.trim()) {
      return;
    }

    const next = [...savedViews.filter((view) => view.name !== name.trim()), {
      id: crypto.randomUUID(),
      name: name.trim(),
      search,
      columnOrder,
    }];
    setSavedViews(next);
    window.localStorage.setItem(storageKey, JSON.stringify(next));
  }

  function applyView(view: SavedGridView) {
    setSearch(view.search);
    setColumnOrder(view.columnOrder);
  }

  async function runBulk(action: BulkAction<TRow>) {
    const selectedRows = visibleRows.filter((row) => selectedIds.has(row.id));
    const validationError = action.validate(selectedRows);
    if (validationError) {
      setStatus(validationError);
      return;
    }

    try {
      await action.execute(selectedRows);
      setSelectedIds(new Set());
      setStatus(`${action.label} completed for ${selectedRows.length} row(s).`);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Bulk action failed.");
    }
  }

  return (
    <section className="pro-grid" aria-label={gridKey}>
      <div className="pro-grid__toolbar">
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search" />
        <button type="button" onClick={saveView}>Save view</button>
        <select onChange={(event) => {
          const view = savedViews.find((item) => item.id === event.target.value);
          if (view) applyView(view);
        }}>
          <option value="">Saved views</option>
          {savedViews.map((view) => <option key={view.id} value={view.id}>{view.name}</option>)}
        </select>
        {bulkActions.map((action) => (
          <button key={action.key} type="button" disabled={selectedIds.size === 0} onClick={() => void runBulk(action)}>
            {action.label}
          </button>
        ))}
      </div>
      {status && <div className="pro-grid__status">{status}</div>}
      <table>
        <thead>
          <tr>
            <th scope="col"></th>
            {orderedColumns.map((column) => <th key={column.key} style={{ width: column.width }}>{column.header}</th>)}
          </tr>
        </thead>
        <tbody>
          {visibleRows.map((row, rowIndex) => (
            <tr key={row.id} aria-selected={selectedIds.has(row.id)}>
              <td>
                <input type="checkbox" checked={selectedIds.has(row.id)} onChange={() => toggleRow(row.id)} />
              </td>
              {orderedColumns.map((column, columnIndex) => (
                <td
                  key={column.key}
                  tabIndex={activeCell.row === rowIndex && activeCell.column === columnIndex ? 0 : -1}
                  ref={(node) => {
                    if (node) cellRefs.current.set(`${rowIndex}:${columnIndex}`, node);
                  }}
                  onKeyDown={(event) => handleCellKeyDown(event, rowIndex, columnIndex)}
                  onFocus={() => setActiveCell({ row: rowIndex, column: columnIndex })}
                >
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
