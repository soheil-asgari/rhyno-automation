// Password visibility toggle
                    function togglePassword(inputId, btn) {
                        var input = document.getElementById(inputId);
                        var icon = btn.querySelector('i');
                        if (!input) return;
                        if (input.type === 'password') {
                            input.type = 'text';
                            if (icon) { icon.className = 'bi bi-eye-slash'; }
                        } else {
                            input.type = 'password';
                            if (icon) { icon.className = 'bi bi-eye'; }
                        }
                    }

                    // Update sandbox label text when toggle changes
                    (function () {
                        var chk = document.getElementById('chkSandbox');
                        var lbl = chk && chk.parentElement && chk.parentElement.querySelector('.form-check-label');
                        if (chk && lbl) {
                            var updateLabel = function () {
                                lbl.textContent = chk.checked ? 'حالت آزمایشی فعال' : 'حالت آزمایشی غیرفعال';
                            };
                            chk.addEventListener('change', updateLabel);
                        }
                    })();
