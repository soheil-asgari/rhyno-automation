(() => {
    const usersEl = document.getElementById('letterUsersData');
    const cfgEl = document.getElementById('letterReplyConfig');
    window.usersList = usersEl ? JSON.parse(usersEl.textContent) : [];
    let replyConfig = cfgEl ? JSON.parse(cfgEl.textContent) : {};
    window.letterReplyConfig = {
        isReplyMode: !!replyConfig.isReplyMode,
        replyToLetterId: replyConfig.replyToLetterId ?? null,
        defaultSubject: replyConfig.defaultSubject ?? '',
        defaultReceiverId: replyConfig.defaultReceiverId ?? ''
    };
})();
