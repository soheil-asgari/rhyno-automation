/* Tax AI slide-over widget — global assistant */
(function () {
    'use strict';

    var launcher = document.getElementById('taxAiLauncher');
    var overlay = document.getElementById('taxAiOverlay');
    var closeBtn = document.getElementById('taxAiClose');
    var messagesEl = document.getElementById('taxAiMessages');
    var inputEl = document.getElementById('taxAiInput');
    var sendBtn = document.getElementById('taxAiSend');
    if (!launcher || !overlay) return;

    var token = window.__taxAntiForgeryToken || '';

    function appendMsg(text, isUser) {
        var wrap = document.createElement('div');
        wrap.className = 'tax-widget-msg ' + (isUser ? 'tax-widget-user' : 'tax-widget-ai');
        var bubble = document.createElement('div');
        bubble.className = 'tax-widget-bubble';
        bubble.textContent = text;
        wrap.appendChild(bubble);
        messagesEl.appendChild(wrap);
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }

    function open() {
        overlay.classList.add('open');
        overlay.setAttribute('aria-hidden', 'false');
        inputEl && inputEl.focus();
    }
    function close() {
        overlay.classList.remove('open');
        overlay.setAttribute('aria-hidden', 'true');
    }

    function toggle() {
        if (overlay.classList.contains('open')) {
            close();
        } else {
            open();
        }
    }

    launcher.addEventListener('click', toggle);
    if (closeBtn) closeBtn.addEventListener('click', close);

    function send() {
        if (!inputEl) return;
        var q = inputEl.value.trim();
        if (!q) return;
        appendMsg(q, true);
        inputEl.value = '';
        appendMsg('در حال جستجو...', false);

        fetch('/TaxAnalytics/Ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ question: q })
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var last = messagesEl.lastElementChild;
                if (last) messagesEl.removeChild(last);
                appendMsg(data.answer || 'پاسخی دریافت نشد.', false);
            })
            .catch(function () {
                var last = messagesEl.lastElementChild;
                if (last) messagesEl.removeChild(last);
                appendMsg('خطا در برقراری ارتباط.', false);
            });
    }

    if (sendBtn) sendBtn.addEventListener('click', send);
    if (inputEl) inputEl.addEventListener('keydown', function (e) { if (e.key === 'Enter') send(); });
})();