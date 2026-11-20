const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../wwwroot/js/features/record-dialog.js'), 'utf8');

function setup(inDialog) {
    const handlers = {}, revoked = [], created = [];
    const host = {
        dataset: { recordPreviewAlt: 'Preview of selected venue photo' }, hidden: true, children: [],
        replaceChildren(...children) { this.children = children; },
        append(child) { this.children.push(child); }
    };
    const savedPhoto = { label: 'Current photo' };
    const input = {
        files: [], disabled: false,
        closest(selector) {
            if (selector === 'input[type="file"]') return this;
            if (selector === '#record-dialog') return inDialog ? dialog : null;
            if (selector === 'form') return { querySelector: () => host };
            return null;
        }
    };
    const message = { textContent: '', focus() {} };
    const recovery = { hidden: true };
    const body = {
        listeners: {},
        addEventListener(type, handler) { this.listeners[type] = handler; },
        querySelector(selector) { return selector === '[data-record-preview]' ? host : null; },
        querySelectorAll(selector) { return selector === '[data-record-preview]' ? [host] : []; },
        replaceChildren() {}
    };
    const dialog = {
        listeners: {}, open: true, showModal() {}, setAttribute() {},
        querySelector(selector) { return ({ '[data-record-body]': body, '[data-record-message]': message, '[data-record-recovery]': recovery })[selector] || null; },
        querySelectorAll() { return []; },
        addEventListener(type, handler) { this.listeners[type] = handler; }
    };
    const document = {
        addEventListener(type, handler) { (handlers[type] ||= []).push(handler); },
        querySelector(selector) { return selector === '#record-dialog' && inDialog ? dialog : null; },
        createElement() { return {}; }
    };
    const URL = {
        createObjectURL(file) { const url = `blob:${file.name}`; created.push(url); return url; },
        revokeObjectURL(url) { revoked.push(url); }
    };
    vm.runInNewContext(source, {
        document, URL, window: {}, sessionStorage: { getItem: () => null },
        fetch: () => Promise.reject(new Error('offline')),
        FormData: class {}, AbortController, Intl, Date, location: {}
    });
    function change(file) {
        input.files = file ? [file] : [];
        const event = { target: input };
        handlers.change[0](event);
        if (inDialog) body.listeners.change(event);
    }
    return { input, host, savedPhoto, revoked, created, change, dialog, message };
}

for (const inDialog of [false, true]) {
    test(`${inDialog ? 'dialog' : 'standalone'} venue preview selects, replaces and clears without changing current photo`, () => {
        const s = setup(inDialog);
        s.change({ name: 'first.jpg', type: 'image/jpeg' });
        assert.equal(s.host.hidden, false);
        assert.equal(s.host.children[0].src, 'blob:first.jpg');
        assert.equal(s.host.children[0].alt, 'Preview of selected venue photo');
        s.change({ name: 'second.png', type: 'image/png' });
        assert.equal(s.host.children[0].src, 'blob:second.png');
        assert.deepEqual(s.revoked, ['blob:first.jpg']);
        s.change(null);
        assert.equal(s.host.hidden, true);
        assert.equal(s.host.children.length, 0);
        assert.deepEqual(s.revoked, ['blob:first.jpg', 'blob:second.png']);
        assert.equal(s.savedPhoto.label, 'Current photo');
    });
}

test('dialog connection failure retains selected file and preview without claiming reselection', async () => {
    const s = setup(true);
    const file = { name: 'venue.jpg', type: 'image/jpeg' };
    s.change(file);
    const form = { action: '/Venue/Create', closest: () => form };
    await s.dialog.listeners.submit({ target: form, preventDefault() {} });
    assert.equal(s.input.files[0], file);
    assert.equal(s.host.children[0].src, 'blob:venue.jpg');
    assert.equal(s.host.hidden, false);
    assert.match(s.message.textContent, /input is retained/i);
    assert.match(s.message.textContent, /duplicate save/i);
    assert.doesNotMatch(s.message.textContent, /choose.*again/i);
    assert.deepEqual(s.revoked, []);
    s.dialog.listeners.close();
    assert.deepEqual(s.revoked, ['blob:venue.jpg']);
});
