# Licensing & Module Entitlement System — Architecture & Task Plan

> **Goal:** A license token that locks the app to a specific server (hardware fingerprint), specifies which modules are enabled (e.g. Contracts, Financial), and caps the number of users — validated primarily offline, with periodic online check-in for renewal/revocation.

---

## 0. Architecture Overview (read this before starting tasks)

### Components you're building
1. **License Token** — a signed JSON payload (JWT) containing customer info, licensed modules, max users, expiry, and a hardware fingerprint. Signed with a **private key that never leaves your possession**.
2. **App-side License Middleware** — lives inside Rhyno Automation itself. Verifies the token's signature (using only the **public** key, shipped inside the app), checks hardware fingerprint match, checks expiry, gates module access, enforces user-count limits.
3. **Hardware Fingerprint Generator** — a small utility inside the app that produces a stable machine ID, used both when requesting a license and when validating one.
4. **License Server (yours)** — a small central API you host, used for: issuing licenses, periodic heartbeat/renewal checks from deployed instances, and remote revocation. Deployed instances call this periodically but keep working offline (with a grace period) if it's unreachable.
5. **Internal License-Issuing Tool** — a CLI or small internal web tool (used only by your team, never shipped to clients) that generates signed license files given customer/module/user-count/fingerprint input.
6. **Client-facing License UI** — a settings page inside the app where the client admin sees license status (modules, expiry, users used/allowed), can request a fingerprint code to send you, and can upload/activate a new license file.

### Critical security rule
> **The private signing key must NEVER be included in the app that's installed on the client's server.** Only the public key ships with the app (for verification). The private key stays only in your License Server / issuing tool, under your control. If this boundary is violated, anyone with access to the client's server binaries could forge their own licenses.

### Data flow summary
```
[Client server]                          [Your License Server]
1. App generates hardware fingerprint
2. Admin copies fingerprint code
3. Admin sends fingerprint + desired
   modules/user-count to you (email/portal)
                                           4. You run the issuing tool →
                                              generates signed license
                                              (JWT) bound to that
                                              fingerprint
5. Admin uploads the license file
   into the app's License settings page
6. App verifies signature (public key),
   fingerprint match, expiry → activates
7. App periodically (e.g. daily) calls
   your License Server's /heartbeat  ---->  8. Server checks revocation
   endpoint                                     status, returns OK / revoked /
                                                 updated license
9. If server unreachable, app keeps
   working using last successful
   validation for a grace period
   (e.g. 15-30 days), then shows
   warnings / restricts access if
   grace period expires
```

---

## Phase 1 — License Token Design & Cryptography

### ☑ Task 1.1 — Define the license token schema and signing mechanism (Completed)
```
I'm building a licensing system for an on-prem ASP.NET Core application (Rhyno Automation). Design and implement the license token as a signed JWT with the following claims:

- lic_id (GUID, unique per issued license)
- customer_name
- customer_id (your internal customer reference)
- modules (array of strings, e.g. ["Contracts", "Financial"])
- max_users (integer — total named/active user accounts allowed across the licensed modules; also support optional per-module limits as a dictionary, e.g. {"Contracts": 20, "Financial": 10}, falling back to the global max_users if a module-specific limit isn't set)
- hardware_fingerprint (string, the target server's fingerprint hash — see Task 1.2)
- issued_at / expires_at (UTC timestamps; support a "perpetual" flag for licenses with no expiry)
- license_type (e.g. "trial", "subscription", "perpetual")

Use RS256 (asymmetric RSA signing) so the app only ever needs the PUBLIC key to verify tokens, while the PRIVATE key stays solely in the issuing tool / license server (never shipped with the app).

Implement:
1. A small standalone .NET console project (LicenseIssuer) — NOT part of the main app — that takes these fields as input (via CLI args or a simple config file) and outputs a signed .lic license file (base64 JWT). This project holds the private key (loaded from a local file, e.g. license-private.pem, gitignored, never committed).
2. A LicenseValidator class inside the main app (Services/Licensing/LicenseValidator.cs) that loads the embedded PUBLIC key, verifies a given license file's signature and expiry, and returns a strongly-typed LicenseInfo object (or a validation failure reason: invalid signature, expired, malformed, fingerprint mismatch — checked in a separate task).

Show me the JWT payload structure and confirm the private key never gets referenced anywhere inside the main OfficeAutomation project — only in the separate LicenseIssuer tool.
```

### ☐ Task 1.2 — Build the hardware fingerprint generator
```
Implement a HardwareFingerprintService (Services/Licensing/HardwareFingerprintService.cs) inside the main app that generates a stable, unique identifier for the server it's running on, suitable for license locking. 

Requirements:
1. Combine multiple stable hardware/OS identifiers so the fingerprint survives things like network changes but changes if the app is moved to a different physical/virtual machine: e.g. Windows MachineGuid (from registry HKLM\SOFTWARE\Microsoft\Cryptography), primary disk volume serial number, and CPU identifier (if reliably available cross-platform; if this only needs to run on Windows Server/IIS, Windows-specific APIs are fine — confirm the deployment OS with me if unsure, otherwise assume Windows Server).
2. Hash the combined raw values (SHA-256) into a single fingerprint string, and also produce a short, human-readable "activation code" (e.g. grouped into blocks like XXXX-XXXX-XXXX-XXXX) that an admin can read aloud or copy-paste to send to us.
3. Add an admin-only page/action (or extend the License settings page created in Phase 3) that displays this fingerprint/activation code so the client's admin can retrieve and send it to us when requesting a license.
4. Make sure the fingerprint is deterministic (same server always produces the same fingerprint) across app restarts, but do NOT persist it in a way that could be tampered with to fake a different machine.

Test by running the fingerprint generator twice (e.g. after an app restart) and confirming identical output.
```

---

## Phase 2 — App-Side License Enforcement

### ☐ Task 2.1 — License validation service + startup check
```
Building on LicenseValidator (Task 1.1) and HardwareFingerprintService (Task 1.2), implement full validation logic in Services/Licensing/LicenseValidator.cs:

1. ValidateAsync(licenseFileContent) should:
   a. Verify JWT signature using the embedded public key.
   b. Check expires_at against current UTC time (unless perpetual).
   c. Compute the current server's hardware fingerprint and compare against the license's hardware_fingerprint claim — reject with a clear "fingerprint mismatch" reason if they don't match.
   d. Return a LicenseInfo object (modules, max_users/per-module limits, expiry, license_type, days_remaining) or a typed failure reason (Invalid, Expired, FingerprintMismatch, Malformed, Revoked).
2. On application startup (Program.cs), load the currently activated license file (stored path, e.g. App_Data/license/current.lic) and validate it. Cache the LicenseInfo in a singleton ILicenseContext service, refreshed:
   - On every startup
   - On demand after a new license is uploaded/activated
   - Periodically in the background (see Phase 3 heartbeat task) — no need to re-read from disk every request.
3. If no valid license is present (missing, expired, invalid, or fingerprint mismatch), the app should still start (don't hard-crash) but operate in a "Locked" mode: all module functionality blocked except the License activation settings page itself, with a clear banner explaining why (e.g. "License expired on 1403/07/01 — please renew" or "No valid license found — please activate").

Show me how ILicenseContext is registered and consumed, and confirm the app doesn't crash on a missing/invalid license file — it should degrade gracefully to the locked state.
```

### ☐ Task 2.2 — Module-level access gating
```
Using the ILicenseContext from Task 2.1, add an authorization mechanism that restricts access to entire modules based on the license's `modules` claim:

1. Create a [RequiresModule("Contracts")] (or "Financial", etc.) action/controller filter attribute.
2. Apply it to ContractsController, ContractGuaranteesController, and any other controllers under the Contracts module, and identify + apply it to the Financial module's controllers as well (search the codebase for the financial module's controller(s) and apply consistently).
3. If a user tries to access a module not included in the active license, redirect to a clear "Module Not Licensed" page (not a raw 403) explaining that this module isn't part of their current license and who to contact to add it.
4. Also hide/disable navigation menu items (sidebar links) for modules not included in the license, so users don't even see entry points they can't use.
5. Confirm: with a test license that only includes ["Contracts"], the Financial module's nav links disappear and direct URL access redirects to the "Module Not Licensed" page, while Contracts still works normally.
```

### ☐ Task 2.3 — User-count enforcement
```
Using ILicenseContext's max_users (global and/or per-module limits from Task 1.1), enforce user-count limits:

1. Decide and implement based on "named users" (i.e. count of active/enabled user accounts assigned to a given module's roles/permissions), not concurrent sessions — confirm this matches how user roles/permissions are currently modeled in this app (check the existing Users/Roles system before implementing) — flag me if concurrent-session-based counting would fit better given the existing architecture.
2. When an admin tries to create a new user account or assign an existing user to a licensed module's role, check the current count of users already assigned to that module against the license's limit for that module (or the global max_users if no per-module limit is set). Block the action with a clear error message ("شما به سقف مجاز کاربران این ماژول رسیده‌اید: X از Y") if it would exceed the limit.
3. Add a small widget/section on the License status page (Phase 3) showing current usage vs. limit per module (e.g. "Contracts: 8 / 20 users").
4. Handle the edge case where a license is downgraded (new license has a lower max_users than current active user count) — don't auto-delete/disable existing users, but block adding new ones until the count is back under the new limit, and show a clear warning banner to the admin.
```

---

## Phase 3 — License Server (Hybrid Online Component)

### ☐ Task 3.1 — Minimal license server API
```
Build a small, separate ASP.NET Core Web API project (LicenseServer) — hosted by us, not shipped to clients — with:

1. POST /api/licenses/issue — (internal/admin-authenticated only) creates a new license record in the server's own database (customer, modules, max_users, fingerprint, expiry, status: active/revoked) and returns the signed .lic file (reuse the signing logic from the LicenseIssuer tool in Task 1.1 — refactor the signing code into a shared library referenced by both the CLI tool and this API, so there's one source of truth for how licenses are signed).
2. POST /api/licenses/heartbeat — called periodically by each deployed app instance with its lic_id and fingerprint. Returns { status: "active" | "revoked" | "updated", updatedLicenseFile?: "..." }. If the license record was revoked or modified (e.g. renewed with a new expiry) since the last heartbeat, return the updated signed license file so the client app can automatically pick it up.
3. A minimal admin authentication (this API is for your internal team only — reuse a simple API key or basic admin login, doesn't need to be elaborate).
4. A simple database (SQLite is fine for this scale) storing: LicenseId, CustomerName, Modules, MaxUsers (+ per-module), Fingerprint, IssuedAt, ExpiresAt, Status (Active/Revoked), LastHeartbeatAt.

Propose the exact request/response JSON shapes before implementing so I can confirm.
```

### ☐ Task 3.2 — App-side heartbeat client with offline grace period
```
In the main app, add a background hosted service (LicenseHeartbeatService) that:
1. Runs on a schedule (e.g. once every 24 hours, plus once on startup) and calls the License Server's /api/licenses/heartbeat endpoint with the current license's lic_id and fingerprint.
2. On success:
   - If status is "active", update a "LastSuccessfulHeartbeatAt" timestamp in ILicenseContext / local storage.
   - If status is "updated", save the new license file and re-validate/reload it automatically (no app restart required).
   - If status is "revoked", immediately move the app into the "Locked" state (same as an invalid license in Task 2.1) and show a clear message.
3. On failure to reach the server (network error, timeout, server down):
   - Do NOT lock the app immediately. Keep operating normally using the last successful offline validation, as long as it's within a grace period (default 21 days since LastSuccessfulHeartbeatAt — make this configurable).
   - Show an increasingly visible warning banner as the grace period gets closer to expiring (e.g. "⚠️ License server unreachable for 15 days — please check connectivity, access will be restricted in 6 days").
   - Once the grace period is fully exceeded with no successful heartbeat, move to the Locked state until connectivity is restored or a new license file is manually activated.
4. Make the heartbeat URL and grace period configurable via appsettings.json (so on-prem deployments can point to the right server URL, and so you can adjust the grace period per contract terms if needed).

Test by simulating: (a) normal heartbeat success, (b) server unreachable within grace period (app keeps working with warning), (c) server unreachable past grace period (app locks), (d) server returns "revoked" (app locks immediately).
```

---

## Phase 4 — Client-Facing License UI

### ☐ Task 4.1 — License status & activation settings page
```
Add a new admin-only settings page (Views/Settings/License.cshtml or similar, matching existing settings page patterns in the app) that shows:

1. Current license status: customer name, licensed modules (with clear badges), max users per module + current usage (from Task 2.3), expiry date (in Jalali) or "Perpetual", license type, and days remaining with a color-coded warning if under 30 days.
2. Last successful heartbeat timestamp and current connectivity status to the license server (green/yellow/red indicator based on the grace period logic from Task 3.2).
3. The server's hardware fingerprint / activation code (from Task 1.2) displayed clearly with a "Copy" button, plus instructions: "برای دریافت یا تمدید لایسنس، این کد را برای پشتیبانی ارسال کنید" (Send this code to support to receive or renew your license).
4. An "Activate / Upload New License" section: a file upload control accepting a .lic file. On upload, call LicenseValidator (Task 2.1), show clear success/failure feedback (including the specific reason if it fails: wrong fingerprint, expired, invalid signature), and if valid, activate it immediately (save to App_Data/license/current.lic, reload ILicenseContext) without requiring an app restart.
5. If the app is currently in "Locked" mode (Task 2.1), this page should still be fully accessible (it must be, since it's the only way to fix the problem) — make sure the [RequiresModule] gating from Task 2.2 explicitly excludes this settings page.

Test the full activation flow: with the app initially unlicensed/locked, upload a valid .lic file for this server's fingerprint and confirm the app immediately unlocks the correct modules without a restart.
```

---

## Phase 5 — Internal Tooling (Your Team Only — Never Shipped to Clients)

### ☐ Task 5.1 — Polish the internal license-issuing workflow
```
Improve the LicenseIssuer CLI tool (or build a very simple internal web UI on top of it, your choice — propose which is more practical given how many licenses you expect to issue) so that our team can:
1. Look up or enter a customer name, paste in the fingerprint/activation code the client sent us, select modules and per-module user limits from a simple form/prompt, and set expiry (or perpetual / trial with a preset short expiry like 14 days).
2. Generate the signed .lic file and, if practical, also register the license in the License Server's database (Task 3.1) in the same step, so issuing a license automatically makes it visible in our own records for future heartbeat/revocation management.
3. Add a "revoke license" command/action that marks a license as revoked in the License Server database, which will then propagate to the client's app on its next heartbeat (Task 3.2).
4. Add a "renew license" command/action that updates a license's expiry and re-signs it, so the client's app picks up the renewal automatically via heartbeat without needing a new manual file upload.

Confirm the private signing key file's location and access permissions are locked down appropriately (not in source control, restricted file permissions) before finishing this task.
```

---

## Suggested Build Order

| Order | Phase | Why |
|---|---|---|
| 1 | Phase 1 | Core crypto/token design — everything else depends on this |
| 2 | Phase 2 | Get offline enforcement fully working first — this alone is a sellable, shippable licensing system |
| 3 | Phase 4 (before Phase 3) | Build the activation UI while it's still simple (offline-only), so you can test Phase 2 end-to-end with real license files |
| 4 | Phase 3 | Add the online/hybrid layer once offline enforcement is proven solid |
| 5 | Phase 5 | Polish your own internal issuing workflow last, once you know what fields/flows you actually use in practice |

Once Phase 1, 2, and 4 are done, you already have a fully functional **offline, hardware-locked, module + user-limited license system** — good enough to ship. Phase 3 adds the ability to remotely renew/revoke after the fact, which matches what you asked for with the hybrid model.









