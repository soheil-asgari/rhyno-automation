(function() {
    'use strict';

    // ── Modal helpers ──
    window.openModal = window.openModal || function(modalId) {
        var el = document.getElementById(modalId);
        if (!el) return;
        el.classList.add('open');
        el.style.display = 'flex';
        document.body.style.overflow = 'hidden';
        if (window.RhynoModalFocus) {
            var trigger = document.querySelector('[data-modal-target="' + modalId + '"]');
            RhynoModalFocus.open(el, { trigger: trigger });
        }
    };
    window.closeModal = window.closeModal || function(modalId) {
        var el = document.getElementById(modalId);
        if (!el) return;
        el.classList.remove('open');
        el.style.display = 'none';
        document.body.style.overflow = '';
        if (window.RhynoModalFocus) {
            RhynoModalFocus.close(el);
        }
    };
    document.addEventListener('click', function(e) {
        if (e.target.classList && e.target.classList.contains('rhyno-modal-overlay') && e.target.style.display === 'flex') {
            e.target.classList.remove('open');
            e.target.style.display = 'none';
            document.body.style.overflow = '';
            if (window.RhynoModalFocus) {
                RhynoModalFocus.close(e.target);
            }
        }
    });
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape') {
            document.querySelectorAll('.rhyno-modal-overlay').forEach(function(el) {
                if (el.style.display === 'flex') {
                    el.classList.remove('open');
                    el.style.display = 'none';
                    document.body.style.overflow = '';
                    if (window.RhynoModalFocus) {
                        RhynoModalFocus.close(el);
                    }
                }
            });
        }
    });

    // ── Quill Editor Setup ──
    var quillInstance = null;
    var textarea = document.getElementById('HtmlContent');
    var editorEl = document.getElementById('htmlEditor');

    function initQuill() {
        if (!editorEl || quillInstance) return;

        if (typeof Quill !== 'undefined') {
            quillInstance = new Quill('#htmlEditor', {
                theme: 'snow',
                modules: {
                    toolbar: [
                        [{ 'header': [1, 2, 3, false] }],
                        ['bold', 'italic', 'underline', 'strike'],
                        [{ 'list': 'ordered'}, { 'list': 'bullet' }],
                        [{ 'align': [] }],
                        ['clean']
                    ]
                },
                placeholder: 'محتوای قالب را اینجا بنویسید یا از تگ‌های کناری استفاده کنید...'
            });

            quillInstance.on('text-change', function() {
                syncEditor();
            });
        }
    }

    function syncEditor() {
        if (textarea) {
            if (quillInstance) {
                textarea.value = quillInstance.root.innerHTML;
            } else if (editorEl) {
                textarea.value = editorEl.innerHTML;
            }
        }
    }

    window.syncEditor = syncEditor;

    document.addEventListener('DOMContentLoaded', function() {
        initQuill();
        var form = (editorEl && editorEl.closest('form')) || (textarea && textarea.closest('form'));
        if (form) {
            form.addEventListener('submit', syncEditor);
        }
    });

    window.openTemplateModal = function(templateId, templateName) {
        initQuill();
        if (templateId) {
            fetch('/Contracts/PreviewTemplate/' + templateId + '?raw=true')
                .then(function(r) { return r.text(); })
                .then(function(html) {
                    if (quillInstance) {
                        quillInstance.clipboard.dangerouslyPasteHTML(html || '');
                    } else if (editorEl) {
                        editorEl.innerHTML = html;
                    }
                    if (textarea) textarea.value = html;
                });
        } else {
            var defaultHtml = '<div style="text-align:center;margin-bottom:20px;"><h2 style="color:#0d9488;">قرارداد</h2></div>'
                + '<div style="display:flex;gap:20px;margin-bottom:20px;">'
                + '<div style="flex:1;border:1px solid #e2e8f0;border-radius:6px;padding:12px;">'
                + '<h3 style="color:#0d9488;font-size:12px;margin-bottom:8px;">طرف اول</h3>'
                + '<p><strong>شرکت:</strong> {{CompanyName}}</p>'
                + '<p><strong>مسئول:</strong> {{AssignedUser}}</p>'
                + '<p><strong>نوع قرارداد:</strong> {{ContractType}}</p>'
                + '</div>'
                + '<div style="flex:1;border:1px solid #e2e8f0;border-radius:6px;padding:12px;">'
                + '<h3 style="color:#0d9488;font-size:12px;margin-bottom:8px;">طرف دوم</h3>'
                + '<p><strong>مشتری:</strong> {{CustomerName}}</p>'
                + '<p><strong>تلفن:</strong> {{CustomerPhone}}</p>'
                + '<p><strong>آدرس:</strong> {{CustomerAddress}}</p>'
                + '</div></div>'
                + '<p><strong>شماره قرارداد:</strong> {{ContractNumber}} | '
                + '<strong>تاریخ شروع:</strong> {{StartDate}} | '
                + '<strong>تاریخ پایان:</strong> {{EndDate}}</p>'
                + '<hr style="margin:16px 0;border-color:#e2e8f0;" />'
                + '<h3 style="color:#0d9488;font-size:12px;margin-bottom:8px;">موضوع قرارداد</h3>'
                + '<p>{{Description}}</p>'
                + '<h3 style="color:#0d9488;font-size:12px;margin:16px 0 8px;">شرایط و ضوابط</h3>'
                + '<p>{{Terms}}</p>';

            if (quillInstance) {
                quillInstance.clipboard.dangerouslyPasteHTML(defaultHtml);
            } else if (editorEl) {
                editorEl.innerHTML = defaultHtml;
            }
            if (textarea) textarea.value = defaultHtml;
        }
        openModal('addTemplateModal');
    };

    window.insertTagWysiwyg = function(tag) {
        initQuill();
        if (quillInstance) {
            var range = quillInstance.getSelection(true);
            var index = range ? range.index : quillInstance.getLength();
            quillInstance.insertText(index, tag, 'user');
            quillInstance.setSelection(index + tag.length);
            syncEditor();
        } else if (editorEl) {
            editorEl.focus();
            document.execCommand('insertText', false, tag);
            syncEditor();
        }
    };

    window.insertPageBreak = function() {
        initQuill();
        var html = '<br/><p style="text-align:center;"><strong>--- صفحه جدید ---</strong></p><br/>';
        if (quillInstance) {
            var range = quillInstance.getSelection(true);
            var index = range ? range.index : quillInstance.getLength();
            quillInstance.clipboard.dangerouslyPasteHTML(index, html);
            syncEditor();
        } else if (editorEl) {
            editorEl.focus();
            document.execCommand('insertHTML', false, html);
            syncEditor();
        }
    };
})();
