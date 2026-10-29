document.addEventListener("DOMContentLoaded", () => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
        document.querySelector('meta[name="request-verification-token"]')?.content || "";

    const postJson = async (url, payload) => {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            let message = "درخواست هوش مصنوعی با خطا روبه‌رو شد.";
            try {
                const error = await response.json();
                message = error.message || message;
            } catch { }
            throw new Error(message);
        }

        return response.json();
    };

    const plainTextToHtml = (value) => {
        const safe = String(value || "")
            .replace(/&/g, "&")
            .replace(/</g, "<")
            .replace(/>/g, ">");
        return safe
            .split(/\n{2,}/)
            .map(part => `<p>${part.replace(/\n/g, "<br>")}</p>`)
            .join("");
    };

    const getEditorBody = () => {
        if (window.letterEditor?.getData) return window.letterEditor.getData();
        return document.getElementById("editor")?.innerHTML || "";
    };

    const setEditorBody = (value) => {
        const html = plainTextToHtml(value);
        if (window.letterEditor?.setData) { window.letterEditor.setData(html); return; }
        const editor = document.getElementById("editor");
        if (editor) editor.innerHTML = html;
    };

    const setBtnLoading = (btn, loading, label) => {
        if (!btn) return;
        if (loading) {
            btn.disabled = true;
            btn.innerHTML = '<span class="inline-block w-3.5 h-3.5 border-2 border-white/40 border-t-white rounded-full animate-spin"></span><span>در حال پردازش...</span>';
        } else {
            btn.disabled = false;
            btn.innerHTML = `<i class="bi bi-send-fill"></i><span>${label}</span>`;
        }
    };

    // ─── Create page: AI writing assistant card ───
    const aiActionButtons = document.querySelectorAll("[data-ai-action]");
    const aiOutput = document.getElementById("aiLetterOutput");
    const aiInsertBtn = document.getElementById("btnInsertAiText");
    const aiStatus = document.getElementById("aiLetterStatus");
    let lastAiText = "";

    const writeAiOutput = (text) => {
        lastAiText = text || "";
        if (aiOutput) {
            aiOutput.classList.remove("text-slate-400");
            aiOutput.textContent = lastAiText || "متن پیشنهادی پس از تولید در اینجا نمایش داده می‌شود.";
        }
        if (aiInsertBtn) aiInsertBtn.disabled = !lastAiText;
    };

    if (aiActionButtons.length) {
        aiActionButtons.forEach(btn => {
            btn.addEventListener("click", async () => {
                aiActionButtons.forEach(b => b.classList.remove("active"));
                btn.classList.add("active");
                const action = btn.dataset.aiAction;
                const instruction = document.getElementById("aiLetterInstruction")?.value || "";
                const receiverId = document.getElementById("receiverSelect")?.value || "";
                const subject = document.getElementById("subjectInput")?.value || "";

                if (action === "draft") {
                    if (!receiverId || !subject) {
                        if (aiStatus) aiStatus.textContent = "ابتدا گیرنده و موضوع نامه را انتخاب کنید.";
                        return;
                    }
                    if (aiStatus) aiStatus.textContent = "در حال تولید متن نامه...";
                    try {
                        const result = await postJson("/Letters/DraftWithAi", {
                            receiverId,
                            subject,
                            instruction,
                            currentBody: getEditorBody()
                        });
                        writeAiOutput(result.reply || "");
                        if (aiStatus) aiStatus.textContent = "متن پیشنهادی تولید شد. قبل از درج، آن را بازبینی کنید.";
                    } catch (error) {
                        if (aiStatus) aiStatus.textContent = error.message;
                    }
                    return;
                }

                // tone / summary use the existing letter body + instruction
                if (!subject) {
                    if (aiStatus) aiStatus.textContent = "ابتدا موضوع نامه را وارد کنید.";
                    return;
                }
                if (aiStatus) aiStatus.textContent = action === "tone" ? "در حال بهبود لحن اداری..." : "در حال خلاصه‌سازی...";
                const tonePrompt = `متن زیر را با لحن رسمی‌تر و ساختار اداری استاندارد فارسی بازنویسی کن. فقط متن بازنویسی‌شده را برگردان:\n\n${getEditorBody()}`;
                const summaryPrompt = `خلاصه‌ای کوتاه و مدیریتی از متن زیر بنویس و فقط خلاصه را برگردان:\n\n${getEditorBody()}`;
                try {
                    const result = await postJson("/Letters/DraftWithAi", {
                        receiverId: receiverId || "self",
                        subject,
                        instruction: (action === "tone" ? tonePrompt : summaryPrompt),
                        currentBody: getEditorBody()
                    });
                    writeAiOutput(result.reply || "");
                    if (aiStatus) aiStatus.textContent = "خروجی آماده است.";
                } catch (error) {
                    if (aiStatus) aiStatus.textContent = error.message;
                }
            });
        });
    }

    aiInsertBtn?.addEventListener("click", () => {
        if (!lastAiText) return;
        setEditorBody(lastAiText);
        writeAiOutput("");
        if (aiStatus) aiStatus.textContent = "متن پیشنهادی در نامه درج شد.";
    });

    // ─── Index page: AI smart summary chips ───
    const summaryResult = document.getElementById("letterSummaryResult");
    const chips = document.querySelectorAll(".lts-ai-chip[data-suggest]");
    const drawerBody = document.getElementById("aiDrawerBody");
    const aiDrawer = document.getElementById("letterAiPanel");

    const mapSuggest = (suggest) => {
        const map = { week: "week", unread: "unread", today: "today", urgent: "unread", pending: "week" };
        return map[suggest] || "week";
    };

    const showSummaryLoading = () => {
        if (!summaryResult) return;
        summaryResult.classList.add("is-loading");
        summaryResult.innerHTML = '<div class="flex items-center justify-center gap-2 py-2 text-slate-400"><span class="inline-block w-4 h-4 border-2 border-indigo-300 border-t-indigo-600 rounded-full animate-spin"></span><span>در حال دریافت خلاصه از هوش مصنوعی...</span></div>';
    };

    const showSummaryResult = (reply) => {
        if (!summaryResult) return;
        summaryResult.classList.remove("is-loading");
        summaryResult.classList.add("lts-ai-result-card");
        summaryResult.textContent = reply || "پاسخی دریافت نشد.";
        if (drawerBody) drawerBody.scrollTop = drawerBody.scrollHeight;
    };

    const runSummary = async (suggest) => {
        if (!summaryResult) return;
        aiDrawer?.classList.remove("collapsed");
        const mode = mapSuggest(suggest);
        showSummaryLoading();
        try {
            const result = await postJson("/Letters/SummarizeWithAi", { mode, letterId: null });
            showSummaryResult(result.reply || "");
        } catch (error) {
            summaryResult.classList.remove("is-loading");
            summaryResult.textContent = error.message;
        }
    };

    chips.forEach(chip => {
        chip.addEventListener("click", async (e) => {
            e.preventDefault();
            e.stopPropagation();
            const suggest = chip.dataset.suggest;
            chips.forEach(c => c.disabled = true);
            await runSummary(suggest);
            chips.forEach(c => c.disabled = false);
        });
    });

    // ─── Create page legacy draft button (compat) ───
    const draftButton = document.getElementById("btnDraftWithAi");
    if (draftButton) {
        draftButton.addEventListener("click", async () => {
            const receiverId = document.getElementById("receiverSelect")?.value || "";
            const subject = document.getElementById("subjectInput")?.value || "";
            const instruction = document.getElementById("aiLetterInstruction")?.value || "";
            const status = document.getElementById("aiLetterStatus");

            if (!receiverId || !subject) {
                if (status) status.textContent = "ابتدا گیرنده و موضوع نامه را انتخاب کنید.";
                return;
            }
            setBtnLoading(draftButton, true, "تولید متن");
            if (status) status.textContent = "در حال تولید متن نامه...";
            try {
                const result = await postJson("/Letters/DraftWithAi", {
                    receiverId, subject, instruction, currentBody: getEditorBody()
                });
                setEditorBody(result.reply || "");
                if (status) status.textContent = "متن پیشنهادی تولید شد. قبل از ارسال، آن را بازبینی کنید.";
            } catch (error) {
                if (status) status.textContent = error.message;
            } finally {
                setBtnLoading(draftButton, false, "تولید متن");
            }
        });
    }

    // ─── Reply with AI (Details page) ───
    const replyButton = document.getElementById("btnGenerateLetterReply");
    if (replyButton) {
        replyButton.addEventListener("click", async () => {
            const resultBox = document.getElementById("aiLetterReplyResult");
            const intent = document.getElementById("aiReplyIntent")?.value || "";
            const letterId = Number(replyButton.dataset.letterId || "0");
            if (!letterId) {
                if (resultBox) resultBox.value = "شناسه نامه معتبر نیست.";
                return;
            }
            setBtnLoading(replyButton, true, "تولید پاسخ");
            if (resultBox) resultBox.value = "در حال تولید پاسخ پیشنهادی...";
            try {
                const result = await postJson("/Letters/ReplyWithAi", { letterId, intent });
                if (resultBox) resultBox.value = result.reply || "";
            } catch (error) {
                if (resultBox) resultBox.value = error.message;
            } finally {
                setBtnLoading(replyButton, false, "تولید پاسخ");
            }
        });
    }
});