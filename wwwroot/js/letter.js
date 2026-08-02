document.addEventListener("DOMContentLoaded", () => {
    let myEditor;
    const draftKey = "rhyno.letter.draft";
    const autosaveDelay = 700;
    let autosaveTimer = null;

    const allUsers = window.usersList || [];
    const replyConfig = window.letterReplyConfig || {};
    const subjectInput = document.getElementById('subjectInput');
    const receiverSelect = document.getElementById('receiverSelect');
    const draftStatus = document.getElementById('draftStatus');
    const previewModal = document.getElementById('letterPreviewModal');
    const previewBody = document.getElementById('letterPreviewBody');
    const editorElement = document.getElementById('editor');

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

    const isPreviewOpen = () => previewModal && !previewModal.classList.contains('hidden');

    const refreshOpenPreview = () => {
        // اگر مودال پیش‌نمایش باز است، محتوا را با آخرین مقادیر به‌روزرسانی کن
        if (isPreviewOpen()) {
            renderPreview();
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
            refreshOpenPreview();
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

        refreshOpenPreview();
    };

    const scheduleDraftSave = () => {
        if (!myEditor && !editorElement) return;
        clearTimeout(autosaveTimer);
        setDraftStatus("در حال ذخیره پیش‌نویس...");
        autosaveTimer = setTimeout(saveDraft, autosaveDelay);
    };

    const saveDraft = () => {
        const body = getEditorBody();
        const payload = {
            subject: subjectInput?.value || "",
            receiverId: receiverSelect?.value || "",
            body: body || "",
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
            if (draft.body) {
                if (myEditor?.setData) {
                    myEditor.setData(draft.body);
                } else if (editorElement) {
                    editorElement.innerHTML = draft.body;
                }
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
        refreshOpenPreview();
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

    /**
     * دریافت متن ادیتور؛ در صورت در دسترس نبودن CKEditor،
     * محتوای المان #editor (contenteditable) را برمی‌گرداند.
     */
    const getEditorBody = () => {
        if (myEditor?.getData) {
            try {
                return myEditor.getData();
            } catch {
                // ignore
            }
        }
        return editorElement?.innerHTML || '';
    };

    // در دسترس قرار دادن برای اسکریپت‌های دیگر (مثل letter-form.js)
    window.rhynoLetterGetBody = getEditorBody;
    window.rhynoLetterSubmit = () => {
        document.getElementById('btnSendLetter')?.click();
    };

    const renderPreview = () => {
        if (!previewModal || !previewBody) return;
        const receiver = document.getElementById('display-receiver')?.innerText || '....................';
        const prefix = document.getElementById('gender-prefix')?.innerText || '';
        const subject = subjectInput?.value || '....................';
        const body = getEditorBody();
        previewBody.innerHTML = `
            <div class="paper-preview">
                <p class="mb-2"><strong>گیرنده:</strong> ${prefix} ${receiver}</p>
                <p class="mb-3"><strong>موضوع:</strong> ${subject}</p>
                <div class="preview-render">${body}</div>
            </div>`;
        previewModal.classList.remove('hidden');
        previewModal.setAttribute('aria-hidden', 'false');
    };

    const closePreview = () => {
        previewModal?.classList.add('hidden');
        previewModal?.setAttribute('aria-hidden', 'true');
    };

    // ── اتصال رویدادها (مستقل از ادیتور / بدون وابستگی به CDN) ──
    document.getElementById('btnPreviewLetter')?.addEventListener('click', renderPreview);
    document.getElementById('btnClosePreview')?.addEventListener('click', closePreview);
    previewModal?.addEventListener('click', (e) => {
        if (e.target === previewModal) {
            closePreview();
        }
    });

    document.getElementById('btnSendLetter')?.addEventListener('click', () => {
        const rec = receiverSelect?.value;
        const sub = subjectInput?.value;

        if (!rec || !sub) {
            alert('گیرنده و موضوع الزامی هستند.');
            return;
        }

        const body = getEditorBody().trim();

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

    // ── راه‌اندازی ادیتور با محافظت در برابر نبود CDN ──
    if (typeof ClassicEditor !== 'undefined' && editorElement) {
        ClassicEditor.create(editorElement, {
            language: 'fa',
            contentsLangDirection: 'rtl'
        }).then(ed => {
            myEditor = ed;
            window.letterEditor = ed;
            loadDraft();
            ed.model.document.on('change:data', () => {
                scheduleDraftSave();
                refreshOpenPreview();
            });
        }).catch(err => {
            console.error('CKEditor failed to initialize:', err);
            if (editorElement) {
                editorElement.setAttribute('contenteditable', 'true');
            }
            setDraftStatus('ویرایشگر پیشرفته در دسترس نیست؛ از حالت ویرایش ساده استفاده کنید.');
        });
    } else if (editorElement) {
        // بدون CKEditor - از contenteditable استفاده می‌کنیم
        editorElement.setAttribute('contenteditable', 'true');
        editorElement.addEventListener('input', () => {
            scheduleDraftSave();
            refreshOpenPreview();
        });
        loadDraft();
        setDraftStatus('حالت ویرایش ساده');
    }
});