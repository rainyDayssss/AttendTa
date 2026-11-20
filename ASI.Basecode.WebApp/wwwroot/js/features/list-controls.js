(() => {
    'use strict';
    const searches = Array.from(document.querySelectorAll('[data-list-search]'));
    const filterValues = form => Array.from(form.elements || [])
        .filter(field => field.name && !['hidden', 'submit', 'button'].includes(field.type))
        .map(field => [field.name, field.value, field.checked]);
    const appliedFilters = new Map(searches.map(form => [form, JSON.stringify(filterValues(form))]));
    const disclosures = Array.from(document.querySelectorAll('[data-expandable-text]')).map(host => {
        const content = host.querySelector('[data-expandable-content]');
        const toggle = host.querySelector('[data-expandable-toggle]');
        const label = toggle.querySelector('[data-expandable-label]');
        let expanded = false;
        function measure() {
            if (expanded) return;
            content.classList.add('line-clamp-two');
            toggle.hidden = content.scrollHeight <= content.clientHeight + 1;
            if (toggle.hidden) content.classList.remove('line-clamp-two');
        }
        toggle.addEventListener('click', () => {
            expanded = !expanded;
            content.classList.toggle('line-clamp-two', !expanded);
            toggle.setAttribute('aria-expanded', String(expanded));
            label.textContent = expanded ? 'Read less' : 'Read more';
            if (!expanded) measure();
        });
        return measure;
    });
    if (disclosures.length) {
        const measure = () => disclosures.forEach(refresh => refresh());
        if (document.fonts) document.fonts.ready.then(measure); else measure();
        let disclosureTimer;
        window.addEventListener('resize', () => {
            clearTimeout(disclosureTimer);
            disclosureTimer = setTimeout(measure, 150);
        });
    }
    // AJAX record saves already reload the current URL; their JSON results stay intact.
    document.addEventListener('submit', event => {
        const form = event.target;
        if (form.method !== 'post' || !form.closest('main, dialog')) return;
        const current = new URL(window.location.href);
        const action = new URL(form.action, current);
        if (action.origin !== current.origin) return;
        current.searchParams.forEach((value, key) => {
            if (!action.searchParams.has(key)) action.searchParams.set(key, value);
        });
        form.action = action.toString();
        let origin = form.querySelector('[name="uiReturnUrl"]');
        if (!origin) {
            origin = document.createElement('input');
            origin.type = 'hidden'; origin.name = 'uiReturnUrl'; form.append(origin);
        }
        origin.value = current.pathname + current.search;
    }, true);

    const footer = document.querySelector('[data-paging-size]');
    // A POST re-render sits on a POST-only action, so a fit here would GET that action and 405.
    // The re-render already carries the reader's page size, so there is nothing to fit.
    if (!footer || footer.dataset.pagingMode === 'fixed' || footer.dataset.pagingFit === 'off') return;
    let resized = false, retryPending = false, timer;
    let viewportWidth = window.innerWidth, viewportHeight = window.innerHeight;
    // Storage can be absent (tests, hardened browsers) or throw when blocked; the fit
    // still works without it, it just loses the cross-navigation memory.
    function fittedAlready(key, size) {
        try { return window.sessionStorage?.getItem(key) === String(size); } catch (error) { return false; }
    }
    function fitRows() {
        // A rejected POST uses one-shot TempData. Navigating before the ready handler reopens
        // its dialog (or while its off-page fallback is visible) would discard that recovery.
        // Keep the current page stable for its lifetime, even after the dialog is dismissed.
        if (document.querySelector('[data-list-preserve]')) return;
        if (document.querySelector('dialog[open]') || document.activeElement?.matches('input, select, textarea') ||
            document.querySelector('[data-bulk-row]:checked') ||
            document.querySelector('[data-expandable-toggle][aria-expanded="true"]') ||
            searches.some(form => form.querySelector('[name="search"]').value.trim() !== form.dataset.appliedSearch.trim() ||
                JSON.stringify(filterValues(form)) !== appliedFilters.get(form))) {
            retryPending = true;
            return;
        }
        retryPending = false;
        const table = footer.parentElement.querySelector('.table-scroll table') || document.querySelector('.table-scroll table');
        const grid = table ? null : document.querySelector('[data-paging-grid]');
        if (!table && !grid) return;
        const rows = Array.from(table ? table.querySelectorAll('tbody > tr') : grid.children).filter(row => !row.querySelector('[colspan]'));
        if (!rows.length) return;
        const size = Number(footer.dataset.pagingSize);
        const rowHeight = Math.max(48, ...rows.map(row => row.getBoundingClientRect().height));
        const top = (table ? rows[0] : grid).getBoundingClientRect().top + window.scrollY;
        const columns = grid ? rows.filter(row => Math.abs(row.getBoundingClientRect().top - rows[0].getBoundingClientRect().top) < 2).length : 1;
        const gap = grid ? parseFloat(window.getComputedStyle(grid).rowGap) || 0 : 0;
        const available = window.innerHeight - top - footer.getBoundingClientRect().height - 32;
        const minimum = 5;
        const maximum = window.innerWidth < 768 ? 5 : 10;
        const fitted = Math.max(1, Math.floor((available + gap) / (rowHeight + gap))) * columns;
        const total = Number(footer.dataset.pagingTotal) || 25;
        const desired = Math.max(minimum, Math.min(maximum, total, fitted));
        const url = new URL(window.location.href);
        const withinBounds = size >= minimum && size <= maximum;
        // Hold the size while paging the same query. Refit after a filter change
        // or a real resize, rather than repeatedly navigating for different row text.
        const fitMarker = window.innerWidth < 768 ? '3m' : '3d';
        const marker = url.searchParams.get('fit');
        if (marker === fitMarker && !resized && withinBounds) return;
        // One fit per result set and viewport height per session. `fit` is a query
        // parameter a user can strip, so the guard cannot depend on it alone: without
        // this, a stripped link or a mobile URL bar that changes the viewport height
        // reloads the page on every navigation.
        const criteria = new URLSearchParams(url.search);
        ['page', 'pageSize', 'fit'].forEach(key => criteria.delete(key));
        criteria.sort();
        const fitKey = `attendta-fit-v4:${url.pathname}:${criteria}:${footer.dataset.pagingTotal}:${maximum}:${window.innerHeight}`;
        if (withinBounds && !resized && (!marker || marker === fitMarker) && fittedAlready(fitKey, size)) return;
        // There is nothing else to fetch when the whole result already fits.
        if (withinBounds && total <= size && desired >= total) return;
        if (desired === size) return;
        const firstIndex = (Number(footer.dataset.pagingPage) - 1) * size;
        url.searchParams.set('pageSize', desired);
        url.searchParams.set('page', Math.floor(firstIndex / desired) + 1);
        url.searchParams.set('fit', fitMarker);
        try { window.sessionStorage?.setItem(fitKey, String(desired)); } catch (error) { /* storage disabled: fall through to the fit param guard */ }
        window.location.replace(url.toString());
    }
    window.addEventListener('resize', () => {
        // Scrollbar appearance and small panel jitter must not restart the paging fit.
        const crossedBreakpoint = (window.innerWidth < 768) !== (viewportWidth < 768);
        if (!crossedBreakpoint && Math.abs(window.innerWidth - viewportWidth) < 32 && Math.abs(window.innerHeight - viewportHeight) < 32) return;
        viewportWidth = window.innerWidth;
        viewportHeight = window.innerHeight;
        resized = true;
        clearTimeout(timer);
        timer = setTimeout(fitRows, 400);
    });
    // Revisit a blocked fit only after the user leaves or closes the active control.
    const retryFit = () => { if (retryPending) setTimeout(fitRows, 0); };
    document.addEventListener('focusout', retryFit);
    document.addEventListener('close', retryFit, true);
    document.addEventListener('change', retryFit);
    document.addEventListener('click', event => {
        if (event.target.closest('[data-expandable-toggle], [data-bulk-clear]')) retryFit();
    });
    if (document.fonts) document.fonts.ready.then(fitRows);
    else fitRows();
})();
