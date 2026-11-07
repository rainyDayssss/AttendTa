(() => {
    'use strict';
    const dialog = document.querySelector('#record-dialog');
    if (!dialog || typeof dialog.showModal !== 'function') return;
    const body = dialog.querySelector('[data-record-body]');
    const message = dialog.querySelector('[data-record-message]');
    const recovery = dialog.querySelector('[data-record-recovery]');
    let trigger, busy = false, request;
    const headers = { 'X-Record-Dialog': 'true' };
    const restoreKey = 'attendta-record-focus';
    const previous = sessionStorage.getItem(restoreKey);
    if (previous) {
        sessionStorage.removeItem(restoreKey);
        (Array.from(document.querySelectorAll('[data-record-open]')).find(a => a.dataset.recordOpen === previous)
            || document.querySelector('[data-record-open]'))?.focus();
    }
    function focusForm() {
        const errors = body.querySelector('.validation-summary-errors');
        const control = body.querySelector('input:not([type="hidden"]):not([readonly]):not([disabled]), textarea:not([readonly]):not([disabled]), select:not([disabled]), button:not([disabled])');
        (errors || control)?.focus();
    }
    function setBusy(value) {
        busy = value;
        dialog.setAttribute('aria-busy', String(value));
        dialog.querySelectorAll('button').forEach(button => { button.disabled = value; });
    }
    async function load() {
        request?.abort();
        request = new AbortController();
        const current = request;
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
        dialog.querySelector('[data-record-fallback]').href = link.href;
        dialog.showModal();
        load();
    });
    dialog.addEventListener('click', event => {
        if (event.target.closest('[data-record-close]') && !busy) dialog.close();
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
    dialog.addEventListener('close', () => { request?.abort(); trigger?.focus(); });
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
            body.innerHTML = html;
            message.textContent = 'Please correct the highlighted errors.';
            setBusy(false);
            focusForm();
        } catch {
            message.textContent = 'The save could not be confirmed. Your input is retained. Check the list in another tab before trying again to avoid a duplicate save.';
            setBusy(false);
            message.focus();
        }
    });
})();
