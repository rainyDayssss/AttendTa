const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const read = file => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');

test('affected tables select a content-sized action column without changing Members', () => {
    for (const file of ['Views/Event/Index.cshtml', 'Views/AdminAccounts/Index.cshtml']) {
        assert.match(read(file), /<th[^>]*class="actions actions-balanced"[^>]*>Actions<\/th>/);
    }
    for (const file of ['Views/AccountRequest/Index.cshtml', 'Views/CertificateLedger/Index.cshtml', 'Views/RegistrationReview/Index.cshtml']) {
        assert.match(read(file), /<th[^>]*class="[^"]*actions-decision[^"]*"[^>]*>Actions<\/th>/);
    }
    assert.doesNotMatch(read('Views/Member/Index.cshtml'), /actions-balanced|actions-decision/);
    const css = read('Styles/app.css');
    assert.match(css, /th\.actions-balanced\s*\{\s*width:\s*200px/);
    assert.match(css, /th\.actions-decision\s*\{\s*width:\s*232px/);
    assert.match(css, /@media \(min-width: 641px\) and \(max-width: 850px\)[\s\S]*?th\.actions-decision\s*\{\s*width:\s*30%/);
});

test('registration dates align with their headings and event type widths cover every table state', () => {
    const review = read('Views/RegistrationReview/Index.cshtml');
    assert.match(review, /data-label="Submission date" class="numeric cell-date"/);
    assert.match(review, /data-label="Submission time" class="numeric cell-date"/);
    const types = read('Views/EventType/Index.cshtml');
    assert.equal((types.match(/<table class="data-table table-sticky event-type-table">/g) || []).length, 3);
    const css = read('Styles/app.css');
    assert.match(css, /\.data-table \.numeric\.cell-date\s*\{\s*text-align:\s*left/);
    assert.match(css, /\.event-type-table thead th:first-child\s*\{\s*width:\s*30%/);
    assert.match(css, /\.venue-table :is\(th, td\)\.numeric\s*\{\s*padding-right:\s*56px/);
});
