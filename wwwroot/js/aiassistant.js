document.addEventListener("DOMContentLoaded", function () {
    const input = document.getElementById("userInput");
    const chat = document.getElementById("chatMessages");
    const sendBtn = document.getElementById("sendBtn");
    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    sendBtn.addEventListener("click", sendMessage);
    input.addEventListener("keypress", function (e) {
        if (e.key === "Enter") sendMessage();
    });


    async function sendMessage() {

        const message = input.value.trim();
        if (message === "") return;

        // پیام کاربر
        const userDiv = document.createElement("div");
        userDiv.className = "bg-teal-100 dark:bg-teal-900/40 text-teal-900 dark:text-teal-100 rounded-2xl rounded-br-none p-3.5 max-w-[80%] text-xs font-semibold leading-relaxed self-end align-self-end";
        userDiv.innerText = message;
        chat.appendChild(userDiv);

        input.value = "";

        // پاسخ AI
        const wrapper = document.createElement("div");
        wrapper.className = "flex items-start gap-2.5 self-start";
        
        const avatar = document.createElement("div");
        avatar.className = "flex h-8 w-8 items-center justify-center rounded-full bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300 flex-shrink-0";
        avatar.innerHTML = '<i class="bi bi-robot"></i>';

        const aiDiv = document.createElement("div");
        aiDiv.className = "bg-slate-100 dark:bg-slate-800 text-slate-800 dark:text-slate-200 rounded-2xl rounded-bl-none p-3.5 max-w-[80%] text-xs font-semibold leading-relaxed";
        aiDiv.innerText = "▌";
        
        wrapper.appendChild(avatar);
        wrapper.appendChild(aiDiv);
        chat.appendChild(wrapper);

        chat.scrollTop = chat.scrollHeight;

        const response = await fetch("/AiAssistant/StreamAI", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": antiForgeryToken
            },
            body: JSON.stringify({ message: message })
        });

        const reader = response.body.getReader();
        const decoder = new TextDecoder();

        let aiText = "";

        while (true) {

            const { done, value } = await reader.read();
            if (done) break;

            const chunk = decoder.decode(value);

            const lines = chunk.split("\n");

            for (let line of lines) {

                if (line.startsWith("data: ")) {

                    const text = line.replace("data: ", "");

                    aiText += text;

                    aiDiv.innerText = aiText + "▌";
                }
            }

            chat.scrollTop = chat.scrollHeight;
        }

        aiDiv.innerText = aiText;
    }

});

async function askAI(message) {
    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const response = await fetch("/AiAssistant/StreamAI", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": antiForgeryToken
        },
        body: JSON.stringify({ message: message })
    });

    const reader = response.body.getReader();
    const decoder = new TextDecoder();

    let aiText = "";

    while (true) {

        const { done, value } = await reader.read();

        if (done) break;

        const chunk = decoder.decode(value);

        const lines = chunk.split("\n");

        for (let line of lines) {

            if (line.startsWith("data: ")) {

                const text = line.replace("data: ", "");

                aiText += text;

                document.getElementById("ai-response").innerText = aiText;
            }
        }
    }
}
