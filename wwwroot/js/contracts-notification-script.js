/*
 * Rhyno Contracts — Notification Bell & Alert Panel
 *
 * Provides:
 *   1. AJAX polling for unread notification count (updates badge every 60s)
 *   2. Mark single notification as read directly from header dropdown and contract panel
 *   3. Mark all as read from header dropdown panel
 *   4. Contract-specific notification panel on the dashboard
 */
(function() {
    "use strict";
    var POLL_MS = 60000;
    var UNREAD_URL = "/Notifications/UnreadCount";
    var MARK_READ_URL = "/Notifications/MarkRead/";
    var MARK_ALL_URL = "/Notifications/MarkAllRead";
    var NTFY_API_URL = "/Notifications/GetNotificationsJson?module=Contracts&take=5";
    function getToken() {
        var m = document.querySelector("meta[name=\"request-verification-token\"]");
        return m ? m.getAttribute("content") : "";
    }
    function pollUnred() {
        var b = document.getElementById("notificationBadge");
        if (!b) return;
        fetch(UNREAD_URL, {method:"GET",headers:{"Accept":"application/json"}})
        .then(function(r){return r.json()})
        .then(function(d){
            var c = d.count || 0;
            b.style.display = c > 0 ? "flex" : "none";
            if (c > 0) b.textContent = c.toLocaleString("fa-IR");
            var pc = document.getElementById("notificationPanelCount");
            if (pc) pc.textContent = c.toLocaleString("fa-IR") + " \u0645\u0648\u0631\u062F";
        }).catch(function(){});
    }
    function markRead(btn) {
        var el = btn.closest("[data-ntfy-id]");
        if (!el) return;
        var id = el.getAttribute("data-ntfy-id");
        if (!id) return;
        fetch(MARK_READ_URL + id, {method:"POST",headers:{"Content-Type":"application/json","RequestVerificationToken":getToken()},body:"{}"})
        .then(function(r){return r.json()})
        .then(function(d){
            if (!d.success) return;
            el.classList.remove("ntfy-unread");
            var dot = el.querySelector(".ntfy-unread-dot");
            if (dot) dot.remove();
            var mb = el.querySelector(".ntfy-mark-read");
            if (mb) mb.remove();
            pollUnred();
        }).catch(function(){});
    }
    function markAllRead() {
        fetch(MARK_ALL_URL, {method:"POST",headers:{"Content-Type":"application/json","RequestVerificationToken":getToken()},body:"{}"})
        .then(function(r){return r.json()})
        .then(function(d){
            if (!d.success) return;
            document.querySelectorAll(".ntfy-unread").forEach(function(el) {
                el.classList.remove("ntfy-unread");
                var dot = el.querySelector(".ntfy-unread-dot");
                if (dot) dot.remove();
                var mb = el.querySelector(".ntfy-mark-read");
                if (mb) mb.remove();
            });
            pollUnred();
        }).catch(function(){});
    }
    function esc(s) {
        if (!s) return "";
        var d = document.createElement("div");
        d.appendChild(document.createTextNode(s));
        return d.innerHTML;
    }
    function loadPanel() {
        var c = document.getElementById("contractNotificationsPanel");
        if (!c) return;
        c.innerHTML = "<div class=\"flex items-center gap-2 py-3 px-2\"><span class=\"rhyno-spinner\" style=\"width:14px;height:14px;border-width:2px;\"></span><span class=\"text-[10px] text-slate-400\">" + String.fromCharCode(1583,1585,32,1581,1575,1600,32,1576,1575,1581,1711,1584,1575,1581,1740,32,1575,1593,1604,1575,1606,8204,1607,1575,46,46,46) + "</span></div>";
        fetch(NTFY_API_URL,{method:"GET",headers:{"Accept":"application/json"}})
        .then(function(r){return r.json()})
        .then(function(d){
            if (!d || !d.items || d.items.length === 0) {
                c.innerHTML = "<div class=\"py-6 text-center text-[11px] text-slate-400 dark:text-slate-500\"><i class=\"bi bi-bell-slash ml-1 text-lg\"></i><span class=\"block mt-1\">" + String.fromCharCode(1607,1740,1670,32,1575,1593,1604,1575,1606,1740,32,1576,1581,1575,1740,32,1602,1581,1575,1581,1583,1575,1583,1607,1575,32,1608,1580,1608,1583,32,1606,1584,1575,1583,1583,46) + "</span></div>";
                return;
            }
            var h = "";
            d.items.forEach(function(item) {
                var tb = item.cssTone ==="danger" ? "bg-rose-100 dark:bg-rose-950/30 text-rose-600 dark:text-rose-400" : item.cssTone ==="warning" ? "bg-amber-100 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400" : item.cssTone ==="success" ? "bg-emerald-100 dark:bg-emerald-950/30 text-emerald-600 dark:text-emerald-400" : "bg-sky-100 dark:bg-sky-950/30 text-sky-600 dark:text-sky-400";
                var ib = item.isRead ? "" : "bg-sky-50/50 dark:bg-sky-950/10 ntfy-unread";
                h += "<div class=\"flex items-start gap-2.5 px-2 py-2.5 transition hover:bg-slate-50/50 dark:hover:bg-slate-800/30 " + ib + "\" data-ntfy-id=\"" + item.id + "\"><div class=\"mt-0.5 h-5 w-5 rounded-full flex items-center justify-center flex-shrink-0 text-xs " + tb + "\"><i class=\"bi " + item.icon + "\" style=\"font-size:9px;\"></i></div><div class=\"min-w-0 flex-1\"><div class=\"flex items-start justify-between gap-1\"><strong class=\"text-[11px] text-slate-900 dark:text-white leading-tight\">" + esc(item.title) + "</strong>" + (!item.isRead ? "<span class=\"ntfy-unread-dot mt-0.5 h-1.5 w-1.5 rounded-full bg-sky-500 flex-shrink-0\"></span>" : "") + "</div><p class=\"text-[10px] text-slate-500 dark:text-slate-400 mt-0.5 leading-relaxed line-clamp-2\">" + esc(item.message) + "</p><div class=\"flex items-center gap-2 mt-1\"><span class=\"text-[8px] text-slate-400 dark:text-slate-500\">" + item.createdAtText + "</span>" + (!item.isRead ? "<button type=\"button\" class=\"ntfy-mark-read text-[8px] text-sky-600 dark:text-sky-400 hover:underline border-0 bg-transparent cursor:pointer px-0 py-0\">" + String.fromCharCode(1593,1604,1575,1605,32,1582,1608,1575,1606,1583,606) + "</button>" : "") + "</div></div></div>";
            });
            h += "<div class=\"border-t border-slate-100 dark:border-slate-800 px-2 py-2 text-center\"><a href=\"/Notifications?module=Contracts\" class=\"text-[10px] font-bold text-indigo-600 dark:text-indigo-400 no-underline hover:underline\">" + String.fromCharCode(1605,1588,1575,1607,1587,32,1607,1605,1607,32,1575,1593,1604,1575,1606,8204,1607,1575,1740,32,1602,1581,1575,1581,1583,1575,1583,1607,1575) + "</a></div>";
            c.innerHTML = h;
            c.querySelectorAll(".ntfy-mark-read").forEach(function(btn) {
                btn.addEventListener("click", function(e) { e.stopPropagation(); markRead(this); });
            });
        }).catch(function() {
            c.innerHTML = "<div class=\"py-6 text-center text-[11px] text-slate-400 dark:text-slate-500\"><i class=\"bi bi-exclamation-circle ml-1\"></i><span class=\"block mt-1\">" + String.fromCharCode(1582,1591,1575,32,1583,1581,32,1576,1575,1581,1711,1584,1575,1581,1740,32,1575,1593,1604,1575,1606,8204,1607,1575,46) + "</span></div>";
        });
    }
    document.addEventListener("DOMContentLoaded", function() {
        pollUnred();
        setInterval(pollUnred, POLL_MS);
        var markAllBtn = document.getElementById("headerMarkAllRead");
        if (markAllBtn) {
            markAllBtn.addEventListener("click", function(e) { e.preventDefault(); markAllRead(); });
        }
        loadPanel();
        if (window.RhynoTabs && typeof window.RhynoTabs.open === "function") {
            var orig = window.RhynoTabs.open;
            window.RhynoTabs.open = function(url, title, icon) {
                orig(url, title, icon);
                if (url && url.indexOf("/Contracts/Dashboard") !== -1) setTimeout(loadPanel, 300);
            };
        }
    });
})();
