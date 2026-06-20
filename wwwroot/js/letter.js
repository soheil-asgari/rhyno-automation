document.addEventListener("DOMContentLoaded", () => {
    let myEditor;
    const draftKey = "rhyno.letter.draft";
    const autosaveDelay = 700;
    let autosaveTimer = null;

    ClassicEditor.create(document.querySelector('#editor'), {
        language: 'fa',
        contentsLangDirection: 'rtl'
    }).then(ed => {
        myEditor = ed;
        window.letterEditor = ed;
        loadDraft();
        ed.model.document.on('change:data', scheduleDraftSave);
    });

    const allUsers = window.usersList || [];
    const replyConfig = window.letterReplyConfig || {};
    const subjectInput = document.getElementById('subjectInput');
    const receiverSelect = document.getElementById('receiverSelect');
    const draftStatus = document.getElementById('draftStatus');
    const previewModal = document.getElementById('letterPreviewModal');
    const previewBody = document.getElementById('letterPreviewBody');

    if (replyConfig.isReplyMode && receiverSelect) {
        receiverSelect.value = replyConfig.defaultReceiverId || '';
    }

    if (replyConfig.isReplyMode && subjectInput) {
        subjectInput.value = replyConfig.defaultSubject || '';
    }

    const setDraftStatus = (text) => {
        if (draftStatus) {
            draftStatus.textContent = text;
        }
    };

    const triggerReceiverChange = () => {
        if (!receiverSelect) return;

        const id = receiverSelect.value;
        const receiverSpan = document.getElementById('display-receiver');
        const prefixSpan = document.getElementById('gender-prefix');
        const previewReceiver = document.getElementById('previewReceiver');

        if (!id) {
            if (receiverSpan) receiverSpan.innerText = '....................';
            if (prefixSpan) prefixSpan.innerText = 'جناب آقای / سرکار خانم';
            if (previewReceiver) previewReceiver.textContent = 'انتخاب نشده';
            return;
        }

        const user = allUsers.find(x => x.Id == id);
        if (!user) {
            return;
        }

        if (receiverSpan) receiverSpan.innerText = user.FullName;
        if (previewReceiver) previewReceiver.textContent = user.FullName || 'انتخاب نشده';

        const g = (user.Gender || '').trim();
        if (prefixSpan) {
            prefixSpan.innerText =
                g === 'Male' ? 'جناب آقای' :
                    g === 'Female' ? 'سرکار خانم' :
                        g === 'Department' ? 'واحد محترم' :
                            'جناب آقای / سرکار خانم';
        }
    };

    const scheduleDraftSave = () => {
        if (!myEditor) return;
        clearTimeout(autosaveTimer);
        setDraftStatus("در حال ذخیره پیش‌نویس...");
        autosaveTimer = setTimeout(saveDraft, autosaveDelay);
    };

    const saveDraft = () => {
        if (!myEditor) return;
        const payload = {
            subject: subjectInput?.value || "",
            receiverId: receiverSelect?.value || "",
            body: myEditor.getData() || "",
            updatedAt: new Date().toISOString()
        };
        localStorage.setItem(draftKey, JSON.stringify(payload));
        setDraftStatus("پیش‌نویس ذخیره شد");
    };

    const loadDraft = () => {
        try {
            const raw = localStorage.getItem(draftKey);
            if (!raw) return;

            const draft = JSON.parse(raw);
            if (draft.receiverId && receiverSelect && !receiverSelect.value) {
                receiverSelect.value = draft.receiverId;
            }
            if (draft.subject && subjectInput && !subjectInput.value) {
                subjectInput.value = draft.subject;
            }
            if (draft.body && myEditor?.setData) {
                myEditor.setData(draft.body);
            }
            triggerReceiverChange();
            triggerSubjectChange();
            setDraftStatus("پیش‌نویس بازیابی شد");
        } catch {
            setDraftStatus("بازیابی پیش‌نویس ناموفق بود");
        }
    };

    const triggerSubjectChange = () => {
        if (!subjectInput) return;
        const value = subjectInput.value || '....................';
        const subjectDisplay = document.getElementById('display-subject');
        const previewSubject = document.getElementById('previewSubject');
        if (subjectDisplay) subjectDisplay.innerText = value;
        if (previewSubject) previewSubject.textContent = subjectInput.value || '-';
    };

    if (receiverSelect) {
        receiverSelect.addEventListener('change', () => {
            triggerReceiverChange();
            scheduleDraftSave();
        });
    }

    if (subjectInput) {
        subjectInput.addEventListener('input', () => {
            triggerSubjectChange();
            scheduleDraftSave();
        });

        triggerSubjectChange();
    }

    triggerReceiverChange();

    const replyHidden = document.getElementById('hReplyToLetterId');
    if (replyHidden && replyConfig.replyToLetterId != null) {
        replyHidden.value = replyConfig.replyToLetterId;
    }

    const renderPreview = () => {
        if (!previewModal || !previewBody || !myEditor) return;
        const receiver = document.getElementById('display-receiver')?.innerText || '....................';
        const prefix = document.getElementById('gender-prefix')?.innerText || '';
        const subject = subjectInput?.value || '....................';
        const body = myEditor.getData();
        previewBody.innerHTML = `
            <div class="paper-preview">
                <p class="mb-2"><strong>گیرنده:</strong> ${prefix} ${receiver}</p>
                <p class="mb-3"><strong>موضوع:</strong> ${subject}</p>
                <div class="preview-render">${body}</div>
            </div>`;
        previewModal.classList.remove('d-none');
        previewModal.setAttribute('aria-hidden', 'false');
    };

    document.getElementById('btnPreviewLetter')?.addEventListener('click', renderPreview);
    document.getElementById('btnClosePreview')?.addEventListener('click', () => {
        previewModal?.classList.add('d-none');
        previewModal?.setAttribute('aria-hidden', 'true');
    });
    previewModal?.addEventListener('click', (e) => {
        if (e.target === previewModal) {
            previewModal.classList.add('d-none');
            previewModal.setAttribute('aria-hidden', 'true');
        }
    });

    document.getElementById('btnSendLetter')?.addEventListener('click', () => {
        const rec = receiverSelect?.value;
        const sub = subjectInput?.value;

        if (!rec || !sub) {
            alert('گیرنده و موضوع الزامی هستند.');
            return;
        }

        if (!myEditor) {
            alert('ادیتور هنوز آماده نیست.');
            return;
        }

        const body = myEditor.getData().trim();

        if (!body) {
            alert('متن نامه نباید خالی باشد.');
            return;
        }

        document.getElementById('hTitle').value = sub;
        document.getElementById('hContent').value = body;
        document.getElementById('hReceiver').value = rec;
        localStorage.removeItem(draftKey);
        document.getElementById('finalForm').submit();
    });
});
