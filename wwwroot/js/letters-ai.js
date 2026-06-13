document.addEventListener("DOMContentLoaded", () => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

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
            } catch {
                // Response was not JSON.
            }

            throw new Error(message);
        }

        return response.json();
    };

    const plainTextToHtml = (value) => {
        const safe = String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");

        return safe
            .split(/\n{2,}/)
            .map(part => `<p>${part.replace(/\n/g, "<br>")}</p>`)
            .join("");
    };

    const getEditorBody = () => {
        if (window.letterEditor?.getData) {
            return window.letterEditor.getData();
        }

        return document.getElementById("editor")?.innerHTML || "";
    };

    const setEditorBody = (value) => {
        const html = plainTextToHtml(value);
        if (window.letterEditor?.setData) {
            window.letterEditor.setData(html);
            return;
        }

        const editor = document.getElementById("editor");
        if (editor) {
            editor.innerHTML = html;
        }
    };

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

            draftButton.disabled = true;
            if (status) status.textContent = "در حال تولید متن نامه...";

            try {
                const result = await postJson("/Letters/DraftWithAi", {
                    receiverId,
                    subject,
                    instruction,
                    currentBody: getEditorBody()
                });

                setEditorBody(result.reply || "");
                if (status) status.textContent = "متن پیشنهادی تولید شد. قبل از ارسال، آن را بازبینی کنید.";
            } catch (error) {
                if (status) status.textContent = error.message;
            } finally {
                draftButton.disabled = false;
            }
        });
    }

    const summaryButton = document.getElementById("btnSummarizeLetters");
    if (summaryButton) {
        const modeSelect = document.getElementById("letterSummaryMode");
        const letterSelect = document.getElementById("letterSummaryId");
        const resultBox = document.getElementById("letterSummaryResult");

        const syncLetterSelect = () => {
            if (!modeSelect || !letterSelect) return;
            letterSelect.disabled = modeSelect.value !== "selected";
        };

        syncLetterSelect();
        modeSelect?.addEventListener("change", syncLetterSelect);

        summaryButton.addEventListener("click", async () => {
            const mode = modeSelect?.value || "week";
            const letterId = letterSelect?.value || null;

            if (mode === "selected" && !letterId) {
                if (resultBox) resultBox.textContent = "برای خلاصه و گردش نامه، یک موضوع را از لیست انتخاب کنید.";
                return;
            }

            summaryButton.disabled = true;
            if (resultBox) resultBox.textContent = "در حال دریافت خلاصه از هوش مصنوعی...";

            try {
                const result = await postJson("/Letters/SummarizeWithAi", {
                    mode,
                    letterId: letterId ? Number(letterId) : null
                });

                if (resultBox) resultBox.textContent = result.reply || "";
            } catch (error) {
                if (resultBox) resultBox.textContent = error.message;
            } finally {
                summaryButton.disabled = false;
            }
        });
    }

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

            replyButton.disabled = true;
            if (resultBox) resultBox.value = "در حال تولید پاسخ پیشنهادی...";

            try {
                const result = await postJson("/Letters/ReplyWithAi", {
                    letterId,
                    intent
                });

                if (resultBox) resultBox.value = result.reply || "";
            } catch (error) {
                if (resultBox) resultBox.value = error.message;
            } finally {
                replyButton.disabled = false;
            }
        });
    }
});
