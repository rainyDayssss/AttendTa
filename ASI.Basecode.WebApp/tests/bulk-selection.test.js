const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

const source = fs.readFileSync(path.join(__dirname, '../wwwroot/js/features/bulk-selection.js'), 'utf8');

function element(tagName) {
    return {
        tagName,
        dataset: {},
        children: [],
        listeners: {},
        textContent: '',
        type: '',
        name: '',
        value: '',
        checked: false,
        disabled: false,
        indeterminate: false,
        setAttribute() {},
        focus() { this.focused = true; },
        addEventListener(name, handler) { this.listeners[name] = handler; },
        replaceChildren(...nodes) { this.children = nodes; nodes.forEach(node => { node.parent = this; }); },
        matches(selector) { return selector === '[data-bulk-row]' && this.isRow === true; }
    };
}

function setup({ rows = 3 } = {}) {
    const count = element('p');
    const toggleAll = element('input');
    const openButton = element('button');
    const clearButton = element('button');
    const selectAllButton = element('button');
    const ids = element('div');
    const summary = element('p');
    const list = element('ul');
    const heading = element('h2');
    const dialog = Object.assign(element('dialog'), { showModal() { this.open = true; } });

    const rowElements = Array.from({ length: rows }, (_, index) => {
        const row = element('input');
        row.isRow = true;
        row.value = String(index + 7);
        row.dataset.bulkLabel = 'Person ' + (index + 7);
        return row;
    });

    const selectors = new Map([
        ['[data-bulk-count]', count],
        ['[data-bulk-all]', toggleAll],
        ['[data-bulk-open]', openButton],
        ['[data-bulk-clear]', clearButton],
        ['[data-bulk-select-all]', selectAllButton],
        ['[data-bulk-dialog]', dialog]
    ]);

    const root = Object.assign(element('div'), {
        dataset: { bulkNoun: 'request' },
        querySelector: selector => selectors.get(selector) || null,
        querySelectorAll: selector => selector === '[data-bulk-row]' ? rowElements : []
    });

    dialog.querySelector = selector => ({
        '[data-bulk-dialog-ids]': ids,
        '[data-bulk-dialog-summary]': summary,
        '[data-bulk-dialog-list]': list,
        'h2': heading
    })[selector] || null;

    const pageHandlers = {};
    const context = {
        window: { addEventListener: (name, handler) => { pageHandlers[name] = handler; } },
        document: {
            readyState: 'loading',
            addEventListener() {},
            createElement: element,
            querySelectorAll: selector => selector === '[data-bulk-root]' ? [root] : []
        }
    };

    vm.runInNewContext(source, context);
    const api = context.window.AttendTaBulkSelection.initialize()[0];
    return { api, root, rows: rowElements, count, toggleAll, openButton, clearButton, selectAllButton, dialog, ids, summary, list, heading, pageHandlers };
}

test('selection updates the count, the header state and the action', () => {
    const { api, rows, count, toggleAll, openButton } = setup();

    assert.equal(count.textContent, '0 selected');
    assert.equal(openButton.disabled, true);
    assert.equal(toggleAll.disabled, false);

    rows[0].checked = true;
    api.sync();
    assert.equal(count.textContent, '1 selected');
    assert.equal(openButton.disabled, false);
    assert.equal(toggleAll.indeterminate, true);
    assert.equal(toggleAll.checked, false);

    rows[1].checked = true;
    rows[2].checked = true;
    api.sync();
    assert.equal(count.textContent, '3 selected');
    assert.equal(toggleAll.checked, true);
    assert.equal(toggleAll.indeterminate, false);
});

test('the header checkbox drives every row', () => {
    const { root, rows, toggleAll, count } = setup();

    toggleAll.checked = true;
    root.listeners.change({ target: toggleAll });
    assert.ok(rows.every(row => row.checked));
    assert.equal(count.textContent, '3 selected');

    toggleAll.checked = false;
    root.listeners.change({ target: toggleAll });
    assert.ok(rows.every(row => !row.checked));
    assert.equal(count.textContent, '0 selected');
});

test('select all on this page ticks every eligible row and deselect all clears them', () => {
    const { rows, count, selectAllButton, clearButton } = setup();

    selectAllButton.listeners.click();
    assert.ok(rows.every(row => row.checked));
    assert.equal(count.textContent, '3 selected');

    clearButton.listeners.click();
    assert.ok(rows.every(row => !row.checked));
    assert.equal(count.textContent, '0 selected');
});

test('opening the dialog writes one hidden id per selected row and names each person', () => {
    const { api, rows, dialog, ids, summary, list } = setup();

    rows[0].checked = true;
    rows[2].checked = true;
    assert.equal(api.open(), true);
    assert.equal(dialog.open, true);
    assert.deepEqual(ids.children.map(input => [input.type, input.name, input.value]), [
        ['hidden', 'ids', '7'],
        ['hidden', 'ids', '9']
    ]);
    assert.equal(summary.textContent, '2 requests selected.');
    assert.deepEqual(list.children.map(item => item.textContent), ['Person 7', 'Person 9']);
});

test('opening the action button opens the dialog with the current selection', () => {
    const { rows, openButton, dialog, ids } = setup();

    rows[1].checked = true;
    openButton.listeners.click();
    assert.equal(dialog.open, true);
    assert.deepEqual(ids.children.map(input => input.value), ['8']);
});

test('opening the dialog moves focus to the heading, not the close button', () => {
    const { api, rows, heading, dialog } = setup();

    rows[0].checked = true;
    assert.equal(api.open(), true);
    assert.equal(dialog.open, true);
    assert.equal(heading.focused, true);
});

test('the dialog refuses to open with nothing selected', () => {
    const { api, dialog, ids, summary } = setup();

    assert.equal(api.open(), false);
    assert.notEqual(dialog.open, true);
    assert.equal(ids.children.length, 0);
    assert.equal(summary.textContent, '');
});

test('a bfcache restore clears the selection', () => {
    const { api, rows, count, pageHandlers } = setup();

    rows[0].checked = true;
    api.sync();
    assert.equal(count.textContent, '1 selected');
    pageHandlers.pageshow({ persisted: true });
    assert.ok(rows.every(row => !row.checked));
    assert.equal(count.textContent, '0 selected');
});

test('select all and deselect all are disabled when the page has no selectable rows', () => {
    const { selectAllButton, clearButton, toggleAll, openButton } = setup({ rows: 0 });

    assert.equal(selectAllButton.disabled, true);
    assert.equal(clearButton.disabled, true);
    assert.equal(toggleAll.disabled, true);
    assert.equal(openButton.disabled, true);
});

test('the page actions follow the current selection', () => {
    const { api, rows, selectAllButton, clearButton } = setup();

    // Nothing ticked: selecting all is the only action that would change anything.
    assert.equal(selectAllButton.disabled, false);
    assert.equal(clearButton.disabled, true);

    rows[0].checked = true;
    api.sync();
    assert.equal(selectAllButton.disabled, false);
    assert.equal(clearButton.disabled, false);

    // Every row ticked: there is nothing left to select.
    rows.forEach(row => { row.checked = true; });
    api.sync();
    assert.equal(selectAllButton.disabled, true);
    assert.equal(clearButton.disabled, false);

    rows.forEach(row => { row.checked = false; });
    api.sync();
    assert.equal(selectAllButton.disabled, false);
    assert.equal(clearButton.disabled, true);
});

test('selection is never persisted', () => {
    assert.doesNotMatch(source, /sessionStorage|localStorage/);
});
