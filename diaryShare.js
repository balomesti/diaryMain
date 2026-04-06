// wwwroot/js/diaryShare.js
// Requires html2canvas — add this to your _Host.cshtml or index.html:
// <script src="https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js"></script>
// <script src="js/diaryShare.js"></script>

window.DiaryShare = {

    /**
     * Captures the element with the given id as a PNG and triggers a download.
     * Falls back to the Web Share API on mobile if available.
     */
    captureAndDownload: async function (elementId, filename) {
        const el = document.getElementById(elementId);
        if (!el) { console.warn('DiaryShare: element not found:', elementId); return; }

        // Briefly make it visible so html2canvas can render it
        const prev = {
            position: el.style.position,
            top:      el.style.top,
            left:     el.style.left,
        };
        el.style.position = 'fixed';
        el.style.top      = '0';
        el.style.left     = '0';

        try {
            const canvas = await html2canvas(el, {
                scale:           2,          // 2x for retina-quality output
                useCORS:         true,
                backgroundColor: null,
                logging:         false,
            });

            // Restore hidden position
            el.style.position = prev.position;
            el.style.top      = prev.top;
            el.style.left     = prev.left;

            const dataUrl = canvas.toDataURL('image/png');

            // Try Web Share API first (mobile browsers)
            if (navigator.share && navigator.canShare) {
                const blob = await (await fetch(dataUrl)).blob();
                const file = new File([blob], filename, { type: 'image/png' });
                if (navigator.canShare({ files: [file] })) {
                    await navigator.share({ files: [file], title: 'Diary Me' });
                    return;
                }
            }

            // Fallback: trigger download
            const link      = document.createElement('a');
            link.href       = dataUrl;
            link.download   = filename;
            link.click();

        } catch (err) {
            // Restore on error too
            el.style.position = prev.position;
            el.style.top      = prev.top;
            el.style.left     = prev.left;
            console.error('DiaryShare: capture failed', err);
        }
    }
};
