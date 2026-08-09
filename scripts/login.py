#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
login.py
========

Dedicated, self-contained module for the Iranian Tax Portal (سامانه مودیان)
login + CAPTCHA flow. The working inner mechanics were moved here verbatim from
`tax_exporter.py` / `app.py` so that any consumer (e.g. `tax_exporter.py`) can
log in by calling a single reusable function:

    from login import handle_login_and_captcha
    info = handle_login_and_captcha(page, username, password)

This module owns:
  * the optional dependency setup (ddddocr, requests, Playwright),
  * the GapGPT Vision CAPTCHA solver (`solve_with_gapgpt`),
  * the ddddocr + GapGPT fallback orchestrator (`solve_captcha`),
  * the full login handler (`handle_login_and_captcha(page, username, password)`),
  * and every small DOM helper those functions depend on.

Exposed API (imported by `tax_exporter.py`):
  * handle_login_and_captcha(page, username, password) -> dict
  * solve_with_gapgpt(image_bytes) -> str | None
  * solve_captcha(png) -> str | None
  * log / json_out / fail  (diagnostics + machine-readable STDOUT contract)
  * pick / wait_for_page_load / fill_by_hint / click_by_text  (generic DOM helpers)
  * _current_url / _is_already_authenticated / _is_on_login_page
  * constants: PORTAL_BASE, LOGIN_BASE, OUTPUT_FILES_URL, READY_STATUS,
    PERIOD_LABELS, STATUS_*, TAX_USERNAME, TAX_PASSWORD, GAPGPT_*,
    CAPTCHA_IMG_SELECTORS, OTP_FIELD_SELECTORS, LOGIN_ERROR_MARKERS,
    CAPTCHA_INPUT_SELECTOR, LOGIN_SUBMIT_SELECTOR, CAPTCHA_REFRESH_SELECTORS,
    MAX_LOGIN_ATTEMPTS
"""

from __future__ import annotations

import base64
import json
import os
import re
import sys
import time

# ----------------------------------------------------------------------------
# Guaranteed File Logger — writes every diagnostic line to an absolute file
# path in the project root so it survives regardless of C#'s working directory.
# ----------------------------------------------------------------------------
LOG_FILE = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "debug_log.txt"))

def log(msg: str) -> None:
    """Write a diagnostic line to STDERR AND the guaranteed log file."""
    timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
    line = f"[{timestamp}] {msg}"
    print(line, file=sys.stderr, flush=True)
    try:
        with open(LOG_FILE, "a", encoding="utf-8") as f:
            f.write(line + "\n")
            f.flush()
    except Exception:
        pass

# ----------------------------------------------------------------------------
# Optional, lazily-imported dependencies (the module still runs if absent).
# ----------------------------------------------------------------------------
try:  # captcha solver #1 - ddddocr (module-level engine, matching app.py)
    import ddddocr  # type: ignore
    ocr_engine = ddddocr.DdddOcr(show_ad=False)  # type: ignore
    HAS_DDDDOCR = True
except Exception:  # pragma: no cover - optional dependency
    ocr_engine = None
    HAS_DDDDOCR = False

try:  # captcha solver #2 - GapGPT vision endpoint (env-configured).
    import requests  # type: ignore
    _REQUESTS = requests
except Exception:  # pragma: no cover - optional dependency
    _REQUESTS = None

try:  # Playwright is required for the non-sandbox flow.
    from playwright.sync_api import sync_playwright  # type: ignore
    _PLAYWRIGHT_OK = True
except Exception:  # pragma: no cover - optional dependency
    _PLAYWRIGHT_OK = False


# ----------------------------------------------------------------------------
# Force UTF-8 on STDOUT/STDERR. When the .NET host runs this module with its
# output redirected/piped, Python races back to the OS locale codec, which on
# Windows is cp1252 ("charmap"). That codec cannot encode Persian/Arabic text
# (e.g. ی, U+06CC) and raises:
#     UnicodeEncodeError: 'charmap' codec can't encode character '\u06cc' ...
# Reconfiguring both streams to UTF-8 keeps the machine-readable STDOUT JSON
# contract intact (the C# host reads it as UTF-8) and lets diagnostic lines on
# STDERR carry Persian text too.
# ----------------------------------------------------------------------------
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except (AttributeError, ValueError):  # pragma: no cover - older interpreters
        pass

PORTAL_BASE = "https://tp.tax.gov.ir"
LOGIN_BASE = "https://my.tax.gov.ir"
OUTPUT_FILES_URL = f"{PORTAL_BASE}/invoice/outputFiles"
READY_STATUS = "آماده دانلود"
# Status that marks a completed / successful export request on the requests table
# (task: wait until a row's status is «موفق» = Successful before downloading).
SUCCESS_STATUS = "موفق"

# Tax period number -> Persian season label used on the export modal.
PERIOD_LABELS = {1: "بهار", 2: "تابستان", 3: "پاییز", 4: "زمستان"}

# Result "status" values emitted by `--action initiate` (see action_initiate).
STATUS_ALREADY_LOGGED_IN = "ALREADY_LOGGED_IN"
STATUS_OTP_SENT = "OTP_SENT"
STATUS_OTP_NOT_SENT = "OTP_NOT_SENT"

# Default hardcoded fallback credentials, used only when the CLI `--username` /
# `--password` arguments are missing/empty. Prefer environment variables so real
# secrets are never committed to disk; values can be overridden per deployment.
TAX_USERNAME = os.environ.get("TAX_PORTAL_USERNAME", "").strip()
TAX_PASSWORD = os.environ.get("TAX_PORTAL_PASSWORD", "").strip()

# GapGPT Vision CAPTCHA solver configuration (implementation from app.py). The
# endpoint defaults to app.py's value; both can be overridden via environment
# so real API keys are never committed to disk.
GAPGPT_ENDPOINT = os.environ.get("GAPGPT_API_URL", "https://api.gapgpt.app/v1/chat/completions")
GAPGPT_API_KEY = os.environ.get("GAPGPT_API_KEY", "YOUR_GAPGPT_API_KEY_HERE")

# Selectors used to confirm the portal actually presented an OTP field (i.e. the
# SMS was really triggered) after submitting the login form. This closes the
# false-positive bug where we returned Exit Code 0 without any SMS being sent.
# عکس کپچا (matching app.py selectors).
CAPTCHA_IMG_SELECTORS = (
    "img[src*='captcha']",
    "img[src*='Captcha']",
    "img[src*='getCaptcha']",
)

# کادر ورود کد پیامک (matching app.py selectors).
OTP_FIELD_SELECTORS = (
    "input[placeholder*='کد']",
    "input[name*='otp']",
    "input[name*='code']",
)

# Error toasts/alerts the portal shows when the login submission failed, e.g. a
# wrong captcha or wrong credentials. Detecting these prevents reporting an SMS
# that was never triggered.
LOGIN_ERROR_MARKERS = (
    "تصویر امنیتی اشتباه است",
    "تصویر امنیتی نادرست است",
    "نام کاربری یا کلمه عبور نادرست است",
    "کد امنیتی اشتباه است",
    "کد امنیتی معتبر نیست",
    "کد ملی یا شماره همراه ثبت نشده",
)

# ----------------------------------------------------------------------------
# Precise captcha / submit selectors on my.tax.gov.ir (see task: fix the empty
# captcha input and the false-success exit code).
# ----------------------------------------------------------------------------
# The captcha text box («تصویر امنیتی») — matching app.py's selector.
CAPTCHA_INPUT_SELECTOR = "input[placeholder*='تصویر امنیتی'], input[placeholder*='تصویر']"
# The purple login submission button («ورود به درگاه ملی» / «ورود»).
LOGIN_SUBMIT_SELECTOR = "button:has-text('ورود به درگاه ملی'), button:has-text('ورود')"
# Candidate selectors for the captcha refresh icon, used to request a fresh
# captcha image before re-submitting after a rejected attempt.
CAPTCHA_REFRESH_SELECTORS = (
    "#captchaRefresh", "#refreshCaptcha", "#reloadCaptcha",
    "img[title*='جدید']", "img[alt*='جدید']", "a[href*='captcha']",
    "button[title*='جدید']", "[class*='refresh']",
)
# How many times we solve+fresh the captcha and re-submit the login form before
# giving up and reporting a hard failure (task: retry loop up to 3 tries).
MAX_LOGIN_ATTEMPTS = 3


def json_out(obj) -> None:
    """Emit the final machine-readable result to STDOUT."""
    print(json.dumps(obj, ensure_ascii=False, default=str))
    sys.stdout.flush()


def fail(message: str, **extra) -> None:
    log(f"FAIL: {message}")
    json_out({"success": False, "message": message, **extra})
    sys.exit(1)
# ----------------------------------------------------------------------------
# Captcha solving (implementation from app.py): ddddocr -> GapGPT Vision.
# ----------------------------------------------------------------------------
def solve_with_gapgpt(image_bytes: bytes):
    """
    GapGPT Vision solver ported from app.py: POST the captcha image to
    GAPGPT_ENDPOINT and ask for its text. Endpoint/key come from the
    GAPGPT_ENDPOINT / GAPGPT_API_KEY constants (env-overridable, see above).
    """
    if _REQUESTS is None:
        return None
    if not GAPGPT_API_KEY or GAPGPT_API_KEY == "YOUR_GAPGPT_API_KEY_HERE":
        log("GapGPT API key is not configured")
        return None

    log("sending captcha image to GapGPT Vision...")
    base64_image = base64.b64encode(image_bytes).decode("utf-8")

    headers = {
        "Authorization": f"Bearer {GAPGPT_API_KEY}",
        "Content-Type": "application/json",
    }
    payload = {
        "model": "gpt-4o-mini",
        "messages": [
            {
                "role": "user",
                "content": [
                    {
                        "type": "text",
                        "text": (
                            "Extract ONLY the 4 or 5 English letters and"
                            " numbers in this CAPTCHA image. Return ONLY the"
                            " string without spaces or extra text."
                        ),
                    },
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": f"data:image/png;base64,{base64_image}"
                        },
                    },
                ],
            }
        ],
        "max_tokens": 10,
    }
    try:
        response = _REQUESTS.post(
            GAPGPT_ENDPOINT, headers=headers, json=payload, timeout=30
        )
        if response.status_code == 200:
            result = response.json()
            raw_text = result["choices"][0]["message"]["content"].strip()
            clean_captcha = re.sub(r"[^a-zA-Z0-9]", "", raw_text)
            log(f"captcha solved via GapGPT: {clean_captcha!r}")
            return clean_captcha
        else:
            log(f"GapGPT API error {response.status_code}: {response.text}")
    except Exception as exc:  # pragma: no cover - defensive
        log(f"GapGPT request failed: {exc}")

    return None


def solve_captcha(png: bytes):
    """
    Orchestrates the captcha solvers in fallback order (matching app.py):
    ddddocr (only when its result is 4-5 alphanumeric chars), then GapGPT.
    Returns the proposed text (possibly empty) so the caller can prompt a human
    when neither automated solver succeeds.
    """
    if HAS_DDDDOCR:
        try:
            res = ocr_engine.classification(png)
            if isinstance(res, bytes):  # pragma: no cover - defensive
                res = res.decode("utf-8", errors="ignore")
            clean_captcha = re.sub(r"[^a-zA-Z0-9]", "", res)
            if 4 <= len(clean_captcha) <= 5:
                log(f"captcha solved via ddddocr: {clean_captcha!r}")
                return clean_captcha
            else:
                log(f"ddddocr result has invalid length ({clean_captcha!r}); switching to GapGPT...")
        except Exception as exc:  # pragma: no cover - defensive
            log(f"ddddocr failed: {exc}")

    return solve_with_gapgpt(png)
# ----------------------------------------------------------------------------
# Small DOM helpers (defensive: try several candidate selectors).
# ----------------------------------------------------------------------------
def pick(page, candidates, predicate=None):
    """Return the first matching element from a list of selector candidates."""
    for sel in candidates:
        try:
            els = page.query_selector_all(sel)
            for el in els:
                if predicate is None or predicate(el):
                    return el
        except Exception:
            continue
    return None


def fill_by_hint(page, value, *label_hints, field_selectors=()):
    """Fill an input located via aria-label/placeholder/name text hints."""
    value = (value or "").strip()
    fallback = None
    for sel in field_selectors:
        fallback = fallback or pick(page, [sel])
    for hint in label_hints:
        for sel in (
            f"input[aria-label*='{hint}']",
            f"input[placeholder*='{hint}']",
            f"input[name*='{hint}']",
            f"textarea[aria-label*='{hint}']",
        ):
            el = pick(page, [sel])
            if el is not None and el.is_visible():
                el.click()
                el.fill(value)
                return True
    if fallback is not None and fallback.is_visible() and not (hasattr(fallback, "is_readonly") and fallback.is_readonly()):
        fallback.click()
        fallback.fill(value)
        return True
    return False


def click_by_text(page, text, *, exact=False, selectors=("button",)):
    """Click a button/link whose inner text matches `text`."""
    for tag in selectors:
        for el in page.query_selector_all(tag):
            try:
                inner = (el.inner_text() or "").strip()
                match = (inner == text) if exact else (text in inner)
                if match and el.is_visible():
                    el.click()
                    return True
            except Exception:
                continue
    return False


def wait_for_page_load(page, timeout_ms: int = 15000):
    try:
        page.wait_for_load_state("domcontentloaded", timeout=timeout_ms)
    except Exception:
        pass
    time.sleep(0.5)


def _current_url(page) -> str:
    try:
        return page.url or ""
    except Exception:
        return ""


def _is_already_authenticated(page) -> bool:
    """
    Detect an already-active session (typically from a cached `browser_data`
    profile). When the portal considers the session valid, a request to the SSO
    `Login` page is redirected to a dashboard / to tp.tax.gov.ir and no new SMS
    OTP is needed.
    """
    url = _current_url(page)
    if "tp.tax.gov.ir" in url:
        return True
    if "my.tax.gov.ir" in url and "/login" not in url.lower():
        return True
    return False


def _is_on_login_page(page) -> bool:
    """True when the browser currently sits on the SSO login page."""
    url = _current_url(page)
    return "my.tax.gov.ir" in url and "/login" in url.lower()


def _is_authenticated(page) -> bool:
    """True when the browser is ALREADY authenticated on the output-files page.

    Per the task spec this means the URL *stays* on `outputFiles` (the portal did
    NOT redirect us to the SSO login) AND no password input is present/visible
    (i.e. we are past the credential form). Only when this returns False should
    the login / SMS-OTP flow be executed.
    """
    url = _current_url(page)
    if "outputFiles" not in url:
        return False
    try:
        pw = page.query_selector("input[type='password']")
        if pw is not None and pw.is_visible():
            return False
    except Exception:
        pass
    return True


def _has_login_error(page) -> bool:
    """Detect the portal's error toasts/alerts after a failed login submit."""
    try:
        body = page.inner_text("body")
    except Exception:
        return False
    return any(marker in body for marker in LOGIN_ERROR_MARKERS)


def _click_captcha_refresh(page) -> bool:
    """
    Click the captcha refresh icon so the portal serves a new captcha image.
    Returns True when a refresh control was found and clicked. Defensive: the
    portal may use any of several ids/classes, so a list of candidates is tried.
    """
    for sel in CAPTCHA_REFRESH_SELECTORS:
        try:
            el = page.query_selector(sel)
            if el is not None and el.is_visible():
                el.click()
                log(f"captcha refresh clicked via selector: {sel}")
                return True
        except Exception:
            continue
    log("no captcha refresh control found; will re-read whatever image is shown")
    return False


def _wait_for_otp_field(page, timeout_seconds: float = 15.0) -> bool:
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        if _has_login_error(page):
            log("login error toast/alert detected while waiting for OTP")
            return False

        try:
            body_text = page.inner_text("body")
            # اگر عبارات اختصاصی صفحه OTP روی صفحه ظاهر شد
            if any(kw in body_text for kw in ("کد تأیید", "ارسال پیامک", "رمز یکبار مصرف", "کد ارسال شده")):
                # مطمئن می‌شویم کادر ورود شناسه ملی دیگر روی صفحه نیست یا کادر اختصاصی OTP ظاهر شده
                el = pick(page, OTP_FIELD_SELECTORS, predicate=lambda e: e.is_visible())
                if el is not None or "Login" not in page.url:
                    log("OTP page/field explicitly verified")
                    return True
        except Exception:
            pass

        time.sleep(0.5)
    log("timed out waiting for the OTP field to appear")
    return False
# ----------------------------------------------------------------------------
# Login.
# ----------------------------------------------------------------------------
def handle_login_and_captcha(page, username: str, password: str) -> dict:
    """
    Login handler ported from app.py: open the SSO login page (my.tax.gov.ir),
    fill username/password with app.py's selectors, solve the captcha (ddddocr ->
    GapGPT, see solve_captcha), fill it and submit via Enter to trigger the SMS
    OTP. The OTP itself is entered separately by the consumer (tax_exporter's
    `complete` action), so the automated SMS webhook server from app.py is
    intentionally excluded.

    Returns the same dict contract as the caller expects:
      {"status": STATUS_ALREADY_LOGGED_IN | STATUS_OTP_SENT | STATUS_OTP_NOT_SENT,
       "challengeToken": str, "maskedPhone": str|None}
    """
    # 1. Open the SSO login page.
    log("opening SSO login page")
    page.goto(f"{LOGIN_BASE}/Login", wait_until="domcontentloaded", timeout=45000)
    wait_for_page_load(page)

    # 1b. Detect an already-active session (cached browser_data). If the portal
    #     redirected us to a dashboard / tp.tax.gov.ir, no new SMS is needed.
    if _is_already_authenticated(page):
        log("session already active; no SMS OTP required")
        return {"status": STATUS_ALREADY_LOGGED_IN}

    # 2. Fill username + password using app.py's selectors.
    page.locator("input[type='text']").first.fill(username)
    page.locator("input[type='password']").first.fill(password)
    log(f"username field filled ({len(username)} chars); "
        f"password field filled ({len(password)} chars)")

    # 3. Locate the captcha image (app.py selectors + ancestor-based fallback).
    captcha_img = pick(page, CAPTCHA_IMG_SELECTORS)
    if captcha_img is None or not captcha_img.is_visible():
        log("primary captcha selector not visible; using ancestor-based fallback")
        captcha_img = page.locator("img").filter(
            has=page.locator(
                "xpath=ancestor::*[contains(@class, 'captcha') or"
                " contains(@class, 'image')]"
            )
        ).first

    captcha_bytes = captcha_img.screenshot() or b""
    captcha_text = solve_captcha(captcha_bytes)

    if not captcha_text:
        log("captcha could not be solved; aborting login")
        return {"status": STATUS_OTP_NOT_SENT}

    # 4. Fill the captcha input and submit via Enter (app.py flow).
    captcha_input = page.locator(CAPTCHA_INPUT_SELECTOR).first
    captcha_input.fill(captcha_text)
    log(f"captcha solved ({captcha_text}) and filled; submitting login form")
    captcha_input.press("Enter")
    wait_for_page_load(page)

    # 5. Strict validation: the OTP field must actually appear (SMS triggered).
    if not _wait_for_otp_field(page, timeout_seconds=8.0):
        log("no OTP field appeared after submit -> assume captcha/login rejected")
        try:
            page.screenshot(path="debug_captcha_failed.png", full_page=True)
            log("saved debug_captcha_failed.png")
        except Exception as exc:
            log(f"could not save debug_captcha_failed.png: {exc}")
        return {"status": STATUS_OTP_NOT_SENT}

    # 6. The OTP field appeared -> the SMS was really triggered. Capture proof.
    try:
        page.screenshot(path="debug_otp_triggered.png", full_page=True)
        log("saved debug_otp_triggered.png")
    except Exception as exc:
        log(f"could not save debug_otp_triggered.png: {exc}")

    return {
        "challengeToken": _extract_token(page),
        "maskedPhone": _extract_phone(page),
        "status": STATUS_OTP_SENT,
    }


def _extract_token(page) -> str:
    """Best-effort challenge/session token from URL or a hidden field."""
    try:
        if "challenge=" in page.url:
            return page.url.split("challenge=")[1].split("&")[0]
        for sel in ("input[name='challenge']", "input[name='challengeToken']"):
            el = pick(page, [sel])
            if el is not None:
                return (el.get_attribute("value") or "").strip()
    except Exception:
        pass
    return ""


def _extract_phone(page) -> "str | None":
    try:
        for el in page.query_selector_all("div, span, p"):
            try:
                t = (el.inner_text() or "").strip()
                if "پیامک" in t and any(ch.isdigit() for ch in t):
                    digits = "".join(ch for ch in t if ch.isdigit() or ch in "*-")
                    return f"***{digits[-4:]}" if digits else None
            except Exception:
                continue
    except Exception:
        pass
    return None