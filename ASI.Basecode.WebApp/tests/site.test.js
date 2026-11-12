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
    vm.runInNewContext(source, { $, document, fetch, console });

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
