const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

test('creation date minimum advances at Manila midnight without lowering the server minimum', () => {
    const listeners = {};
    const document = {
        addEventListener: (name, handler) => { listeners[name] = handler; },
        querySelector: () => null
    };
    const source = fs.readFileSync(path.join(__dirname, '../wwwroot/js/features/record-dialog.js'), 'utf8');
    class Clock extends Date { constructor() { super('2026-11-19T16:00:00Z'); } }
    vm.runInNewContext(source, { document, Date: Clock, Intl });
    const input = { min: '2026-11-19T00:00', closest: () => input };
    listeners.focusin({ target: input });
    assert.equal(input.min, '2026-11-20T00:00');
    input.min = '2026-11-21T00:00';
    listeners.focusin({ target: input });
    assert.equal(input.min, '2026-11-21T00:00');
});
