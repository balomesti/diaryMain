// ─────────────────────────────────────────────────────────────────────────────
// ADD these two methods inside your existing RichTextEditor = { ... } object.
// Place them alongside your other methods like execCommand, getContent, etc.
// ─────────────────────────────────────────────────────────────────────────────

startVoice(id, dotnetRef) {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) return false;

    const recognition = new SpeechRecognition();
    recognition.lang = 'en-US';
    recognition.continuous = true;
    recognition.interimResults = false;

    recognition.onresult = (event) => {
        const transcript = Array.from(event.results)
            .map(r => r[0].transcript)
            .join(' ');
        dotnetRef.invokeMethodAsync('OnVoiceResult', transcript);
    };

    recognition.onend = () => {
        dotnetRef.invokeMethodAsync('OnVoiceEnd');
    };

    recognition.onerror = () => {
        dotnetRef.invokeMethodAsync('OnVoiceEnd');
    };

    recognition.start();

    if (!editors[id]) editors[id] = {};
    editors[id]._recognition = recognition;
    return true;
},

stopVoice(id) {
    const recognition = editors[id]?._recognition;
    if (recognition) {
        recognition.stop();
        editors[id]._recognition = null;
    }
},


// ─────────────────────────────────────────────────────────────────────────────
// ADD these styles to your existing editor CSS file (e.g. RichTextEditor.css)
// ─────────────────────────────────────────────────────────────────────────────

/*
.tb-mic {
    display: flex;
    align-items: center;
    gap: 5px;
    padding: 5px 10px;
    border-radius: 6px;
    border: 1px solid #D4C9BD;
    background: #FAF7F2;
    font-size: 0.75rem;
    color: #5C4033;
    cursor: pointer;
    transition: all 0.2s ease;
}

.tb-mic:hover {
    background: #F0EAE0;
}

.tb-mic.listening {
    background: #FEE2E2;
    border-color: #FCA5A5;
    color: #991B1B;
    animation: pulse-mic 1.2s ease-in-out infinite;
}

@keyframes pulse-mic {
    0%, 100% { opacity: 1; }
    50%       { opacity: 0.6; }
}
*/
