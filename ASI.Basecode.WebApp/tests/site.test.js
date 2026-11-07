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
        addEventListener() {},
        querySelector() {
            return null;
        },
        focus() {
            element.focused = true;
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
