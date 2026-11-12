(() => {
    'use strict';
    const main = document.getElementById('main-content');
    if (!main) return;
    const status = main.querySelector('[data-list-status]');
    if (!status) return;

    const BUSY_TIMEOUT = 15000;
    const regions = Array.from(main.querySelectorAll('[data-list-region]'));
    const busyLabel = status.dataset.listBusyLabel || 'Loading…';

    // site.js disables a submitted button and swaps its label. Remember the pre-submit state so a
    // bfcache restore can put the list back the way the user left it.
    const submits = new Map();
    main.querySelectorAll('form[method="get"] button[type="submit"], form.inline-form button[type="submit"]')
        .forEach(button => submits.set(button, { text: button.textContent, disabled: button.disabled }));

    let timer;

    function setBusy(label) {
        clearTimeout(timer);
        // The empty line is reserved by CSS, so the region stays in the accessibility tree and the
        // text change is announced; hiding it would suppress the announcement.
        status.textContent = label;
        regions.forEach(region => region.setAttribute('aria-busy', 'true'));
        // Escape or the browser's Stop button can abort a navigation without a pageshow, so never
        // leave the list stuck busy. A submitted toggle stays disabled: its POST may still be in
        // flight, and re-enabling it would invite a second submission.
        timer = setTimeout(function () {
            regions.forEach(region => region.removeAttribute('aria-busy'));
            status.textContent = 'This is taking longer than expected. The action may not have completed.';
        }, BUSY_TIMEOUT);
    }

    function clearBusy() {
        clearTimeout(timer);
        status.textContent = '';
        regions.forEach(region => region.removeAttribute('aria-busy'));
        submits.forEach((state, button) => {
            button.disabled = state.disabled;
            button.textContent = state.text;
        });
    }

    // A filter tab or pager link keeps the current document on screen until the response arrives,
    // so the paragraph's own label is the truthful one to show.
    main.addEventListener('click', event => {
        if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest('.filter-tab, a[rel="prev"], a[rel="next"]');
        if (!link || !main.contains(link)) return;
        setBusy(busyLabel);
    });

    // Search is a GET form; the status toggles are inline POST forms. Both leave the document, so
    // the busy state only has to survive the wait, not an in-page update.
    main.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (form.method !== 'get' && !form.classList.contains('inline-form')) return;
        const submit = form.querySelector('button[type="submit"]');
        setBusy(submit?.dataset.submittingLabel || busyLabel);
    });

    // Only a bfcache restore needs clearing; the first pageshow must not wipe a busy state the user
    // just started.
    window.addEventListener('pageshow', event => {
        if (event.persisted) clearBusy();
    });
})();
