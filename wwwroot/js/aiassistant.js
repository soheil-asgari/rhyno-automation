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
        userDiv.className = "user-message";
        userDiv.innerText = message;
        chat.appendChild(userDiv);

        input.value = "";

        // پاسخ AI
        const aiDiv = document.createElement("div");
        aiDiv.className = "ai-message";
        aiDiv.innerText = "▌";
        chat.appendChild(aiDiv);

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
