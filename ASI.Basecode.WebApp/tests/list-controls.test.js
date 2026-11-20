const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function load({ width = 1366, height = 720, top = 250, rowHeight = 50, total = 50, size = 5, url = 'http://localhost/Member?pageSize=5', editing = false, expanded = false, search = null, paging = true, grid = false, mode = 'auto', fit = null, storage = null, selection = null, preserve = false } = {}) {
    const events = {}, changes = [], forms = [];
    const table = { getBoundingClientRect: () => ({ top }), querySelector: () => ({ getBoundingClientRect: () => ({ height: 40 }) }), querySelectorAll: () => Array.from({ length: 5 }, () => ({ querySelector: () => null, getBoundingClientRect: () => ({ top: top + 40, height: rowHeight }) })) };
    const footer = { dataset: { pagingSize: String(size), pagingPage: new URL(url).searchParams.get('page') || '1', pagingMode: mode, pagingTotal: String(total), pagingFit: fit }, getBoundingClientRect: () => ({ height: 64 }), parentElement: { querySelector: () => grid ? null : table } };
    const cards = { children: Array.from({ length: 5 }, (_, i) => ({ querySelector: () => null, getBoundingClientRect: () => ({ top: top + Math.floor(i / 2) * 220, height: 200 }) })), getBoundingClientRect: () => ({ top }) };
    if (search) forms.push(search);
    const document = {
        activeElement: { matches: () => editing },
        querySelectorAll: selector => selector === '[data-list-search]' ? forms : [],
        querySelector: selector => selector === '[data-list-preserve]' ? preserve ? {} : null : selector === '[data-paging-size]' ? paging ? footer : null : selector === 'dialog[open]' ? null : selector === '[data-bulk-row]:checked' ? selection?.checked ? {} : null : selector === '[data-expandable-toggle][aria-expanded="true"]' ? expanded ? {} : null : selector === '[data-paging-grid]' ? cards : grid ? null : table,
        addEventListener: (event, callback) => { events[event] = callback; },
        createElement: () => ({})
    };
    const window = { getComputedStyle: () => ({ rowGap: '20px' }), innerWidth: width, innerHeight: height, scrollY: 0, sessionStorage: storage, location: { href: url, replace: value => changes.push(value) }, addEventListener: (event, callback) => { events[event] = callback; } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/features/list-controls.js'), 'utf8'), { document, window, URL, URLSearchParams, setTimeout: callback => { callback(); return 1; }, clearTimeout() {} });
    return { events, changes, window, document };
}

test('auto fit uses the real row height and preserves filters when requesting fewer rows', () => {
    const { changes } = load({ height: 700, size: 12, url: 'http://localhost/Member?search=Ana&course=BSIT&pageSize=12' });
    assert.equal(changes.length, 1);
    const url = new URL(changes[0]);
    assert.equal(url.searchParams.get('pageSize'), '6');
    assert.equal(url.searchParams.get('search'), 'Ana');
    assert.equal(url.searchParams.get('course'), 'BSIT');
    assert.equal(url.searchParams.get('fit'), '3d');
});

test('failed decision recovery prevents automatic navigation before ready and on resize', () => {
    for (const url of ['http://localhost/RegistrationReview?status=confirmed', 'http://localhost/Event/Details/7?status=confirmed']) {
        const { changes, events, window } = load({ height: 1000, url, preserve: true });
        assert.equal(changes.length, 0);
        window.innerWidth = 390;
        events.resize();
        events.close();
        assert.equal(changes.length, 0);
        assert.equal(load({ height: 1000, url }).changes.length, 1);
    }
});

test('fit waits for editing and uses taller screens beyond five rows', () => {
    assert.equal(load({ height: 500, editing: true }).changes.length, 0);
    assert.equal(new URL(load({ height: 1000 }).changes[0]).searchParams.get('pageSize'), '10');
    assert.equal(new URL(load({ height: 2400 }).changes[0]).searchParams.get('pageSize'), '10');
    assert.equal(load({ height: 1000, total: 4 }).changes.length, 0);
});

test('paging a fitted query is stable until a real resize', () => {
    const { changes, events, window } = load({ height: 500, size: 10, url: 'http://localhost/Member?pageSize=10&fit=3d&page=2' });
    assert.equal(changes.length, 0);
    window.innerWidth = 1200;
    events.resize();
    assert.equal(changes.length, 1);
    const url = new URL(changes[0]);
    assert.equal(url.searchParams.get('pageSize'), '5');
    assert.equal(url.searchParams.get('page'), '3');
});

test('dossier has a five-row minimum but grows on taller screens', () => {
    assert.equal(load({ height: 600, top: 650, mode: 'roster' }).changes.length, 0);
    assert.equal(new URL(load({ height: 1400, top: 650, mode: 'roster' }).changes[0]).searchParams.get('pageSize'), '10');
});

test('old fitted URLs are recalculated once for the current viewport', () => {
    const { changes } = load({ height: 1000, url: 'http://localhost/Member?pageSize=5&fit=1' });
    assert.equal(new URL(changes[0]).searchParams.get('pageSize'), '10');
    assert.equal(new URL(changes[0]).searchParams.get('fit'), '3d');
});

test('scrollbar-sized viewport changes do not restart a fitted page', () => {
    const { events, changes, window } = load({ height: 500, url: 'http://localhost/Member?fit=3d' });
    window.innerWidth -= 15;
    events.resize();
    assert.equal(changes.length, 0);
});

test('a pending search is not lost to an automatic viewport fit', () => {
    const search = { dataset: { appliedSearch: 'Ana' }, querySelector: () => ({ value: 'Ben' }) };
    const { changes } = load({ search, height: 1000 });
    assert.equal(changes.length, 0);
});

test('an expanded description is not lost to automatic paging on resize', () => {
    const { changes, events, window } = load({ expanded: true });
    window.innerWidth -= 100;
    events.resize();
    assert.equal(changes.length, 0);
});

test('list POSTs preserve their original view and do not replace the action route', () => {
    const { events } = load({ paging: false, url: 'http://localhost/Member?search=Ana&page=2&pageSize=3' });
    let origin;
    const form = { method: 'post', action: 'http://localhost/Member/Deactivate/7', closest: () => ({}), querySelector: () => origin, append: input => { origin = input; } };
    events.submit({ target: form });
    assert.equal(new URL(form.action).pathname, '/Member/Deactivate/7');
    assert.equal(new URL(form.action).searchParams.get('search'), 'Ana');
    assert.equal(origin.value, '/Member?search=Ana&page=2&pageSize=3');
});

test('short screens keep five records for tables and card grids', () => {
    for (const grid of [false, true]) {
        const { changes } = load({ height: 500, size: 12, grid, url: 'http://localhost/Member?pageSize=12' });
        assert.equal(new URL(changes[0]).searchParams.get('pageSize'), '5');
    }
    assert.equal(load({ height: 500, total: 3 }).changes.length, 0);
});

test('legacy small fitted pages retain their first record in the new page', () => {
    const { changes } = load({ height: 500, size: 2, url: 'http://localhost/Member?search=Ana&pageSize=2&page=4&fit=2' });
    const url = new URL(changes[0]);
    assert.equal(url.searchParams.get('pageSize'), '5');
    assert.equal(url.searchParams.get('page'), '2');
    assert.equal(url.searchParams.get('search'), 'Ana');
});

test('mobile caps even previously fitted pages and small result sets', () => {
    const storage = { getItem: () => '1', setItem: () => {} };
    for (const grid of [false, true]) {
        const { changes } = load({ width: 390, height: 1200, total: 7, size: 10, grid, storage, url: 'http://localhost/Member?search=Ana&pageSize=10&fit=2' });
        const url = new URL(changes[0]);
        assert.equal(url.searchParams.get('pageSize'), '5');
        assert.equal(url.searchParams.get('search'), 'Ana');
    }
});

test('old desktop sizes above the cap keep their first item', () => {
    const { changes } = load({ width: 1024, height: 1200, size: 25, url: 'http://localhost/Member?pageSize=25&page=2&fit=2' });
    const url = new URL(changes[0]);
    assert.equal(url.searchParams.get('pageSize'), '10');
    assert.equal(url.searchParams.get('page'), '3');
});

test('a mobile-fitted link refits for desktop', () => {
    const storage = { getItem: () => '1', setItem: () => {} };
    const { changes } = load({ width: 1024, height: 1200, storage, url: 'http://localhost/Member?pageSize=5&fit=3m' });
    const url = new URL(changes[0]);
    assert.equal(url.searchParams.get('pageSize'), '10');
    assert.equal(url.searchParams.get('fit'), '3d');
});

test('deferred fit does not discard an unapplied filter', () => {
    const filter = { name: 'course', type: 'select-one', value: '' };
    const search = { dataset: { appliedSearch: '' }, elements: [filter], querySelector: () => ({ value: '' }) };
    const { changes, events, window, document } = load({ width: 775, height: 1000, size: 10, editing: true, search, url: 'http://localhost/Member?pageSize=10&fit=3d' });
    window.innerWidth = 760;
    events.resize();
    filter.value = 'BSIT';
    document.activeElement.matches = () => false;
    events.focusout();
    assert.equal(changes.length, 0);
});

test('a pending fit preserves bulk selections until deselected', () => {
    const selection = { checked: true };
    const { changes, events } = load({ width: 390, size: 10, selection, url: 'http://localhost/Member?pageSize=10&fit=3d' });
    events.focusout();
    assert.equal(changes.length, 0);
    selection.checked = false;
    events.change();
    assert.equal(new URL(changes[0]).searchParams.get('pageSize'), '5');
});

test('fit memory distinguishes filters with the same result count', () => {
    const keys = new Map();
    const storage = { getItem: key => keys.get(key) || null, setItem: (key, value) => keys.set(key, value) };
    assert.equal(load({ height: 1000, storage, url: 'http://localhost/Member?course=BSIT' }).changes.length, 1);
    assert.equal(load({ height: 1000, storage, url: 'http://localhost/Member?course=BSCS' }).changes.length, 1);
    assert.equal(load({ height: 1000, storage, url: 'http://localhost/Member?course=BSIT&pageSize=5' }).changes.length, 1);
});

test('a fit postponed while editing retries after focus leaves', () => {
    const { changes, events, window, document } = load({ width: 775, height: 1000, size: 10, editing: true, url: 'http://localhost/Member?pageSize=10&fit=3d' });
    window.innerWidth = 760;
    events.resize();
    assert.equal(changes.length, 0);
    document.activeElement.matches = () => false;
    events.focusout();
    assert.equal(new URL(changes[0]).searchParams.get('pageSize'), '5');
});

test('crossing the mobile breakpoint refits even for a small width change', () => {
    const { changes, events, window } = load({ width: 775, height: 1000, size: 10, url: 'http://localhost/Member?pageSize=10&fit=3d' });
    assert.equal(changes.length, 0);
    window.innerWidth = 760;
    events.resize();
    assert.equal(new URL(changes[0]).searchParams.get('pageSize'), '5');
});

test('a POST re-render is never refitted, on load or on resize', () => {
    // A batch POST re-renders on its own POST-only action, so any fit would GET that action and 405.
    const url = 'http://localhost/RegistrationReview/ApproveSelected?status=pending&pageSize=5';
    const { changes, events } = load({ height: 1000, fit: 'off', url });
    assert.equal(changes.length, 0);
    // The fit never wires itself up, so a resize cannot navigate to the POST-only action either.
    assert.equal(events.resize, undefined);
    // The same page as a GET view still fits, so the mark is what stops it, not the URL.
    assert.equal(new URL(load({ height: 1000, url }).changes[0]).searchParams.get('pageSize'), '10');
});
