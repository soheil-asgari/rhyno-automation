// ── Background Change (upload file + URL) ──────────
                function changeBackground() {
                    const overlay = document.createElement('div');
                    overlay.style.cssText = 'position:fixed;inset:0;z-index:9999;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.7);backdrop-filter:blur(4px);';
                    overlay.id = 'bgUploadModal';

                    const dialog = document.createElement('div');
                    dialog.style.cssText = 'background:#fff;border-radius:12px;padding:24px;max-width:480px;width:90%;box-shadow:0 20px 60px rgba(0,0,0,0.4);direction:rtl;';
                    dialog.innerHTML = ''
                        + '<h3 style="margin:0 0 16px 0;font-size:16px;font-weight:800;color:#1f2937;">\uD83D\uDDBC\uFE0F \u062A\u063A\u06CC\u06CC\u0631 \u062A\u0635\u0648\u06CC\u0631 \u067E\u0633\u200C\u0632\u0645\u06CC\u0646\u0647</h3>'
                        + '<div style="margin-bottom:16px;">'
                        + '<label style="display:block;font-size:12px;font-weight:700;color:#4b5563;margin-bottom:8px;">\uD83D\uDCC1 \u0622\u067E\u0644\u0648\u062F \u0639\u06A9\u0633 \u0627\u0632 \u062F\u0633\u062A\u06AF\u0627\u0647:</label>'
                        + '<input type="file" id="bgFileInput" accept="image/*" style="display:block;width:100%;padding:8px;border:2px dashed #d1d5db;border-radius:8px;font-size:12px;cursor:pointer;background:#f9fafb;" />'
                        + '<div id="bgFilePreview" style="margin-top:8px;display:none;">'
                        + '<img id="bgPreviewImg" style="max-width:100%;max-height:120px;border-radius:8px;border:1px solid #e5e7eb;" />'
                        + '<button type="button" id="bgApplyFile" style="margin-top:8px;padding:8px 16px;background:#007bb5;color:#fff;border:none;border-radius:6px;font-size:12px;font-weight:700;cursor:pointer;">\u0627\u0639\u0645\u0627\u0644 \u0627\u06CC\u0646 \u0639\u06A9\u0633</button>'
                        + '</div></div>'
                        + '<div style="margin-bottom:16px;">'
                        + '<label style="display:block;font-size:12px;font-weight:700;color:#4b5563;margin-bottom:8px;">\uD83D\uDD17 \u06CC\u0627 \u0648\u0627\u0631\u062F \u06A9\u0631\u062F\u0646 \u0622\u062F\u0631\u0633 (URL):</label>'
                        + '<div style="display:flex;gap:8px;">'
                        + '<input type="text" id="bgUrlInput" placeholder="https://example.com/image.jpg" style="flex:1;padding:8px 12px;border:1px solid #d1d5db;border-radius:6px;font-size:12px;outline:none;" />'
                        + '<button type="button" id="bgApplyUrl" style="padding:8px 16px;background:#007bb5;color:#fff;border:none;border-radius:6px;font-size:12px;font-weight:700;cursor:pointer;">\u0627\u0639\u0645\u0627\u0644</button>'
                        + '</div></div>'
                        + '<div style="display:flex;gap:8px;justify-content:flex-end;">'
                        + '<button type="button" id="bgResetDefault" style="padding:8px 16px;background:#f3f4f6;color:#374151;border:1px solid #d1d5db;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer;">\u0628\u0627\u0632\u0646\u0634\u0627\u0646\u06CC \u0628\u0647 \u067E\u06CC\u0634\u200C\u0641\u0631\u0636</button>'
                        + '<button type="button" id="bgCloseModal" style="padding:8px 16px;background:#e5e7eb;color:#374151;border:1px solid #d1d5db;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer;">\u0628\u0633\u062A\u0646</button>'
                        + '</div>';
                    overlay.appendChild(dialog);
                    document.body.appendChild(overlay);

                    const close = () => overlay.remove();
                    document.getElementById('bgCloseModal').addEventListener('click', close);
                    overlay.addEventListener('click', (e) => { if (e.target === overlay) close(); });

                    let uploadedDataUrl = null;
                    document.getElementById('bgFileInput').addEventListener('change', (e) => {
                        const file = e.target.files[0];
                        if (!file || !file.type.startsWith('image/')) { Rhyno.toast('\u0644\u0637\u0641\u0627\u064B \u06CC\u06A9 \u0641\u0627\u06CC\u0644 \u062A\u0635\u0648\u06CC\u0631\u06CC \u0627\u0646\u062A\u062E\u0627\u0628 \u06A9\u0646\u06CC\u062F.', 'error'); return; }
                        const reader = new FileReader();
                        reader.onload = (ev) => {
                            uploadedDataUrl = ev.target.result;
                            document.getElementById('bgPreviewImg').src = uploadedDataUrl;
                            document.getElementById('bgFilePreview').style.display = 'block';
                        };
                        reader.readAsDataURL(file);
                    });
                    document.getElementById('bgApplyFile').addEventListener('click', () => {
                        if (uploadedDataUrl) { applyBg(uploadedDataUrl); close(); }
                    });
                    document.getElementById('bgApplyUrl').addEventListener('click', () => {
                        const url = document.getElementById('bgUrlInput').value.trim();
                        if (url) { applyBg(url); close(); } else { Rhyno.toast('\u0644\u0637\u0641\u0627\u064B \u06CC\u06A9 \u0622\u062F\u0631\u0633 \u0645\u0639\u062A\u0628\u0631 \u0648\u0627\u0631\u062F \u06A9\u0646\u06CC\u062F.', 'warning'); }
                    });
                    document.getElementById('bgResetDefault').addEventListener('click', () => {
                        localStorage.removeItem('rhyno.customBg');
                        var brandBg = '@(homeBrandBgUrl ?? "")';
                        if (brandBg) {
                            setBodyBg(brandBg);
                        } else {
                            document.body.style.background = '';
                            document.body.style.backgroundSize = '';
                        }
                        close();
                        Rhyno.toast('\u067E\u0633\u200C\u0632\u0645\u06CC\u0646\u0647 \u0628\u0647 \u062D\u0627\u0644\u062A \u067E\u06CC\u0634\u200C\u0641\u0631\u0636 \u0628\u0627\u0632\u0646\u0634\u0627\u0646\u06CC \u0634\u062F.', 'success');
                    });
                }

                function setBodyBg(src) {
                    document.body.style.setProperty('background', `url('${src}') no-repeat center center fixed`, 'important');
                    document.body.style.setProperty('background-size', 'cover', 'important');
                }

                function applyBg(src) {
                    setBodyBg(src);
                    localStorage.setItem('rhyno.customBg', src);
                    Rhyno.toast('\u067E\u0633\u200C\u0632\u0645\u06CC\u0646\u0647 \u0628\u0627 \u0645\u0648\u0641\u0642\u06CC\u062A \u062A\u063A\u06CC\u06CC\u0631 \u06A9\u0631\u062F.', 'success');
                }

                function addShortcut() {
                    Rhyno.toast('\u0628\u0631\u0627\u06CC \u0627\u0641\u0632\u0648\u062F\u0646 \u0645\u06CC\u0627\u0646\u0628\u0631 \u0628\u0647 \u0635\u0641\u062D\u0647 \u0645\u0648\u0631\u062F \u0646\u0638\u0631 \u0628\u0631\u0648\u06CC\u062F.', 'info');
                }

                function addWidget() {
                    Rhyno.toast('\u0648\u06CC\u062C\u062A\u200C\u0647\u0627\u06CC \u0628\u06CC\u0634\u062A\u0631\u06CC \u0628\u0647 \u0632\u0648\u062F\u06CC \u062F\u0631 \u062F\u0633\u062A\u0631\u0633 \u062E\u0648\u0627\u0647\u0646\u062F \u0628\u0648\u062F.', 'info');
                }

                // ── Restore custom background ────────────────────────
                (() => {
                    const savedBg = localStorage.getItem('rhyno.customBg');
                    if (savedBg) {
                        setBodyBg(savedBg);
                    }
                })();

                // ── Randomize online users count (demo) ──────────────
                (() => {
                    const el = document.getElementById('onlineUsersCount');
                    if (el) {
                        const base = parseInt(el.textContent.replace(/[^0-9]/g, '')) || 2;
                        setInterval(() => {
                            const delta = Math.floor(Math.random() * 3) - 1;
                            const newVal = Math.max(1, base + delta);
                            el.textContent = newVal.toLocaleString('fa-IR') + ' نفر';
                        }, 30000);
                    }
                })();
