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

            // ── Insert tag into WYSIWYG ──────────────────────────────
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
