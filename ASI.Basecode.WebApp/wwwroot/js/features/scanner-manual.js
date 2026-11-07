// Door scanner manual adapter (register M-06). The lookup is server-rendered so it works without
// JavaScript; this adapter keeps the interaction explicit and predictable: selecting or viewing a
// row never records attendance, only the Check in button posts, and a submitted row cannot post
// twice. It also focuses the lookup field when the manual mode is selected.
(function () {
    'use strict';

    var root = document.querySelector('[data-scanner]');
    if (!root) return;

    var lookupForm = root.querySelector('[data-lookup-form]');
    var lookupInput = root.querySelector('#lookup-query');

    if (lookupInput) {
        var manualMode = root.querySelector('[data-scanner-mode="manual"]');
        if (manualMode) {
            manualMode.addEventListener('click', function () {
                lookupInput.focus();
            });
        }
    }

    if (lookupForm) {
        lookupForm.addEventListener('submit', function (event) {
            if (lookupInput && !lookupInput.value.trim()) {
                event.preventDefault();
                lookupInput.focus();
                return;
            }

            var button = lookupForm.querySelector('button[type="submit"]');
            if (button) {
                button.disabled = true;
                button.textContent = button.getAttribute('data-submitting-label') || 'Searching…';
            }
        });
    }

    root.querySelectorAll('[data-check-in-form]').forEach(function (form) {
        form.addEventListener('submit', function () {
            form.querySelectorAll('button').forEach(function (button) {
                button.disabled = true;
            });
        });
    });
}());
