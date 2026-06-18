document.addEventListener("DOMContentLoaded", () => {
    let myEditor;

    ClassicEditor.create(document.querySelector('#editor'), {
        language: 'fa',
        contentsLangDirection: 'rtl'
    }).then(ed => {
        myEditor = ed;
        window.letterEditor = ed;
    });

    const allUsers = window.usersList || [];
    const replyConfig = window.letterReplyConfig || {};
    const subjectInput = document.getElementById('subjectInput');
    const receiverSelect = document.getElementById('receiverSelect');

    if (replyConfig.isReplyMode && receiverSelect) {
        receiverSelect.value = replyConfig.defaultReceiverId || '';
    }

    if (replyConfig.isReplyMode && subjectInput) {
        subjectInput.value = replyConfig.defaultSubject || '';
    }

    const triggerReceiverChange = () => {
        if (!receiverSelect) return;

        const id = receiverSelect.value;
        const receiverSpan = document.getElementById('display-receiver');
        const prefixSpan = document.getElementById('gender-prefix');

        if (!id) {
            receiverSpan.innerText = '....................';
            prefixSpan.innerText = 'جناب آقای / سرکار خانم';
            return;
        }

        const user = allUsers.find(x => x.Id == id);
        if (!user) {
            return;
        }

        receiverSpan.innerText = user.FullName;

        const g = (user.Gender || '').trim();
        prefixSpan.innerText =
            g === 'Male' ? 'جناب آقای' :
                g === 'Female' ? 'سرکار خانم' :
                    g === 'Department' ? 'واحد محترم' :
                        'جناب آقای / سرکار خانم';
    };

    if (receiverSelect) {
        receiverSelect.addEventListener('change', triggerReceiverChange);
        triggerReceiverChange();
    }

    if (subjectInput) {
        subjectInput.addEventListener('input', e => {
            document.getElementById('display-subject').innerText =
                e.target.value || '....................';
        });

        if (subjectInput.value) {
            document.getElementById('display-subject').innerText = subjectInput.value;
        }
    }

    const replyHidden = document.getElementById('hReplyToLetterId');
    if (replyHidden && replyConfig.replyToLetterId != null) {
        replyHidden.value = replyConfig.replyToLetterId;
    }

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
        document.getElementById('finalForm').submit();
    });
});
