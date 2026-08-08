/* Tax Analytics AI Assistant — chat page */
(function () {
    'use strict';

    var messagesEl = document.getElementById('taxChatMessages');
    var inputEl = document.getElementById('taxChatInput');
    var sendBtn = document.getElementById('taxChatSend');
    if (!messagesEl || !inputEl || !sendBtn) return;

    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    function appendMsg(text, isUser) {
        var wrap = document.createElement('div');
        wrap.className = 'tax-msg ' + (isUser ? 'tax-msg-user' : 'tax-msg-ai');
        var bubble = document.createElement('div');
        bubble.className = 'tax-msg-bubble';
        bubble.textContent = text;
        wrap.appendChild(bubble);
        messagesEl.appendChild(wrap);
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }

    function send() {
        var q = inputEl.value.trim();
        if (!q) return;
        appendMsg(q, true);
        inputEl.value = '';
        appendMsg('در حال جستجو...', false);

        fetch('/TaxAnalytics/Ask', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
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
                appendMsg('خطا در برقراری ارتباط. لطفاً دوباره تلاش کنید.', false);
            });
    }

    sendBtn.addEventListener('click', send);
    inputEl.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') send();
    });
})();