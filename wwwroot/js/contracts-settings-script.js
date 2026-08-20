(function() {
            'use strict';

            // ── Modal helpers (with focus trap) ────────────────────────
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

            // ── WYSIWYG Editor ────────────────────────────────────────
            var editor = document.getElementById('htmlEditor');
            var textarea = document.getElementById('HtmlContent');

            function syncEditor() {
                if (editor && textarea) {
                    textarea.value = editor.innerHTML;
                }
            }

            // Sync before form submit
            var form = editor && editor.closest('form');
            if (form) {
                form.addEventListener('submit', syncEditor);
            }

            // Toolbar commands
            document.querySelectorAll('.wysiwyg-btn[data-cmd]').forEach(function(btn) {
                btn.addEventListener('click', function() {
                    var cmd = btn.getAttribute('data-cmd');
                    if (editor) editor.focus();
                    document.execCommand(cmd, false, null);
                    syncEditor();
                });
            });

            // Initial sync: load any existing textarea value into editor
            if (editor && textarea && textarea.value) {
                editor.innerHTML = textarea.value;
            }

            // Auto-sync on input
            if (editor) {
                editor.addEventListener('input', syncEditor);
            }

            // ── Pre-load default template when opening modal ─────────────
            window.openTemplateModal = function(templateId, templateName) {
                var editor = document.getElementById('htmlEditor');
                var textarea = document.getElementById('HtmlContent');
                if (templateId) {
                    // Edit mode: load existing template
                    fetch('/Contracts/PreviewTemplate/' + templateId + '?raw=true')
                        .then(function(r) { return r.text(); })
                        .then(function(html) {
                            if (editor) editor.innerHTML = html;
                            if (textarea) textarea.value = html;
                        });
                } else {
                    // New template: pre-load a clean default structure
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
                        + '<p>{{Terms}}</p>'
                        + '<h3 style="color:#0d9488;font-size:12px;margin:16px 0 8px;">خلاصه مالی</h3>'
                        + '<table style="width:100%;border-collapse:collapse;margin:10px 0;">'
                        + '<tr style="background:#0d9488;color:#fff;"><th style="padding:6px;border:1px solid #e2e8f0;">مبلغ کل</th><th style="padding:6px;border:1px solid #e2e8f0;">پرداخت شده</th><th style="padding:6px;border:1px solid #e2e8f0;">باقی مانده</th><th style="padding:6px;border:1px solid #e2e8f0;">وضعیت</th></tr>'
                        + '<tr><td style="padding:6px;border:1px solid #e2e8f0;text-align:center;">{{TotalAmount}} ریال</td><td style="padding:6px;border:1px solid #e2e8f0;text-align:center;">{{PaidAmount}} ریال</td><td style="padding:6px;border:1px solid #e2e8f0;text-align:center;">{{RemainingBalance}} ریال</td><td style="padding:6px;border:1px solid #e2e8f0;text-align:center;">{{PaymentStatus}}</td></tr>'
                        + '</table>';
                    if (editor) editor.innerHTML = defaultHtml;
                    if (textarea) textarea.value = defaultHtml;
                }
                openModal('addTemplateModal');
            };
            window.insertTagWysiwyg = function(tag) {
                if (!editor) return;
                editor.focus();
                document.execCommand('insertHTML', false, tag);
                syncEditor();
            };

            // ── Insert a page break into WYSIWYG ─────────────────────
            window.insertPageBreak = function() {
                if (!editor) return;
                editor.focus();
                var html = '<br/><p style="text-align:center;"><strong>--- صفحه جدید ---</strong></p><br/>';
                document.execCommand('insertHTML', false, html);
                syncEditor();
            };
        })();
