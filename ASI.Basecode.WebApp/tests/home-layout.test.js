const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const read = file => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');

test('management groups share a responsive wrapper without changing the member group', () => {
    const view = read('Views/Home/Index.cshtml').replace(/\r\n/g, '\n');
    const management = view.slice(view.indexOf('@if (management)\n{'), view.indexOf('else if (member)'));
    const member = view.slice(view.indexOf('else if (member)'));
    assert.match(management, /home-management-groups[\s\S]*id="home-manage"[\s\S]*id="home-operations"/);
    assert.doesNotMatch(member, /home-management-groups/);
    assert.match(member, /id="home-member"/);
    const css = read('Styles/features/home.css');
    assert.match(css, /\.home-management-groups\s*\{\s*display:\s*grid;\s*gap:\s*24px/);
    assert.match(css, /@media \(min-width:\s*1024px\)[\s\S]*?grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\)/);
    const built = read('wwwroot/css/app.css');
    assert.match(built, /\.home-management-groups\{[^}]*display:grid/);
    assert.match(built, /\.home-management-groups\{grid-template-columns:repeat\(2,minmax\(0,1fr\)\)/);
});
