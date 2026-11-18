(() => {
    'use strict';
    document.addEventListener('focusin', event => {
        const input = event.target.closest('[data-manila-date-min]');
        if (!input) return;
        const parts = new Intl.DateTimeFormat('en-CA', {
            timeZone: 'Asia/Manila', year: 'numeric', month: '2-digit', day: '2-digit'
        }).formatToParts(new Date());
        const part = type => parts.find(item => item.type === type).value;
        const today = `${part('year')}-${part('month')}-${part('day')}T00:00`;
        if (today > input.min) input.min = today;
    });
    let fallbackPreviewUrl;
    document.addEventListener('change', event => {
        const input = event.target.closest('input[type="file"]');
        if (!input || input.closest('#record-dialog')) return;
        const host = input.closest('form')?.querySelector('[data-record-preview]');
        if (!host) return;
        if (fallbackPreviewUrl) URL.revokeObjectURL(fallbackPreviewUrl);
        fallbackPreviewUrl = null;
        host.replaceChildren();
        host.hidden = true;
        const file = input.files?.[0];
        if (!file || !file.type?.startsWith('image/') || !URL.createObjectURL) return;
        fallbackPreviewUrl = URL.createObjectURL(file);
        const image = document.createElement('img');
        image.src = fallbackPreviewUrl;
        image.alt = host.dataset.recordPreviewAlt || 'Preview of the selected image';
        image.className = 'record-dialog-preview-image';
        host.append(image);
        host.hidden = false;
    });
    const dialog = document.querySelector('#record-dialog');
    if (!dialog || typeof dialog.showModal !== 'function') return;
    const body = dialog.querySelector('[data-record-body]');
    const message = dialog.querySelector('[data-record-message]');
    const recovery = dialog.querySelector('[data-record-recovery]');
    const subtitle = dialog.querySelector('[data-record-subtitle]');
    let trigger, busy = false, request, previewUrl;
    const lockedControls = new Set();
    const headers = { 'X-Record-Dialog': 'true' };
    const restoreKey = 'attendta-record-focus';
    const previous = sessionStorage.getItem(restoreKey);
    if (previous) {
        sessionStorage.removeItem(restoreKey);
        const restored = Array.from(document.querySelectorAll('[data-record-open]')).find(a => a.dataset.recordOpen === previous);
        const unchanged = document.querySelector('[data-member-edit-unchanged]');
        if (restored && unchanged && previous.startsWith('edit-member-')) {
            unchanged.classList.add('record-result-sticky');
            restored.classList.add('record-result-focus');
            const descriptions = (restored.getAttribute('aria-describedby') || '').split(/\s+/).filter(Boolean);
            if (!descriptions.includes(unchanged.id)) descriptions.push(unchanged.id);
            restored.setAttribute('aria-describedby', descriptions.join(' '));
        }
        (restored || document.querySelector('[data-record-open]'))?.focus();
        if (restored && unchanged && previous.startsWith('edit-member-')) {
            window.addEventListener('load', () => {
                if (document.activeElement === restored) restored.scrollIntoView({ block: 'center', inline: 'nearest' });
            }, { once: true });
        }
    }
    function focusForm() {
        const errors = body.querySelector('.validation-summary-errors');
        (errors || body.querySelector('.input-validation-error:not([type="hidden"]):not([disabled])') || body.querySelector('input:not([type="hidden"]):not([readonly]):not([disabled]), textarea, select, button'))?.focus();
    }
    function clearPreview() {
        if (previewUrl) {
            if (typeof URL !== 'undefined' && URL.revokeObjectURL) URL.revokeObjectURL(previewUrl);
            previewUrl = null;
        }
        body.querySelectorAll('[data-record-preview]').forEach(host => {
            host.replaceChildren();
            host.hidden = true;
        });
    }
    function previewFile(input) {
        const host = body.querySelector('[data-record-preview]');
        const file = input.files?.[0];
        if (!host || !file || !file.type?.startsWith('image/') || typeof URL === 'undefined' || !URL.createObjectURL) return;
        previewUrl = URL.createObjectURL(file);
        const image = document.createElement('img');
        image.src = previewUrl;
        image.alt = host.dataset.recordPreviewAlt || 'Preview of the selected image';
        image.className = 'record-dialog-preview-image';
        host.replaceChildren(image);
        host.hidden = false;
    }
    body.addEventListener('change', event => {
        const input = event.target.closest('input[type="file"]');
        if (!input) return;
        clearPreview();
        previewFile(input);
    });
    function setBusy(value) {
        busy = value;
        dialog.setAttribute('aria-busy', String(value));
        if (value) {
            dialog.querySelectorAll('button, input, select, textarea').forEach(control => {
                if (!control.disabled) { lockedControls.add(control); control.disabled = true; }
            });
        } else {
            lockedControls.forEach(control => { control.disabled = false; });
            lockedControls.clear();
        }
    }
    async function load() {
        request?.abort();
        request = new AbortController();
        const current = request;
        clearPreview();
        body.replaceChildren();
        recovery.hidden = true;
        message.textContent = 'Loading editor…';
        try {
            const response = await fetch(trigger.href, { headers, signal: current.signal });
            const html = await response.text();
            if (!response.ok || response.redirected || !html.includes('data-record-form')) throw new Error();
            if (request !== current || !dialog.open) return;
            body.innerHTML = html;
            message.textContent = '';
            window.AttendTaDialogs?.track(dialog, true);
            focusForm();
        } catch (error) {
            if (error.name === 'AbortError' || request !== current) return;
            message.textContent = 'The editor could not be loaded. Retry or open the full page to continue.';
            recovery.hidden = false;
            message.focus();
        }
    }
    document.addEventListener('click', event => {
        const link = event.target.closest('[data-record-open]');
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
        event.preventDefault();
        trigger = link;
        dialog.querySelector('#record-dialog-title').textContent = link.dataset.recordTitle;
        if (subtitle) {
            subtitle.textContent = link.dataset.recordSubtitle ?? '';
            subtitle.hidden = !subtitle.textContent;
        }
        dialog.querySelector('[data-record-fallback]').href = link.href;
        dialog.showModal();
        load();
    });
    dialog.addEventListener('click', event => {
        if (event.target.closest('[data-record-close]') && !busy) window.AttendTaDialogs?.requestClose(dialog);
        if (event.target.closest('[data-record-retry]') && !busy) load();
    });
    dialog.addEventListener('cancel', event => { if (busy) event.preventDefault(); });
    dialog.addEventListener('keydown', event => {
        if (event.key !== 'Tab') return;
        const controls = Array.from(dialog.querySelectorAll('a[href], button, input, textarea, select, [tabindex]'))
            .filter(element => !element.disabled && element.tabIndex >= 0 && element.getClientRects().length);
        const first = controls[0], last = controls[controls.length - 1];
        if (!first) { event.preventDefault(); return; }
        if (event.shiftKey && (document.activeElement === first || !controls.includes(document.activeElement))) {
            event.preventDefault(); last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !controls.includes(document.activeElement))) {
            event.preventDefault(); first.focus();
        }
    });
    dialog.addEventListener('close', () => { request?.abort(); clearPreview(); trigger?.focus(); });
    dialog.addEventListener('submit', async event => {
        const form = event.target.closest('[data-record-form]');
        if (!form) return;
        event.preventDefault();
        if (busy) return;
        const data = new FormData(form);
        setBusy(true);
        message.textContent = 'Saving…';
        recovery.hidden = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: data, headers });
            if (!response.ok || response.redirected) throw new Error();
            if (response.headers.get('content-type')?.includes('application/json')) {
                const result = await response.json();
                if (result.saved !== true) throw new Error();
                sessionStorage.setItem(restoreKey, trigger.dataset.recordOpen);
                location.reload();
                return;
            }
            const html = await response.text();
            if (!html.includes('data-record-form')) throw new Error();
            clearPreview();
            body.innerHTML = html;
            message.textContent = body.querySelector('[data-record-conflict]')
                ? 'The member could not be saved. Your input is retained; follow the recovery instructions.'
                : 'Please correct the highlighted errors.';
            setBusy(false);
            focusForm();
        } catch {
            message.textContent = 'The save could not be confirmed. Your input is retained. Check the list in another tab before trying again to avoid a duplicate save.';
            setBusy(false);
            message.focus();
        }
    });
})();
