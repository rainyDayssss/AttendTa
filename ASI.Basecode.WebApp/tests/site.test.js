const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

function createElement(tagName) {
    const element = {
        tagName: tagName.toUpperCase(),
        children: [],
        attributes: {},
        className: '',
        id: '',
        textContent: '',
        innerHTML: '',
        dataset: {},
        listeners: {},
        parent: null,
        appendChild(child) {
            child.parent = element;
            element.children.push(child);
            return child;
        },
        prepend(child) {
            child.parent = element;
            element.children.unshift(child);
            return child;
        },
        remove() {
            if (!element.parent) return;
            const index = element.parent.children.indexOf(element);
            if (index >= 0) element.parent.children.splice(index, 1);
        },
        setAttribute(name, value) {
            element.attributes[name] = String(value);
        },
        addEventListener(type, handler) {
            (element.listeners[type] = element.listeners[type] || []).push(handler);
        },
        querySelectorAll() { return []; },
        querySelector() {
            return null;
        },
        focus(options) {
            element.focused = true;
            element.focusOptions = options;
        },
        closest() {
            return null;
        },
        showModal() {
            element.open = true;
        }
    };
    return element;
}

// The fetched-dialog host receives the partial's markup and is asked for its <dialog>. This mock
// keeps the one dialog the markup stands for so a test can fire the native close event.
function createDialogHost() {
    const dialog = createElement('dialog');
    const host = createElement('div');
    let markup = '';
    Object.defineProperty(host, 'innerHTML', {
        get: () => markup,
        set(value) {
            markup = value;
            host.children.length = 0;
            if (value) host.appendChild(dialog);
        }
    });
    host.querySelector = selector => (selector === 'dialog' ? dialog : null);
    return { host, dialog };
}

function runSite(document, fetch) {
    const $ = callback => callback();
    $.ajaxSetup = () => {};

    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'site.js'), 'utf8');
    vm.runInNewContext(source, { $, document, fetch, console, window: {} });

    const handlers = document.clickHandlers || [];
    assert.ok(handlers.length > 0, 'site.js must register a click handler');
    // The fetched-dialog handler is registered last, after the generic dialog handlers.
    return handlers[handlers.length - 1];
}

function clickEvent(trigger) {
    return {
        target: { closest: () => trigger },
        preventDefault() {}
    };
}

const flush = () => new Promise(resolve => setImmediate(resolve));

test('cancelled empty manual searches do not leave the search button busy', () => {
    const button = createElement('button');
    button.textContent = 'Search';
    button.dataset.submittingLabel = 'Searching…';
    const input = createElement('input');
    const form = createElement('form');
    form.querySelector = selector => selector === 'button[type="submit"]' ? button : null;
    const root = {
        querySelector: selector => selector === '[data-lookup-form]' ? form : selector === '#lookup-query' ? input : null,
        querySelectorAll: () => []
    };
    const handlers = {};
    const document = {
        body: createElement('body'), clickHandlers: [], createElement,
        querySelector: selector => selector === '[data-scanner]' ? root : null,
        getElementById: () => null,
        addEventListener(type, handler) {
            (handlers[type] ||= []).push(handler);
            if (type === 'click') this.clickHandlers.push(handler);
        }
    };
    runSite(document, () => {});
    const manual = fs.readFileSync(path.join(__dirname, '../wwwroot/js/features/scanner-manual.js'), 'utf8');
    vm.runInNewContext(manual, { document });
    for (const value of ['', '   ']) {
        input.value = value;
        const event = { target: form, submitter: button, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
        form.listeners.submit[0](event);
        handlers.submit[0](event);
        assert.equal(event.defaultPrevented, true);
        assert.equal(button.disabled, undefined);
        assert.equal(button.textContent, 'Search');
        assert.equal(input.focused, true);
    }
    input.value = 'Alex';
    button.getAttribute = () => 'Searching…';
    const event = { target: form, submitter: button, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
    form.listeners.submit[0](event);
    handlers.submit[0](event);
    assert.equal(event.defaultPrevented, false);
    assert.equal(button.disabled, true);
    assert.equal(button.textContent, 'Searching…');
});

test('row details close their menu and restore focus to its visible invoker', () => {
    const body = createElement('body'), dialog = createElement('dialog'), invoker = createElement('button');
    const menu = { id: 'row-menu-test', hidePopover() { this.hidden = true; } };
    const trigger = { dataset: { dialogOpen: 'details-test' }, closest: selector => selector === '[popover]' ? menu : null };
    const document = {
        body, clickHandlers: [], createElement,
        querySelector: selector => selector === '[popovertarget="row-menu-test"]' ? invoker : null,
        getElementById: id => id === 'details-test' ? dialog : null,
        addEventListener(type, handler) { if (type === 'click') this.clickHandlers.push(handler); }
    };
    runSite(document, () => Promise.reject(new Error('No fetch expected')));
    document.clickHandlers[0]({ target: { closest: selector => selector === '[data-dialog-open]' ? trigger : null } });
    assert.equal(menu.hidden, true);
    assert.equal(dialog.open, true);
    dialog.listeners.close[0]();
    assert.equal(invoker.focused, true);
    assert.equal(invoker.focusOptions.preventScroll, true);
});

test('a dialog opens from a row menu without the Popover API', () => {
    const body = createElement('body'), dialog = createElement('dialog'), invoker = createElement('button');
    const menu = { id: 'row-menu-test' };
    const trigger = { tagName: 'A', dataset: { dialogOpen: 'event-cancel' }, closest: selector => selector === '[popover]' ? menu : null };
    const document = {
        body, clickHandlers: [], createElement,
        querySelector: selector => selector === '[popovertarget="row-menu-test"]' ? invoker : null,
        getElementById: () => dialog,
        addEventListener(type, handler) { if (type === 'click') this.clickHandlers.push(handler); }
    };
    runSite(document, () => Promise.reject(new Error('No fetch expected')));
    let prevented = false;
    document.clickHandlers[0]({ target: { closest: selector => selector === '[data-dialog-open]' ? trigger : null }, preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    assert.equal(dialog.open, true);
    dialog.listeners.close[0]();
    assert.equal(invoker.focused, true);
});

test('dialog links navigate normally when native dialog support is absent', () => {
    const body = createElement('body');
    const dialog = createElement('dialog');
    delete dialog.showModal;
    const trigger = { tagName: 'A', dataset: { dialogOpen: 'event-cancel' } };
    const document = {
        body, clickHandlers: [], createElement,
        querySelector: () => null,
        getElementById: () => dialog,
        addEventListener(type, handler) { if (type === 'click') this.clickHandlers.push(handler); }
    };
    runSite(document, () => Promise.reject(new Error('No fetch expected')));
    let prevented = false;
    document.clickHandlers[0]({ target: { closest: selector => selector === '[data-dialog-open]' ? trigger : null }, preventDefault() { prevented = true; } });
    assert.equal(prevented, false);
});

test('a failed cover image is hidden and replaced with an unavailable message', () => {
    const body = createElement('body'), figure = createElement('figure'), message = createElement('p');
    const dialog = { querySelector: selector => selector === '[data-cover-error]' ? message : null };
    const image = { matches: selector => selector === '[data-cover-image]', closest: selector => selector === 'dialog' ? dialog : figure };
    const handlers = {};
    const document = {
        body, createElement, clickHandlers: [], querySelector: () => null, getElementById: () => null,
        addEventListener(type, handler) { (handlers[type] ||= []).push(handler); if (type === 'click') this.clickHandlers.push(handler); }
    };
    runSite(document, () => Promise.reject(new Error('No fetch expected')));
    handlers.error[0]({ target: image });
    assert.equal(figure.hidden, true);
    assert.equal(message.hidden, false);
});

test('a failed preview fetch shows an error notice and no dialog', async () => {
    const body = createElement('body');
    const main = createElement('main');
    main.id = 'main-content';

    let clickHandler;
    const document = {
        body,
        createElement,
        querySelector() {
            return null;
        },
        getElementById(id) {
            if (id === 'main-content') return main;
            if (id === 'deactivation-preview-error') {
                return main.children.find(child => child.id === id) || null;
            }
            return null;
        },
        addEventListener(type, handler) {
            if (type === 'click') clickHandler = handler;
        }
    };

    const $ = callback => callback();
    $.ajaxSetup = () => {};

    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'site.js'), 'utf8');
    vm.runInNewContext(source, {
        $,
        window: {},
        document,
        fetch: () => Promise.resolve({ ok: false, text: () => Promise.resolve('') }),
        console
    });

    assert.equal(typeof clickHandler, 'function');

    const trigger = createElement('button');
    trigger.dataset.deactivationPreview = '/DeactivationPreview/1';
    let defaultPrevented = false;

    clickHandler({
        target: { closest: () => trigger },
        preventDefault() {
            defaultPrevented = true;
        }
    });

    await new Promise(resolve => setImmediate(resolve));

    assert.equal(defaultPrevented, true);
    assert.equal(trigger.focused, undefined);
    assert.equal(main.children.length, 1);

    const notice = main.children[0];
    assert.equal(notice.id, 'deactivation-preview-error');
    assert.equal(notice.className, 'notice notice-error');
    assert.equal(notice.attributes.role, 'alert');
    assert.match(notice.textContent, /Unable to load the deactivation preview/);
});

test('a fetched dialog returns focus to its trigger with preventScroll when it closes', async () => {
    const body = createElement('body');
    const { host, dialog } = createDialogHost();
    const trigger = createElement('button');
    trigger.dataset.deactivationPreview = '/DeactivationPreview/1';

    const document = {
        body,
        createElement: tagName => (tagName === 'div' ? host : createElement(tagName)),
        querySelector() { return null; },
        getElementById() { return null; },
        addEventListener(type, handler) {
            if (type === 'click') (document.clickHandlers = document.clickHandlers || []).push(handler);
        }
    };

    const clickHandler = runSite(document, () => Promise.resolve({ ok: true, text: () => Promise.resolve('<dialog></dialog>') }));
    clickHandler(clickEvent(trigger));
    await flush();

    assert.equal(dialog.open, true);
    assert.equal(trigger.focused, undefined);

    dialog.listeners.close[0]();

    assert.equal(trigger.focused, true);
    assert.equal(trigger.focusOptions?.preventScroll, true);
    assert.equal(host.children.length, 0);
});

test('a fetched dialog returns focus to the visible popover invoker, not the hidden trigger', async () => {
    const body = createElement('body');
    const { host, dialog } = createDialogHost();
    const popover = createElement('div');
    popover.id = 'row-menu-account-7';
    const trigger = createElement('button');
    trigger.dataset.deactivationPreview = '/DeactivationPreview/7';
    trigger.closest = selector => (selector === '[popover]' ? popover : null);
    const invoker = createElement('button');

    const document = {
        body,
        createElement: tagName => (tagName === 'div' ? host : createElement(tagName)),
        querySelector(selector) {
            return selector === '[popovertarget="row-menu-account-7"]' ? invoker : null;
        },
        getElementById() { return null; },
        addEventListener(type, handler) {
            if (type === 'click') (document.clickHandlers = document.clickHandlers || []).push(handler);
        }
    };

    const clickHandler = runSite(document, () => Promise.resolve({ ok: true, text: () => Promise.resolve('<dialog></dialog>') }));
    clickHandler(clickEvent(trigger));
    await flush();

    dialog.listeners.close[0]();

    assert.equal(invoker.focused, true);
    assert.equal(invoker.focusOptions?.preventScroll, true);
    assert.equal(trigger.focused, undefined);
});

test('a superseded fetch opens no dialog, shows no error and moves no focus', async () => {
    const body = createElement('body');
    const { host, dialog } = createDialogHost();
    const first = createElement('button');
    first.dataset.deactivationPreview = '/DeactivationPreview/1';
    const second = createElement('button');
    second.dataset.deactivationPreview = '/DeactivationPreview/2';

    const pending = [];
    const fetch = () => new Promise(resolve => pending.push(resolve));

    const document = {
        body,
        createElement: tagName => (tagName === 'div' ? host : createElement(tagName)),
        querySelector() { return null; },
        getElementById() { return null; },
        addEventListener(type, handler) {
            if (type === 'click') (document.clickHandlers = document.clickHandlers || []).push(handler);
        }
    };

    const clickHandler = runSite(document, fetch);
    clickHandler(clickEvent(first));
    clickHandler(clickEvent(second));

    // The first request fails after the second one has already replaced it: no notice, no dialog.
    pending[0]({ ok: false, text: () => Promise.resolve('') });
    await flush();

    assert.equal(host.children.length, 0);
    assert.equal(body.children.length, 1, 'only the dialog host, no error notice');
    assert.equal(dialog.open, undefined);

    pending[1]({ ok: true, text: () => Promise.resolve('<dialog></dialog>') });
    await flush();

    assert.equal(host.children.length, 1);
    assert.equal(dialog.open, true);
});

test('a scan posts the layout meta token as the antiforgery header', async () => {
    const form = {
        action: '/CheckIn/Scan?eventId=12',
        elements: { eventId: { value: '12' } },
        addEventListener() {}
    };
    const root = {
        querySelector(selector) {
            return selector === '[data-scan-form]' ? form : null;
        },
        querySelectorAll() {
            return [];
        }
    };
    const meta = { getAttribute: () => 'meta-token' };
    const document = {
        querySelector(selector) {
            if (selector === '[data-scanner]') return root;
            if (selector === 'meta[name="RequestVerificationToken"]') return meta;
            return null;
        },
        createElement,
        dispatchEvent() {}
    };

    let call;
    const window = {};
    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'features', 'scanner.js'), 'utf8');
    vm.runInNewContext(source, {
        window,
        document,
        fetch(url, options) {
            call = { url, options };
            return Promise.resolve({ json: () => Promise.resolve({ outcome: 'invalid', message: 'No.' }) });
        },
        URLSearchParams,
        Intl,
        Date,
        console
    });

    await window.AttendTaScanner.postScan(form, 'abc123');

    // The adapter must post to the form's own action, never a constructed or hard-coded origin.
    assert.equal(call.url, form.action);
    assert.doesNotMatch(call.url, /^https?:/);
    assert.equal(call.options.method, 'POST');
    assert.equal(call.options.headers.RequestVerificationToken, 'meta-token');
    assert.equal(call.options.body, 'eventId=12&token=abc123');
    assert.doesNotMatch(call.options.body, /RequestVerificationToken/);
});

test('scan outcome and log show Manila-local time without a timezone suffix', async () => {
    const result = createElement('div');
    const log = createElement('tbody');
    const root = {
        querySelector: selector => selector === '[data-scan-result]' ? result : null,
        querySelectorAll: () => []
    };
    const document = {
        querySelector(selector) {
            if (selector === '[data-scanner]') return root;
            if (selector === '[data-scan-log]') return log;
            return null;
        },
        createElement,
        dispatchEvent() {}
    };
    const window = {};
    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'features', 'scanner.js'), 'utf8');
    vm.runInNewContext(source, {
        window, document, URLSearchParams, Intl, Date, console,
        fetch: () => Promise.resolve({ json: () => Promise.resolve({
            outcome: 'success', registrationId: 42, memberName: 'Test Member',
            studentNumber: '12345678', recordedAt: '2026-01-01T18:30:00Z'
        }) })
    });

    await window.AttendTaScanner.postScan({ action: '/CheckIn/Scan', elements: { eventId: { value: '12' } } }, 'test-token');

    assert.equal(result.children[0].children[3].textContent, '2026-01-02 02:30');
    assert.equal(log.children[0].children[2].textContent, '2026-01-02 02:30');
});

test('live success and repeat preserve immediate invalidation actions and existing rows', async () => {
    const log = createElement('tbody');
    const emptyRow = createElement('tr'), emptyCell = createElement('td');
    emptyCell.className = 'empty-state';
    emptyRow.appendChild(emptyCell);
    emptyCell.parentElement = emptyRow;
    log.appendChild(emptyRow);
    log.querySelector = selector => {
        if (selector === 'td.empty-state') return log.children.includes(emptyRow) ? emptyCell : null;
        const match = selector.match(/^tr\[data-registration-id="(\d+)"\]$/);
        return match ? log.children.find(row => row.attributes['data-registration-id'] === match[1]) : null;
    };
    const root = { querySelector: () => null, querySelectorAll: () => [] };
    const document = { querySelector: selector => selector === '[data-scanner]' ? root : selector === '[data-scan-log]' ? log : null, createElement, dispatchEvent() {} };
    const outcomes = [
        { outcome: 'success', registrationId: 42, memberName: 'Alex', studentNumber: '123', recordedAt: '2026-01-01T18:30:00Z' },
        { outcome: 'repeat', registrationId: 42, memberName: 'Alex', studentNumber: '123', recordedAt: '2026-01-01T18:30:00Z' },
        { outcome: 'repeat', registrationId: 43, memberName: 'Sam', studentNumber: '456', recordedAt: '2026-01-01T18:30:00Z', canInvalidate: false }
    ];
    const window = {};
    const source = fs.readFileSync(path.join(__dirname, '../wwwroot/js/features/scanner.js'), 'utf8');
    vm.runInNewContext(source, { window, document, URLSearchParams, Intl, Date, fetch: () => Promise.resolve({ json: () => Promise.resolve(outcomes.shift()) }) });
    const form = { action: '/CheckIn/Scan', elements: { eventId: { value: '12' } } };
    await window.AttendTaScanner.postScan(form, 'one');
    assert.equal(log.children.length, 1);
    const first = log.children[0];
    assert.equal(first.attributes['data-registration-id'], '42');
    assert.deepEqual(first.children.map(cell => cell.attributes['data-label']), ['Participant', 'Student number', 'Recorded', 'Outcome', 'Reason', 'Action']);
    assert.equal(first.children[0].textContent, 'Alex');
    assert.equal(first.children[4].textContent, '—');
    const button = first.children[5].children[0].children[0];
    assert.equal(first.children[5].children[0].className, 'row-actions');
    assert.equal(button.className, 'button button-quiet-danger');
    assert.equal(button.attributes['data-dialog-open'], 'invalidate-dialog');
    assert.equal(button.attributes['data-dialog-value-id'], '42');
    assert.equal(button.attributes['data-dialog-value-summary'], 'Invalidate the check-in for Alex (123)?');
    await window.AttendTaScanner.postScan(form, 'repeat');
    assert.equal(log.children.length, 1);
    assert.equal(log.children[0], first);
    await window.AttendTaScanner.postScan(form, 'other-repeat');
    assert.equal(log.children.length, 2);
    assert.equal(log.children[0].attributes['data-registration-id'], '43');
    assert.equal(log.children[0].children[5].children[0].children[0].attributes['data-dialog-value-id'], '43');
    assert.equal(log.children[1], first);
});

test('invalidation dialog receives the clicked participant and registration ID', () => {
    class HTMLInputElement { constructor() { this.value = ''; } }
    class HTMLTextAreaElement {}
    const id = new HTMLInputElement(), summary = createElement('p'), dialog = createElement('dialog');
    dialog.querySelector = selector => ({ '[data-dialog-field="id"]': id, '[data-dialog-field="summary"]': summary })[selector] || null;
    const trigger = createElement('button');
    trigger.dataset = { dialogOpen: 'invalidate-dialog', dialogValueId: '73', dialogValueSummary: 'Invalidate the check-in for Sam (456)?' };
    const document = {
        body: createElement('body'), clickHandlers: [], createElement,
        querySelector: () => null, getElementById: value => value === 'invalidate-dialog' ? dialog : null,
        addEventListener(type, handler) { if (type === 'click') this.clickHandlers.push(handler); }
    };
    const $ = callback => callback();
    $.ajaxSetup = () => {};
    const source = fs.readFileSync(path.join(__dirname, '../wwwroot/js/site.js'), 'utf8');
    vm.runInNewContext(source, { $, document, window: {}, HTMLInputElement, HTMLTextAreaElement, fetch() {} });
    document.clickHandlers[0]({ target: { closest: selector => selector === '[data-dialog-open]' ? trigger : null } });
    assert.equal(dialog.open, true);
    assert.equal(id.value, '73');
    assert.equal(summary.textContent, 'Invalidate the check-in for Sam (456)?');
});

test('scanner keyboard navigation wraps between camera and manual lookup', () => {
    const camera = createElement('button'), manual = createElement('button');
    const panels = { camera: {}, manual: {} };
    for (const [element, name] of [[camera, 'camera'], [manual, 'manual']]) {
        element.attributes = { 'data-scanner-mode': name, 'aria-controls': name, 'aria-selected': name === 'camera' ? 'true' : 'false' };
        element.getAttribute = key => element.attributes[key];
    }
    const events = [];
    const root = { querySelector: () => null, querySelectorAll: () => [camera, manual] };
    const document = { querySelector: () => root, getElementById: id => panels[id], dispatchEvent: event => events.push(event.type) };
    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'features', 'scanner.js'), 'utf8');
    vm.runInNewContext(source, { document, window: {}, CustomEvent: function (type) { this.type = type; } });
    let prevented = false;
    camera.listeners.keydown[0]({ key: 'ArrowLeft', preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    assert.equal(manual.attributes['aria-selected'], 'true');
    assert.equal(manual.focused, true);
    assert.equal(panels.camera.hidden, true);
    assert.equal(panels.manual.hidden, false);
    assert.deepEqual(events, ['scanner:camera-stop']);
    manual.listeners.keydown[0]({ key: 'ArrowRight', preventDefault() {} });
    assert.equal(camera.attributes['aria-selected'], 'true');
    assert.equal(panels.camera.hidden, false);
});

test('camera posts decoded QR using the hidden scan form', async () => {
    const form = { action: '/CheckIn/Scan', elements: { eventId: { value: '12' } }, hidden: true };
    const start = createElement('button'), stop = createElement('button'), next = createElement('button');
    const video = { readyState: 4, HAVE_ENOUGH_DATA: 4, videoWidth: 1, videoHeight: 1, play() {} };
    const elements = { '[data-scan-form]': form, '[data-camera-start]': start, '[data-camera-stop]': stop, '[data-scan-next]': next, '[data-scanner-video]': video };
    const root = { querySelector: selector => elements[selector] || null };
    const canvas = { getContext: () => ({ drawImage() {}, getImageData: () => ({ data: [], width: 1, height: 1 }) }) };
    const document = { querySelector: () => root, createElement: () => canvas, addEventListener() {} };
    let frame, posted, stopped = false;
    const window = { jsQR: () => ({ data: 'synthetic-token' }), addEventListener() {}, AttendTaScanner: { postScan: (actualForm, token) => { posted = { actualForm, token }; return Promise.resolve(); } } };
    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'features', 'scanner-camera.js'), 'utf8');
    vm.runInNewContext(source, { window, document, navigator: { mediaDevices: { getUserMedia: () => Promise.resolve({ getTracks: () => [{ stop() { stopped = true; } }] }) } }, requestAnimationFrame: callback => { frame = callback; return 1; }, cancelAnimationFrame() {} });
    start.listeners.click[0]();
    await flush();
    frame();
    await flush();
    assert.equal(posted.actualForm, form);
    assert.equal(posted.token, 'synthetic-token');
    assert.equal(stopped, true);
    assert.equal(next.hidden, false);
});

test('unavailable camera directs the officer to manual lookup', () => {
    const start = createElement('button'), note = createElement('p');
    const elements = { '[data-scan-form]': {}, '[data-camera-start]': start, '[data-camera-note]': note };
    const root = { querySelector: selector => elements[selector] || null };
    let outcome;
    const window = { jsQR() {}, addEventListener() {}, AttendTaScanner: { render: value => { outcome = value; } } };
    const document = { querySelector: () => root, createElement: () => ({ getContext: () => null }), addEventListener() {} };
    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'features', 'scanner-camera.js'), 'utf8');
    vm.runInNewContext(source, { window, document, navigator: {}, cancelAnimationFrame() {} });
    start.listeners.click[0]();
    assert.equal(outcome.outcome, 'camera-unavailable');
    assert.match(note.textContent, /manual lookup/i);
    assert.doesNotMatch(note.textContent, /pass code/i);
});
