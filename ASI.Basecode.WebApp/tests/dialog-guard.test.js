const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

function setup({ reopen = false, missingInvoker = false } = {}) {
    const handlers = {};
    function element(tagName) {
        return {
            tagName, dataset: {}, children: [], listeners: {},
            setAttribute() {},
            append(...children) { children.forEach(child => { child.parent = this; this.children.push(child); }); },
            appendChild(child) { this.append(child); },
            addEventListener(name, handler) { this.listeners[name] = handler; },
            focus() { this.focused = true; },
            remove() { if (this.parent) this.parent.children = this.parent.children.filter(child => child !== this); },
            querySelector(selector) {
                for (const child of this.children) {
                    if ((selector === 'button' && child.tagName === 'button') || (selector === '[data-discard-prompt]' && child.dataset.discardPrompt !== undefined)) return child;
                    const nested = child.querySelector(selector);
                    if (nested) return nested;
                }
                return null;
            }
        };
    }
    class Dialog {}
    const $ = callback => callback();
    $.ajaxSetup = () => {};
    const context = { $, window: {}, HTMLDialogElement: Dialog, document: {
        body: { appendChild() {} },
        createElement: element,
        querySelector: () => null,
        getElementById: () => null,
        addEventListener: (name, callback) => { handlers[name] = callback; }
    }};
    const fields = [{ name: 'Title', type: 'text', value: 'Draft', disabled: false }];
    const dialog = Object.assign(new Dialog(), element('dialog'), {
        querySelectorAll: () => fields,
        getAttribute: () => null,
        close() { this.closed = true; }
    });
    const invoker = { focus(options) { this.focused = true; this.options = options; } };
    const list = { focus(options) { this.focused = true; this.options = options; } };
    if (reopen) {
        const reason = { name: 'reason', type: 'textarea', value: 'Previously entered reason', focus() {} };
        fields.push(reason);
        dialog.dataset.reopenId = '7';
        dialog.dataset.reopenReason = reason.value;
        dialog.dataset.returnFocus = 'row-menu-event-7';
        const query = dialog.querySelector.bind(dialog);
        dialog.querySelector = selector => selector.startsWith('textarea') ? reason : selector === '[data-dialog-field="id"]' ? {} : query(selector);
        dialog.showModal = () => { dialog.open = true; };
        context.document.querySelector = selector => selector === 'dialog[data-reopen="true"]' ? dialog : selector === '[popovertarget="row-menu-event-7"]' ? (missingInvoker ? null : invoker) : selector === '[data-list-region]' ? list : null;
    }
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/site.js'), 'utf8'), context);
    return { api: context.window.AttendTaDialogs, fields, dialog, handlers, invoker, list };
}

test('dialog guard recognizes edits and undoing edits', () => {
    const { api, dialog, fields } = setup();
    api.track(dialog);
    assert.equal(api.hasChanges(dialog), false);
    fields[0].value = 'Changed';
    assert.equal(api.hasChanges(dialog), true);
    fields[0].value = 'Draft';
    assert.equal(api.hasChanges(dialog), false);
});

test('server-loaded editor establishes a new baseline only when explicitly reset', () => {
    const { api, dialog, fields } = setup();
    api.track(dialog);
    fields[0].value = 'Loaded event';
    api.track(dialog);
    assert.equal(api.hasChanges(dialog), true);
    api.track(dialog, true);
    assert.equal(api.hasChanges(dialog), false);
});

test('unchanged dialog dismisses immediately but a busy save cannot close', () => {
    const { api, dialog } = setup();
    api.track(dialog);
    assert.equal(api.requestClose(dialog), true);
    dialog.closed = false;
    dialog.getAttribute = () => 'true';
    assert.equal(api.requestClose(dialog), false);
    assert.equal(dialog.closed, false);
});

test('checkboxes and selected files participate in change detection', () => {
    const { api, dialog, fields } = setup();
    fields.push({ name: 'Consent', type: 'checkbox', checked: false });
    fields.push({ name: 'Image', type: 'file', files: [] });
    api.track(dialog);
    fields[1].checked = true;
    assert.equal(api.hasChanges(dialog), true);
    fields[1].checked = false;
    fields[2].files = [{ name: 'cover.png', size: 42, lastModified: 1 }];
    assert.equal(api.hasChanges(dialog), true);
});

test('dismissing an edited dialog offers keep editing before it can discard input', () => {
    const { api, dialog, fields } = setup();
    api.track(dialog);
    fields[0].value = 'Unsaved title';
    assert.equal(api.requestClose(dialog), false);
    assert.notEqual(dialog.closed, true);
    const prompt = dialog.querySelector('[data-discard-prompt]');
    assert.ok(prompt);
    const keep = prompt.querySelector('button');
    assert.equal(keep.textContent, 'Keep editing');
    assert.equal(keep.focused, true);
    keep.listeners.click();
    assert.equal(dialog.querySelector('[data-discard-prompt]'), null);
    assert.equal(fields[0].value, 'Unsaved title');
    assert.notEqual(dialog.closed, true);
});

test('Escape uses the same discard choice and only explicit discard closes edited input', () => {
    const { api, dialog, fields, handlers } = setup();
    api.track(dialog);
    fields[0].value = 'Unsaved title';
    let prevented = false;
    handlers.cancel({ target: dialog, preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    assert.notEqual(dialog.closed, true);
    const prompt = dialog.querySelector('[data-discard-prompt]');
    const discard = prompt.children[1].children[1];
    assert.equal(discard.textContent, 'Discard changes');
    discard.listeners.click();
    assert.equal(dialog.closed, true);
});

test('closing a dialog clears its previous baseline for the next record', () => {
    const { api, dialog, fields, handlers } = setup();
    api.track(dialog);
    fields[0].value = 'Different record';
    handlers.close({ target: dialog });
    api.track(dialog);
    assert.equal(api.hasChanges(dialog), false);
});

test('server-restored failed reason remains protected even when already rendered in the textarea', () => {
    const { api, dialog, fields } = setup({ reopen: true });
    assert.equal(dialog.open, true);
    assert.equal(fields[1].value, 'Previously entered reason');
    assert.equal(api.hasChanges(dialog), true);
    assert.equal(api.requestClose(dialog), false);
    assert.ok(dialog.querySelector('[data-discard-prompt]'));
});

test('a reopened list cancellation returns focus to the visible row menu button', () => {
    const { dialog, invoker } = setup({ reopen: true });
    assert.equal(dialog.open, true);
    dialog.listeners.close();
    assert.equal(invoker.focused, true);
    assert.equal(invoker.options.preventScroll, true);
});

test('a reopened cancellation returns focus to the list if its row is absent', () => {
    const { dialog, invoker, list } = setup({ reopen: true, missingInvoker: true });
    dialog.listeners.close();
    assert.equal(invoker.focused, undefined);
    assert.equal(list.focused, true);
    assert.equal(list.options.preventScroll, true);
});
