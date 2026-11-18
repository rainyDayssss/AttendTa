const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

test('password toggles are independent, preserve values, and never submit', () => {
    const inputs = { Password: { type: 'password', value: 'secret1' }, ConfirmPassword: { type: 'password', value: 'secret2' } };
    function button(id, label) {
        const attrs = { 'data-password-toggle': id, 'aria-label': label, 'aria-pressed': 'false' };
        return { hidden: true, getAttribute: key => attrs[key], setAttribute: (key, value) => { attrs[key] = value; }, attrs };
    }
    const buttons = [button('Password', 'Show password'), button('ConfirmPassword', 'Show confirmation password')];
    buttons.forEach(b => { b.addEventListener = (name, fn) => { b.click = fn; }; });
    const document = { querySelectorAll: () => buttons, getElementById: id => inputs[id] };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/features/password-visibility.js'), 'utf8'), { document });
    assert.equal(buttons[0].hidden, false);
    buttons[0].click();
    assert.equal(inputs.Password.type, 'text');
    assert.equal(inputs.ConfirmPassword.type, 'password');
    assert.equal(inputs.Password.value, 'secret1');
    assert.equal(buttons[0].attrs['aria-pressed'], 'true');
    assert.equal(buttons[0].attrs['aria-label'], 'Show password');
    assert.equal(buttons[1].attrs['aria-label'], 'Show confirmation password');
    buttons[0].click();
    assert.equal(inputs.Password.type, 'password');
    assert.equal(buttons[0].attrs['aria-label'], 'Show password');
    buttons[1].click();
    assert.equal(inputs.ConfirmPassword.type, 'text');
    assert.equal(inputs.ConfirmPassword.value, 'secret2');
    assert.equal(buttons[1].attrs['aria-label'], 'Show confirmation password');
    assert.equal(buttons[1].attrs['aria-pressed'], 'true');
});
