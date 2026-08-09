#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
tax_exporter.py
===============

Playwright automation for the Iranian Tax Portal (سامانه مودیان) "output-file"
(«درخواست فایل خروجی») workflow. It runs as two separate invocations around a
human-supplied SMS OTP so credentials/OTP are never persisted on disk.

The *login + CAPTCHA* logic no longer lives here — it was extracted into the
dedicated `scripts/login.py` module. This refactored script imports
`handle_login_and_captcha` from `login` and focuses purely on the export flow.

  initiate:
      python scripts/tax_exporter.py --action initiate --username <u> --password <pw>
    * Opens the login page with a *persistent* Chromium profile (browser_data).
    * Checks if the cached session is already authenticated; if so, returns
      success immediately without sending a new SMS OTP.
    * Otherwise calls `handle_login_and_captcha(page, username, password)` (from
      login.py) which fills username + password and solves the login captcha
      (ddddocr -> GapGPT).
    * Saves the browser state so the authenticated session survives to `complete`.

  complete:
      python scripts/tax_exporter.py --action complete --username <u> --password <pw>
                 --otp <code> --year <1405> --period <2> --outdir <dir>
    * Reopens the persistent profile saved by `initiate`.
    * Navigates to https://my.tax.gov.ir/ FIRST (NOT outputFiles) to check
      whether the cached session is ALREADY authenticated. If the SSO
      redirects to tp.tax.gov.ir, login and OTP are SKIPPED entirely.
    * If NOT authenticated, stays on the SSO page where the OTP input field
      is already waiting (from the `initiate` step), fills --otp, submits,
      and waits for the post-OTP redirect to tp.tax.gov.ir. Only AFTER the
      redirect succeeds does it navigate to the outputFiles page.
      (This prevents navigating to outputFiles BEFORE OTP submission, which
      would reset the SSO session on my.tax.gov.ir and invalidate the OTP.)
    * VERIFIES authentication again before proceeding to modal automation.
    * Automates the «درخواست فایل خروجی» modal: opens it if needed, selects
      «خرید داخلی», selects ALL «وضعیت صورتحساب» and «الگوی صورتحساب» options,
      sets «سال صدور صورتحساب» (from --year) and «دوره صدور صورتحساب» (from
      --period / PERIOD_LABELS mapping), then clicks the green submit button.
    * Polls the requests table (every 10s, up to 5 minutes) until the top row
      status turns «موفق» (Successful), then triggers the download with
      `page.expect_download()` and saves the file.

Exactly ONE JSON object is printed to STDOUT on exit so the .NET host can
`json.loads(stdout)`:

  initiate -> {"success": bool, "status": "OTP_SENT" | "ALREADY_LOGGED_IN",
               "message": str, "challengeToken": str, "maskedPhone": str}
      * On Exit Code 0 the SMS was actually triggered (or the session was
        already active). `status` distinguishes the two cases.
  complete -> {"success": bool, "filePath": str, "rows": int,  "message": str}

All diagnostics / logs go to STDERR so the STDOUT contract stays clean.

Sandbox mode (--sandbox) simulates the flow locally and writes a small sample
file so integration testing works without a live portal session.
"""

from __future__ import annotations

import argparse
import os
import pathlib
import sys
import time
import traceback

# ----------------------------------------------------------------------------
# Guaranteed File Logger — writes every diagnostic line to an absolute file
# path in the project root so it survives regardless of C#'s working directory.
# This duplicates the logger defined in login.py so that even if the import
# chain fails, we still get a debug_log.txt in the project root.
# ----------------------------------------------------------------------------
_LOG_FILE = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "debug_log.txt"))


def _file_log(msg: str) -> None:
    """Write a bare line to the guaranteed debug file (no timestamp, no stderr)."""
    try:
        with open(_LOG_FILE, "a", encoding="utf-8") as f:
            f.write(msg + "\n")
            f.flush()
    except Exception:
        pass

try:  # Playwright is required for the non-sandbox flow.
    from playwright.sync_api import sync_playwright  # type: ignore
    _PLAYWRIGHT_OK = True
except Exception:  # pragma: no cover - optional dependency
    sync_playwright = None
    _PLAYWRIGHT_OK = False

# ----------------------------------------------------------------------------
# Import the shared login/captcha logic + constants + generic DOM helpers from
# the dedicated `login` module (keeps the inner mechanics DRY and untouched).
# ----------------------------------------------------------------------------
from login import (  # noqa: E402
    LOGIN_BASE,
    OUTPUT_FILES_URL,
    SUCCESS_STATUS,
    PERIOD_LABELS,
    STATUS_ALREADY_LOGGED_IN,
    STATUS_OTP_SENT,
    STATUS_OTP_NOT_SENT,
    TAX_USERNAME,
    TAX_PASSWORD,
    log,
    json_out,
    fail,
    handle_login_and_captcha,
    pick,
    fill_by_hint,
    click_by_text,
    wait_for_page_load,
    _is_authenticated,
    _is_already_authenticated,
)
# ----------------------------------------------------------------------------
# Debug helpers: explicit stderr logging with flush + screenshots.
# ----------------------------------------------------------------------------
def _debug(msg: str) -> None:
    """Write a detailed [LOG] line to STDERR with immediate flush so the
    .NET host and any watcher sees the line in real time."""
    print(f"[LOG] {msg}", file=sys.stderr, flush=True)
    # Also write to the guaranteed file log for post-mortem analysis.
    try:
        with open(_LOG_FILE, "a", encoding="utf-8") as f:
            f.write(f"[LOG] {msg}\n")
            f.flush()
    except Exception:
        pass


def _screenshot(page, label: str, outdir: pathlib.Path) -> None:
    """Save a debug screenshot with the given label."""
    try:
        path = outdir / f"{label}.png"
        page.screenshot(path=str(path), full_page=True)
        _debug(f"screenshot saved -> {path}")
    except Exception as exc:
        _debug(f"WARNING: could not save screenshot '{label}': {exc}")


# ----------------------------------------------------------------------------
# Login completion: SMS OTP entry (the OTP itself is entered here, after
# `handle_login_and_captcha` triggered the SMS).
# ----------------------------------------------------------------------------
def _enter_otp(page, otp: str):
    log("[LOG] Submitting OTP on SSO page...")
    fill_by_hint(page, otp, "کد تأیید", "کد پیامک", "کد تایید", "otp",
                 field_selectors=("input[autocomplete='one-time-code']", "input[name*='otp']",
                                  "input[name*='verify']", "input[inputmode='numeric']"))
    click_by_text(page, "تأیید", selectors=("button", "input[type='submit']")) \
        or click_by_text(page, "ورود", selectors=("button", "input[type='submit']"))
    wait_for_page_load(page)
    # The portal may first ask to confirm the phone/security method; accept it.
    click_by_text(page, "تأیید", selectors=("button", "input[type='submit']"))
    wait_for_page_load(page)

    # Wait for the post-OTP redirect to the tax portal (tp.tax.gov.ir).
    # This ensures the SSO flow completed successfully before we navigate.
    log("[LOG] OTP submitted. Waiting for SSO redirection...")
    try:
        page.wait_for_url("**/tp.tax.gov.ir/**", timeout=30000)
        log(f"post-OTP redirect confirmed: {page.url}")
    except Exception as exc:
        log(f"post-OTP redirect did not occur within 30s: {exc}")
        raise RuntimeError(
            "ورود کد تأیید انجام شد اما انتقال به پرتال مالیاتی (tp.tax.gov.ir) "
            "صورت نگرفت. لطفاً وضعیت اینترنت یا کد پیامک را بررسی کنید."
        ) from exc


# ----------------------------------------------------------------------------
# Navigation to the output-files page.
# ----------------------------------------------------------------------------
def _open_output_files(page, debug_dir: pathlib.Path | None = None):
    log(f"navigating to {OUTPUT_FILES_URL}")
    _debug(f"Checking initial page URL... Current: {page.url}")
    page.goto(OUTPUT_FILES_URL, wait_until="domcontentloaded", timeout=45000)
    wait_for_page_load(page)
    _debug(f"After navigation URL: {page.url}")
    if debug_dir is not None:
        _screenshot(page, "debug_1_after_nav", debug_dir)


# ----------------------------------------------------------------------------
# «درخواست فایل خروجی» modal automation helpers.
# ----------------------------------------------------------------------------
def _find_modal(page):
    """Return the visible modal container for «درخواست فایل خروجی», or None."""
    for sel in ("div[role='dialog']", ".modal", ".modal-content", ".RadWindow", "[class*='dialog']"):
        try:
            m = page.query_selector(sel)
            if m is not None and m.is_visible() and "درخواست فایل خروجی" in (m.inner_text() or ""):
                return m
        except Exception:
            continue
    return None


def _wait_for_modal(page, timeout_seconds: float = 10.0) -> bool:
    """Wait until the «درخواست فایل خروجی» modal dialog actually appears."""
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        if _find_modal(page) is not None:
            return True
        time.sleep(0.3)
    return False


def _open_export_modal(page, debug_dir: pathlib.Path | None = None):
    """Ensure the «درخواست فایل خروجی» modal is open, then wait for the dialog
    to appear before any fields are filled (task: click the green trigger and
    wait for the modal). Returns True when the modal is visible."""
    _debug("Attempting to find export modal button 'درخواست فایل خروجی'...")
    if _find_modal(page) is not None:
        log("export modal already open; skipping trigger click")
        _debug("Modal was already open (no button click needed).")
        return True
    btn_found = click_by_text(page, "درخواست فایل خروجي", selectors=("button", "a", "input[type='button']"))
    _debug(f"Button found: {btn_found}. Clicking...")
    if btn_found:
        log("clicked the green «درخواست فایل خروجی» button that opens the export modal")
    else:
        log("primary trigger not found; trying the «خروجی» fallback")
        _debug("Primary button NOT found. Falling back to 'خروجی'.")
        click_by_text(page, "خروجی", selectors=("button", "a"))
    time.sleep(0.5)
    modal_visible = _wait_for_modal(page)
    _debug(f"Waiting for modal dialog... Visible: {modal_visible}")
    if not modal_visible:
        log("export modal did not appear; proceeding to fill fields anyway")
        return False
    log("export modal dialog appeared; ready to fill fields")
    if debug_dir is not None:
        _screenshot(page, "debug_2_modal_open", debug_dir)
    return True


def _click_in_modal(page, text, selectors=("button", "input[type='submit']")):
    """Click the first visible element (button/submit) inside the open modal
    whose inner text contains `text`. Falls back to a page-wide search."""
    modal = _find_modal(page)
    scope = modal if modal is not None else page
    for tag in selectors:
        for el in scope.query_selector_all(tag):
            try:
                inner = (el.inner_text() or "").strip()
                if text in inner and el.is_visible():
                    el.click()
                    return True
            except Exception:
                continue
    return False


def _find_section(page, label_hint):
    """Return the nearest visible container whose text contains `label_hint`."""
    for tag in ("label", "td", "th", "div", "span", "fieldset"):
        for el in page.query_selector_all(tag):
            try:
                if el.is_visible() and label_hint in (el.inner_text() or ""):
                    return el
            except Exception:
                continue
    return None


def _find_dropdown_trigger(page, label_hint):
    section = _find_section(page, label_hint)
    if section is None:
        return None
    for sel in ("[role='combobox']", "button", "input[type='text']", "[class*='dropdown']", "[class*='select']"):
        try:
            el = section.query_selector(sel)
            if el is not None and el.is_visible():
                return el
        except Exception:
            continue
    return None


def _select_radio(page, hints):
    for hint in hints:
        for sel in ("input[type='radio']", "label", "span"):
            for el in page.query_selector_all(sel):
                try:
                    if not el.is_visible():
                        continue
                    value = (el.get_attribute("value") or "") + (el.inner_text() or "")
                    if hint in value:
                        if el.tag_name.lower() in ("label", "span"):
                            el.click()
                            return
                        el.check(check_state="checked", force=True)
                        return
                except Exception:
                    continue
def _select_option(page, label):
    """Pick a single option from a native `<select>` by label (with fallback)."""
    for el in page.query_selector_all("select"):
        try:
            for opt in el.query_selector_all("option"):
                candidate = (opt.inner_text() or "") + (opt.get_attribute("value") or "")
                if label in candidate:
                    el.select_option(label=opt.inner_text())
                    return
        except Exception:
            continue
    _click_labelled(page, label)


def _click_labelled(page, label):
    for tag in ("label", "span", "div"):
        for el in page.query_selector_all(tag):
            try:
                if el.is_visible() and label in (el.inner_text() or ""):
                    el.click()
                    return
            except Exception:
                continue


def _check_all(page, label_hint):
    """Tick every checkbox in the labelled section (e.g. 'select all')."""
    for el in page.query_selector_all("input[type='checkbox']"):
        try:
            row = el.evaluate(
                "el => { const g = el.closest('tr,div,label'); return g ? (g.innerText || '') : ''; }")
            if label_hint in row:
                el.check(force=True)
        except Exception:
            continue


def _select_all_options(page, label_hint):
    """
    Select ALL options in the labelled dropdown (e.g. «وضعیت صورتحساب» /
    «الگوی صورتحساب»). Handles both a native multi-select and the RadComboBox
    style multi-select (a combobox whose opened list is a set of checkboxes).
    """
    _debug(f"Opening and selecting dropdown '{label_hint}'...")
    # 1) Native <select> / <select multiple>: select every option.
    for el in page.query_selector_all("select"):
        try:
            row = el.evaluate(
                "el => { const g = el.closest('tr,div,label,td,fieldset'); return g ? (g.innerText||'') : ''; }")
            opts = el.query_selector_all("option")
            if opts and label_hint in row:
                values = []
                for opt in opts:
                    values.append(opt.get_attribute("value") or (opt.inner_text() or ""))
                el.select_option(values=values)
                _debug(f"'{label_hint}' -> selected {len(values)} option(s) via native <select>")
                log(f"selected ALL \"{label_hint}\" options via native <select>")
                return
        except Exception:
            continue

    # 2) RadComboBox multi-select: open the dropdown, tick every visible checkbox.
    trigger = _find_dropdown_trigger(page, label_hint)
    if trigger is not None:
        checked_count = 0
        try:
            trigger.click()
            time.sleep(0.4)
            for el in page.query_selector_all("input[type='checkbox']"):
                try:
                    if el.is_visible():
                        el.check(check_state="checked", force=True)
                        checked_count += 1
                except Exception:
                    continue
            _debug(f"'{label_hint}' -> ticked {checked_count} checkbox(es)")
            log(f"opened \"{label_hint}\" dropdown and ticked all visible checkboxes")
        finally:
            try:
                trigger.press("Escape")
            except Exception:
                pass

    # 3) Last-ditch: tick the "select all" checkbox of the labelled section.
    _check_all(page, label_hint)


def _set_dropdown_value(page, label_hint, value):
    """
    Pick `value` from the labelled dropdown (native <select> or RadComboBox).
    Used for «سال صدور صورتحساب» and «دوره صدور صورتحساب».
    """
    # 1) Native select whose labelling row contains label_hint.
    for el in page.query_selector_all("select"):
        try:
            row = el.evaluate(
                "el => { const g = el.closest('tr,div,label,td,fieldset'); return g ? (g.innerText||'') : ''; }")
            if label_hint in row:
                for opt in el.query_selector_all("option"):
                    candidate = (opt.inner_text() or "") + (opt.get_attribute("value") or "")
                    if str(value) in candidate:
                        el.select_option(value=opt.get_attribute("value") or opt.inner_text())
                        log(f"set \"{label_hint}\" = {value} via native <select>")
                        return True
        except Exception:
            continue

    # 2) RadComboBox: open the dropdown, click the matching item.
    trigger = _find_dropdown_trigger(page, label_hint)
    if trigger is not None:
        trigger.click()
        time.sleep(0.4)
        _click_labelled(page, str(value))
        try:
            trigger.press("Escape")
        except Exception:
            pass
        return True
    return False


def _set_year(page, year):
    if year:
        log(f"setting «سال صدور صورتحساب» to {year}")
        _set_dropdown_value(page, "سال صدور صورتحساب", str(year))


def _set_period(page, period, year):
    label = PERIOD_LABELS.get(period) or PERIOD_LABELS.get(1)
    if year:
        log(f"setting «دوره صدور صورتحساب» to {label} (period {period})")
        _set_dropdown_value(page, "دوره صدور صورتحساب", label)
def _submit_export_request(page, year, period, debug_dir: pathlib.Path | None = None):
    """
    Automate the «درخواست فایل خروجی» modal:
      * open the modal if it is not already open,
      * select the «خرید داخلی» radio button,
      * select ALL «وضعیت صورتحساب» options,
      * select ALL «الگوی صورتحساب» options,
      * set «سال صدور صورتحساب» from `year`,
      * set «دوره صدور صورتحساب» from `period` (PERIOD_LABELS mapping),
      * click the green «درخواست فایل خروجی» submit button.
    """
    log("beginning export-request modal automation")
    _open_export_modal(page, debug_dir=debug_dir)

    # Select the «خرید داخلی» (BUY_DOMESTIC) radio button.
    _debug("Selecting 'خرید داخلی'...")
    _select_radio(page, ("خرید داخلی", "BUY_DOMESTIC", "خرید"))
    log("selected radio: خرید داخلی")
    _debug("'خرید داخلی' radio selected successfully.")

    # Select ALL options in the two multi-select dropdowns.
    _select_all_options(page, "وضعیت صورتحساب")
    _select_all_options(page, "الگوی صورتحساب")

    # Set the invoice year + period.
    _set_year(page, year)
    _set_period(page, period, year)

    # Debug screenshot: form filled, just before submit.
    if debug_dir is not None:
        _screenshot(page, "debug_3_form_filled", debug_dir)

    # Submit: click the green «درخواست فایل خروجی» button inside the modal.
    _debug("Clicking green submit button inside modal...")
    clicked = _click_in_modal(page, "درخواست فایل خروجی", selectors=("button", "input[type='submit']"))
    _debug(f"Submit button in modal clicked: {clicked}")
    if not clicked:
        log("submit button not found in modal; trying green/text fallback")
        _debug("Falling back to green/text submit button...")
        _click_green_submit(page)
    wait_for_page_load(page)
    log("export request submitted")

    # Debug screenshot: after submit.
    if debug_dir is not None:
        _screenshot(page, "debug_4_after_submit", debug_dir)


def _click_green_submit(page):
    """
    Click the green «درخواست فایل خروجی» submit control. Tries a green/success
    styled button/input first, then falls back to a text-based page-wide click.
    """
    modal = _find_modal(page)
    scope = modal if modal is not None else page
    for tag in ("button", "input[type='submit']", "a"):
        for el in scope.query_selector_all(tag):
            try:
                cls = (el.get_attribute("class") or "").lower()
                text = (el.inner_text() or "").strip()
                if ("درخواست فایل خروجی" in text and el.is_visible()) or \
                   (("green" in cls or "success" in cls or "ok" in cls) and el.is_visible()):
                    el.click()
                    return True
            except Exception:
                continue
    return click_by_text(page, "درخواست فایل خروجی", selectors=("button", "input[type='submit']"))
# ----------------------------------------------------------------------------
# Async download handling: poll the requests table until the top row is ready,
# then trigger the download with page.expect_download() and save the file.
# ----------------------------------------------------------------------------
def _top_row(page):
    """Return the first (top / most-recent) <tr> of the requests table, or None."""
    for table_sel in ("table", "[class*='grid']", "[class*='RadGrid']"):
        for table in page.query_selector_all(table_sel):
            try:
                rows = table.query_selector_all("tbody tr, tr")
                if rows:
                    return rows[0]
            except Exception:
                continue
    return None


def _top_row_text(page) -> str:
    """Best-effort text of the first (top) row of the requests table."""
    row = _top_row(page)
    if row is None:
        return ""
    try:
        return (row.inner_text() or "").strip()
    except Exception:
        return ""


def _poll_until_ready(page, timeout_seconds: int = 300, debug_dir: pathlib.Path | None = None):
    """
    Stay on the outputFiles page and poll the requests table every 10 seconds
    (up to `timeout_seconds`, default 5 minutes) until the top row status turns
    «موفق» (Successful). Returns the top-row download control when ready.
    """
    log(f"polling export queue (every 10s, up to {timeout_seconds}s) for «{SUCCESS_STATUS}»")
    started = time.monotonic()
    last_status = ""
    iteration = 0
    while time.monotonic() - started < timeout_seconds:
        iteration += 1
        page.reload(wait_until="domcontentloaded")
        wait_for_page_load(page)
        try:
            body = page.inner_text("body")
        except Exception:
            body = ""
        top_row = _top_row_text(page)
        _debug(f"Polling loop iteration {iteration}: Checking top row status... Found text: {top_row[:200] if top_row else '(empty)'}")
        if SUCCESS_STATUS in top_row or SUCCESS_STATUS in body:
            log("queue status is «موفق» (Successful)")
            link = _find_download_link(page)
            if link is not None:
                return link
            log("status is «موفق» but no download control found; retrying")
            time.sleep(10)
            continue
        for kw in ("در حال انجام", "در حال تولید", "در انتظار"):
            source = top_row or body
            if kw in source and kw != last_status:
                log(f"current queue status: {kw}")
                last_status = kw
                break
        time.sleep(10)
    _debug("Polling timed out after {:.0f}s with {} iterations".format(
        time.monotonic() - started, iteration))
    if debug_dir is not None:
        _screenshot(page, "debug_5_polling_error", debug_dir)
    raise RuntimeError("زمان‌بندی تولید فایل خروجی به پایان رسید. لطفاً بعداً دوباره تلاش کنید.")


def _find_download_link(page):
    """Find the download control (CSV/file button) on the top request row,
    falling back to a page-wide search. Looks for «دانلود» text or
    download/csv/file/excel icon markers."""
    top = _top_row(page)
    scopes = [top] if top is not None else []
    scopes.append(page)
    markers = ("download", "csv", "file", "excel", "xlsx")
    for scope in scopes:
        for tag in ("a", "button", "input[type='button']"):
            for el in scope.query_selector_all(tag):
                try:
                    text = (el.inner_text() or "").strip()
                    cls = (el.get_attribute("class") or "").lower()
                    title = (el.get_attribute("title") or "")
                    if any(m in text for m in ("دانلود", "download", "csv", "file")) or \
                       any(m in cls for m in markers) or "دانلود" in title:
                        return el
                except Exception:
                    continue
    return None


def _download_file(page, outdir: pathlib.Path, link) -> pathlib.Path:
    """
    Trigger the download via `page.expect_download()`, save the downloaded file
    into `outdir` and return its absolute path.
    """
    outdir.mkdir(parents=True, exist_ok=True)
    with page.expect_download(timeout=60000) as download_info:
        link.click(timeout=15000)
    download = download_info.value
    suggested = download.suggested_filename or f"mowadiyan_{int(time.time())}.xlsx"
    dest = outdir / suggested
    download.save_as(str(dest))
    log(f"downloaded file saved at {dest}")
    return dest
# ----------------------------------------------------------------------------
# Action implementations.
# ----------------------------------------------------------------------------


def action_initiate(args) -> None:
    if not _PLAYWRIGHT_OK:
        fail("Playwright در دسترس نیست. pip install playwright && playwright install chromium")

    # Preserve session persistence: do NOT clear the cached browser profile.
    # Instead, open the persistent context first and check if we are already
    # authenticated. If the session is still valid we return success immediately
    # without sending a new SMS OTP; only otherwise do we run the full login.

    log(f"launching browser (headless={not args.headed}, profile={args.browser_data})")
    with sync_playwright() as p:
        context = p.chromium.launch_persistent_context(
            user_data_dir=str(args.browser_data),
            headless=not args.headed,
            no_viewport=True,
            args=["--disable-blink-features=AutomationControlled"],
        )
        page = context.pages[0] if context.pages else context.new_page()
        log(f"browser launched; current page URL: {page.url}")

        # 1. Navigate to the SSO login page and check if already authenticated.
        log("opening SSO login page")
        page.goto(f"{LOGIN_BASE}/Login", wait_until="domcontentloaded", timeout=45000)
        wait_for_page_load(page)
        log(f"SSO login page loaded; current URL: {page.url}")

        if _is_already_authenticated(page):
            log("session already active on initiate; no SMS OTP required")
            context.close()
            json_out({
                "success": True,
                "status": STATUS_ALREADY_LOGGED_IN,
                "message": "نشست از قبل فعال است، نیازی به دریافت کد SMS نیست.",
            })
            return

        # 2. Not authenticated yet — run the full login + captcha flow.
        log("not authenticated; running full login + captcha flow")
        info = handle_login_and_captcha(page, args.username, args.password)
        log(f"login result status: {info.get('status')}")
        context.close()

    status = info.get("status")
    if status == STATUS_ALREADY_LOGGED_IN:
        json_out({
            "success": True,
            "status": STATUS_ALREADY_LOGGED_IN,
            "message": "نشست از قبل فعال است، نیازی به دریافت کد SMS نیست.",
        })
        return
    if status == STATUS_OTP_NOT_SENT:
        fail("کد کپچا وارد نشد یا توسط سرور رد شد.")

    # status == STATUS_OTP_SENT
    json_out({
        "success": True,
        "message": "کد پیامک با موفقیت توسط سامانه مالیاتی صادر شد.",
        "challengeToken": info.get("challengeToken") or "python-challenge",
        "maskedPhone": info.get("maskedPhone"),
    })


def action_complete(args) -> None:
    if not _PLAYWRIGHT_OK:
        fail("Playwright در دسترس نیست. pip install playwright && playwright install chromium")
    outdir = pathlib.Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)
    args.browser_data.mkdir(parents=True, exist_ok=True)

    if not args.otp or (args.otp.strip() in ("", "0", "sandbox")):
        log("OTP is missing or placeholder; attempting cached-session-only flow")
        log(f"OTP value received: '{args.otp[:3]}...' (len={len(args.otp)})")

    log(f"launching browser (headless={not args.headed}, profile={args.browser_data})")
    with sync_playwright() as p:
        context = p.chromium.launch_persistent_context(
            user_data_dir=str(args.browser_data),
            headless=not args.headed,
            no_viewport=True,
            args=["--disable-blink-features=AutomationControlled"],
        )
        page = context.pages[0] if context.pages else context.new_page()
        log(f"browser launched; initial page URL: {page.url}")
        try:
            # -----------------------------------------------------------------
            # CRITICAL: Do NOT navigate to outputFiles yet — going to
            # tp.tax.gov.ir would reset the SSO session on my.tax.gov.ir
            # and invalidate the OTP. Instead, navigate to the SSO page first
            # and check auth from there.
            # -----------------------------------------------------------------
            log("navigating to SSO page to check auth status")
            page.goto(f"{LOGIN_BASE}/", wait_until="domcontentloaded", timeout=45000)
            wait_for_page_load(page)
            log(f"SSO page loaded; current URL: {page.url}")

            if _is_already_authenticated(page):
                # Session is already active (redirected to tp.tax.gov.ir).
                log("session already active (redirected to tp.tax.gov.ir); skipping login and OTP entirely")
                _open_output_files(page, debug_dir=outdir)
                is_auth = _is_authenticated(page)
                log(f"_is_authenticated evaluation result: {is_auth} (current URL: {page.url})")
                if not is_auth:
                    _screenshot(page, "debug_auth_failed", outdir)
                    fail("احراز هویت با وجود هدایت به پرتال مالیاتی ناموفق بود.")
            else:
                # NOT authenticated — we are on my.tax.gov.ir where the OTP
                # input field is waiting (SMS was triggered by `initiate`).
                # Enter the OTP right here on the current SSO page and wait
                # for the post-OTP redirect to tp.tax.gov.ir. Only AFTER the
                # redirect do we navigate to the outputFiles page.
                log("not authenticated; entering OTP on SSO page")
                _enter_otp(page, args.otp)
                log("[LOG] SSO Redirection successful. Now navigating to outputFiles...")

                # Now navigate to the output-files page.
                _open_output_files(page, debug_dir=outdir)

                # CRITICAL: verify authentication was actually successful before
                # attempting modal automation.
                is_auth = _is_authenticated(page)
                _debug(f"Authentication status after OTP + redirect: {is_auth}")
                if not is_auth:
                    _screenshot(page, "debug_auth_failed", outdir)
                    fail(
                        "احراز هویت پس از وارد کردن کد پیامک ناموفق بود. "
                        "لطفاً کد پیامک صحیح را وارد کنید یا مجدداً از ابتدا تلاش نمایید."
                    )

            _submit_export_request(page, args.year, args.period, debug_dir=outdir)
            link = _poll_until_ready(page, debug_dir=outdir)
            file_path = _download_file(page, outdir, link)
        except Exception as exc:
            _debug(f"EXCEPTION caught: {exc}")
            _debug(f"Full traceback:\n{traceback.format_exc()}")
            _debug(f"Page URL at failure: {page.url}")
            try:
                _screenshot(page, "debug_error", outdir)
            except Exception as ss_exc:
                _debug(f"WARNING: could not save error screenshot: {ss_exc}")
            context.close()
            raise
        context.close()

    json_out({
        "success": True,
        "filePath": str(file_path.resolve()),
        "rows": 0,
        "message": "فایل خروجی با موفقیت دریافت شد.",
    })


def action_sandbox_complete(args) -> None:
    """Offline simulation: write a small CSV sample into --outdir."""
    import csv
    outdir = pathlib.Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)
    file = outdir / f"mowadiyan_{args.year}_{args.period}.csv"
    with file.open("w", newline="", encoding="utf-8-sig") as fh:
        writer = csv.writer(fh)
        writer.writerow(["شماره صورتحساب", "تاریخ", "مالیات", "عوارض", "مبلغ کل", "نرخ مالیات"])
        for i in range(1, 6):
            writer.writerow([f"{args.year}-{1000 + i}-{i:03d}",
                             f"{args.year}/{(i % 4) + 1:02d}/0{i:02d}",
                             (12_500_000 + i * 350_000) * 0.10, 0, 12_500_000 + i * 350_000, 0.10])
    json_out({"success": True, "filePath": str(file.resolve()), "rows": 4,
              "message": "فایل نمونه sandbox تولید شد."})
# ----------------------------------------------------------------------------
# CLI.
# ----------------------------------------------------------------------------
def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Mowadiyan Tax Portal output-file exporter (Playwright).")
    parser.add_argument("--action", choices=("initiate", "complete"), required=True)
    parser.add_argument("--username", default="")
    parser.add_argument("--password", default="")
    parser.add_argument("--otp", default="")
    parser.add_argument("--year", type=int, default=0)
    parser.add_argument("--period", type=int, default=0)
    parser.add_argument("--outdir", default=str(pathlib.Path.cwd() / "wwwroot" / "uploads" / "mowadiyan"))
    parser.add_argument("--browser-data", default=str(pathlib.Path.cwd() / "browser_data"))
    parser.add_argument("--sandbox", action="store_true", help="produce a local sample file (no browser).")
    parser.add_argument("--headed", action="store_true", help="run the browser in headed (visible) mode.")
    return parser


def _resolve_credentials(args):
    """
    Verify the incoming CLI credentials and return the resolved (username, password).

    Logs the lengths of the `--username` / `--password` values received from the
    CLI so a debugging session can confirm whether the .NET host actually passed
    real values. When an argument is missing/empty we first try the hardcoded
    fallback constants (TAX_USERNAME / TAX_PASSWORD, env-configurable); if both
    sources are empty we fail fast with a clear, machine-readable error so the
    .NET host never proceeds with blank credentials.
    """
    username = (args.username or "").strip()
    password = (args.password or "").strip()
    log(f"CLI credentials received -> --username length: {len(username)}, "
        f"--password length: {len(password)}")

    if not username:
        if TAX_USERNAME:
            log("--username empty; falling back to default/configured TAX_USERNAME")
            username = TAX_USERNAME
        else:
            log("--username empty and no fallback configured")
    if not password:
        if TAX_PASSWORD:
            log("--password empty; falling back to default/configured TAX_PASSWORD")
            password = TAX_PASSWORD
        else:
            log("--password empty and no fallback configured")

    if not username or not password:
        fail("اطلاعات ورود (نام کاربری/رمز) خالی است.")

    log(f"resolved credentials -> username length: {len(username)}, "
        f"password length: {len(password)}")
    return username, password


def main(argv=None) -> None:
    args = build_parser().parse_args(argv)
    args.browser_data = pathlib.Path(args.browser_data)

    log(f"=== STARTING TAX EXPORTER SCRIPT ===")
    log(f"Args: action={args.action}, sandbox={args.sandbox}, headed={args.headed}, "
        f"year={args.year}, period={args.period}")
    log(f"Working dir: {os.getcwd()}")
    log(f"Log file: {_LOG_FILE}")
    log(f"--outdir: {args.outdir}")
    log(f"--browser-data: {args.browser_data}")

    try:
        if args.sandbox:
            log("sandbox mode enabled — skipping browser")
            if args.action == "initiate":
                json_out({"success": True, "message": "کد پیامک با موفقیت (sandbox) ارسال شد.",
                          "challengeToken": "sandbox-challenge", "maskedPhone": None})
            else:
                action_sandbox_complete(args)
            return
        # Non-sandbox actions always talk to the portal and need credentials.
        username, password = _resolve_credentials(args)
        args.username = username
        args.password = password
        if args.action == "initiate":
            log("invoking action_initiate")
            action_initiate(args)
        else:
            log("invoking action_complete")
            action_complete(args)
        log("=== SCRIPT COMPLETED SUCCESSFULLY ===")
    except Exception as exc:  # noqa: BLE001
        log(f"unhandled error in main: {exc}")
        log(f"Full traceback:\n{traceback.format_exc()}")
        fail(str(exc))


if __name__ == "__main__":
    try:
        log("=== STARTING TAX EXPORTER SCRIPT ===")
        log(f"Raw args: {sys.argv}")
        main()
        log("=== SCRIPT COMPLETED SUCCESSFULLY ===")
    except Exception as e:
        log(f"CRITICAL ERROR / EXCEPTION UNHANDLED:\n{traceback.format_exc()}")
        sys.exit(1)