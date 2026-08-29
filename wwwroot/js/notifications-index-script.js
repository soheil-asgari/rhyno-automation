/**
 * Rhyno Notifications Center — Client-side Logic
 *
 * Provides:
 *   1. Mark single notification as read via AJAX
 *   2. Mark all notifications as read via AJAX
 *   3. Auto-update the "unread count" badge in header notification list via polling
 */
document.addEventListener("DOMContentLoaded", function () {
    "use strict";

    // ═══════════════════════════════════════════════════════════
    //  Mark Single as Read
    // ═══════════════════════════════════════════════════════════
    document.querySelectorAll(".mark-read-btn").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            var item = this.closest("[data-notification-id]");
            if (!item) return;
            var id = item.getAttribute("data-notification-id");
            if (!id) return;

            var token = document.querySelector('meta[name="request-verification-token"]');
            var antiforgery = token ? token.getAttribute("content") : "";

            fetch("/Notifications/MarkRead/" + id, {
// Fallback: try hidden input if meta tag not found
            if (!antiforgery) {
                var hiddenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                if (hiddenInput) antiforgery = hiddenInput.value;
            }
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": antiforgery },
                body: JSON.stringify({})
            })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.success) {
                    item.setAttribute("data-is-read", "true");
                    item.classList.remove("bg-sky-50/50", "dark:bg-sky-950/10");
                    var btn = item.querySelector(".mark-read-btn");
                    if (btn) btn.remove();
                }
            })
            .catch(function () {});
        });
    });

    // ═══════════════════════════════════════════════════════════
    //  Mark All as Read
    // ═══════════════════════════════════════════════════════════
    var markAllBtn = document.getElementById("markAllReadBtn");
    if (markAllBtn) {
        markAllBtn.addEventListener("click", function () {
            var token = document.querySelector('meta[name="request-verification-token"]');
            var antiforgery = token ? token.getAttribute("content") : "";
            // Fallback: try hidden input if meta tag not found
            if (!antiforgery) {
                var hiddenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                if (hiddenInput) antiforgery = hiddenInput.value;
            }

            fetch("/Notifications/MarkAllRead", {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": antiforgery },
                body: JSON.stringify({})
            })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.success) {
                    document.querySelectorAll("[data-notification-id]").forEach(function (item) {
                        item.setAttribute("data-is-read", "true");
                        item.classList.remove("bg-sky-50/50", "dark:bg-sky-950/10");
                        var btn = item.querySelector(".mark-read-btn");
                        if (btn) btn.remove();
                    });
                }
            })
            .catch(function () {});
        });
    }
});
