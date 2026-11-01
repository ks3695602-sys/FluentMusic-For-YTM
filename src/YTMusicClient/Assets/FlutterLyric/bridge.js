// Minimal Non-Invasive Bridge for WinUI 3 -> flutter_lyric (Skia / CanvasKit)
window.lyricsOffsetMs = 0;
window._customQrc = null;
window._customTrans = null;
window._activeAudioElement = null;
window._isInternalSeek = false;

// 1. Intercept asset fetch calls to feed live lyrics to flutter_lyric
const _originalFetch = window.fetch;
window.fetch = async function(resource, init) {
    try {
        const url = (typeof resource === 'string') ? resource : (resource ? resource.url : '');
        if (url && (url.indexOf('stay.qrc') !== -1 || url.endsWith('.qrc'))) {
            if (window._customQrc) {
                return new Response(window._customQrc, {
                    status: 200,
                    headers: { 'Content-Type': 'text/plain; charset=utf-8' }
                });
            }
        }
        if (url && (url.indexOf('stay.trans.lrc') !== -1 || url.endsWith('.trans.lrc'))) {
            if (window._customTrans) {
                return new Response(window._customTrans, {
                    status: 200,
                    headers: { 'Content-Type': 'text/plain; charset=utf-8' }
                });
            }
        }
    } catch (e) {
        console.warn('[Bridge] Fetch intercept warning:', e);
    }
    return _originalFetch.apply(this, arguments);
};

// 2. Intercept audio element creation to mute sound and capture playback ticker
const _origCreate = document.createElement;
document.createElement = function(tag) {
    const el = _origCreate.apply(this, arguments);
    if (tag && typeof tag === 'string' && tag.toLowerCase() === 'audio') {
        el.muted = true;
        el.volume = 0;
        window._activeAudioElement = el;

        el.addEventListener('seeked', function() {
            if (window._isInternalSeek) return;
            if (window.chrome && window.chrome.webview) {
                const targetMs = (el.currentTime * 1000) - (window.lyricsOffsetMs || 0);
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'seek',
                    timeMs: Math.max(0, targetMs)
                }));
            }
        });
    }
    return el;
};

// 3. Fallback search for audio elements created before this script
function checkExistingAudio() {
    if (!window._activeAudioElement) {
        const audios = document.getElementsByTagName('audio');
        if (audios.length > 0) {
            window._activeAudioElement = audios[0];
            window._activeAudioElement.muted = true;
            window._activeAudioElement.volume = 0;
        }
    }
}
setInterval(checkExistingAudio, 500);

// 4. Listen to WinUI 3 messages
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        try {
            const msg = (typeof event.data === 'string') ? JSON.parse(event.data) : event.data;
            if (!msg || !msg.type) return;

            switch (msg.type) {
                case 'timeupdate':
                case 'time':
                    if (window._activeAudioElement) {
                        const effectiveMs = Math.max(0, (msg.timeMs || 0) + (window.lyricsOffsetMs || 0));
                        const targetSec = effectiveMs / 1000.0;
                        window._isInternalSeek = true;
                        if (Math.abs(window._activeAudioElement.currentTime - targetSec) > 0.04) {
                            window._activeAudioElement.currentTime = targetSec;
                        }
                        if (msg.isPlaying && window._activeAudioElement.paused) {
                            window._activeAudioElement.play().catch(function() {});
                        } else if (!msg.isPlaying && !window._activeAudioElement.paused) {
                            window._activeAudioElement.pause();
                        }
                        setTimeout(function() { window._isInternalSeek = false; }, 40);
                    }
                    break;

                case 'offset':
                    window.lyricsOffsetMs = Number(msg.offsetMs) || 0;
                    break;

                case 'lyrics':
                    window._customQrc = msg.qrc || '';
                    window._customTrans = msg.trans || '';
                    if (window._activeAudioElement) {
                        window._activeAudioElement.currentTime = 0;
                    }
                    break;
            }
        } catch (e) {
            console.error('[Bridge] Error handling WinUI message:', e);
        }
    });
}
