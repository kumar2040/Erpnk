function openModal() {
    const modal = document.getElementById('modalOverlay');
    const content = modal.querySelector('div');

    modal.classList.remove('opacity-0', 'pointer-events-none');
    content.classList.remove('scale-95');
    content.classList.add('scale-100');

    document.body.style.overflow = 'hidden'; // Body Lock
}

window.submitToken = function (token, returnUrl) {
    console.log("DEBUG: submitToken called");
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = '/auth/set-token';

    const tokenInput = document.createElement('input');
    tokenInput.type = 'hidden';
    tokenInput.name = 'token';
    tokenInput.value = token;
    form.appendChild(tokenInput);

    if (returnUrl) {
        const ru = document.createElement('input');
        ru.type = 'hidden';
        ru.name = 'returnUrl';
        ru.value = returnUrl;
        form.appendChild(ru);
    }

    document.body.appendChild(form);
    form.submit();
}

function closeModal() {
    const modal = document.getElementById('modalOverlay');
    const content = modal.querySelector('div');

    modal.classList.add('opacity-0', 'pointer-events-none');
    content.classList.remove('scale-100');
    content.classList.add('scale-95');

    document.body.style.overflow = ''; // Unlock
}

// Handle Escape Key
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeModal();
});

// Read-aloud for notifications. Audio clips are queued and played one after another, so two notifications
// arriving together are read in turn instead of talking over each other. Always on (no mute switch). A browser
// may refuse autoplay before the user has clicked anything on the page; that clip is just skipped, never an error.
window.tts = {
    _queue: [],
    _playing: false,
    play: function (bytes, mime) {
        const url = URL.createObjectURL(new Blob([bytes], { type: mime || 'audio/wav' }));
        this._queue.push(url);
        this._next();
    },
    _next: function () {
        if (this._playing || this._queue.length === 0) return;
        this._playing = true;
        const url = this._queue.shift();
        const audio = new Audio(url);
        const done = () => { URL.revokeObjectURL(url); this._playing = false; this._next(); };
        audio.onended = done;
        audio.onerror = () => { console.warn('Read-aloud: the audio could not be decoded.'); done(); };
        audio.play().catch(e => {
            console.warn('Read-aloud: the browser blocked or failed playback (' + (e && e.name) + '). Click the speaker icon once to allow sound.');
            done();
        });
    }
};

// Scrolls one element into view inside its nearest scrollable ancestor. Used by the
// /bom deep link to bring the linked order into view in the orders column. Missing
// element is not an error: a placed order is filtered out of the list, so there is
// simply no row to scroll to.
window.scrollElementIntoView = function (id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

// Resizable panels for /bom. Delegated pointer handling on the .bom-pipe container, so it keeps working when
// Blazor re-renders the gutters. Widths live in the container's --bom-left / --bom-right CSS variables (the
// markup never sets that style attribute, so Blazor leaves them alone) and are remembered in localStorage.
// Double-click a gutter to go back to the default widths.
window.bomSplit = {
    init: function (el) {
        if (!el || el._bomSplit) return;
        el._bomSplit = true;

        var KEY = 'bom-split', GUT = 8, DEF = { left: 300, right: 380 }, MIN = { left: 220, right: 290, mid: 380 };
        var w = { left: DEF.left, right: DEF.right };

        function clamp() {
            var total = el.clientWidth - GUT * 2;
            w.left = Math.max(MIN.left, Math.min(w.left, total - w.right - MIN.mid));
            w.right = Math.max(MIN.right, Math.min(w.right, total - w.left - MIN.mid));
        }
        function apply() {
            clamp();
            el.style.setProperty('--bom-left', w.left + 'px');
            el.style.setProperty('--bom-right', w.right + 'px');
        }
        function save() {
            try { localStorage.setItem(KEY, JSON.stringify(w)); } catch (e) { }
        }

        try {
            var s = JSON.parse(localStorage.getItem(KEY) || 'null');
            if (s && s.left > 0 && s.right > 0) { w.left = s.left; w.right = s.right; }
        } catch (e) { }
        apply();
        window.addEventListener('resize', apply);

        el.addEventListener('dblclick', function (e) {
            if (!e.target.closest('.bom-gutter')) return;
            w.left = DEF.left; w.right = DEF.right; apply(); save();
        });

        el.addEventListener('pointerdown', function (e) {
            var g = e.target.closest('.bom-gutter');
            if (!g || e.button !== 0) return;
            e.preventDefault();
            var idx = g.getAttribute('data-gutter') === '0' ? 'left' : 'right';
            var startX = e.clientX, startW = w[idx];
            g.setPointerCapture(e.pointerId);
            g.classList.add('drag');
            document.body.style.userSelect = 'none';

            function move(ev) {
                var dx = ev.clientX - startX;
                w[idx] = idx === 'left' ? startW + dx : startW - dx;
                apply();
            }
            function up() {
                g.classList.remove('drag');
                document.body.style.userSelect = '';
                g.removeEventListener('pointermove', move);
                g.removeEventListener('pointerup', up);
                g.removeEventListener('pointercancel', up);
                save();
            }
            g.addEventListener('pointermove', move);
            g.addEventListener('pointerup', up);
            g.addEventListener('pointercancel', up);
        });
    }
};
