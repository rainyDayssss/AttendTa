(function () {
    'use strict';
    document.addEventListener('click', function (event) {
        const trigger = event.target.closest('[data-certificate-open]');
        if (trigger) {
            const dialog = document.getElementById(trigger.dataset.certificateOpen);
            if (!dialog) return;
            dialog.querySelectorAll('[data-certificate-field]').forEach(function (field) {
                const value = trigger.dataset[field.dataset.certificateField] || '';
                if (field instanceof HTMLInputElement) field.value = value;
                else field.textContent = value;
            });
            dialog.querySelectorAll('textarea').forEach(function (field) { field.value = ''; });
            dialog.addEventListener('close', function () { trigger.focus(); }, { once: true });
            window.AttendTaDialogs.track(dialog, true);
            dialog.showModal();
            (dialog.querySelector('textarea') || dialog.querySelector('[data-certificate-close]'))?.focus();
        }
        const close = event.target.closest('[data-certificate-close]');
        if (close) window.AttendTaDialogs?.requestClose(close.closest('dialog'));
    });
    document.querySelectorAll('.certificate-dialog form').forEach(function (form) {
        form.addEventListener('submit', function () {
            form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('[data-certificate-close]').forEach(function (button) { button.disabled = true; });
        });
    });
}());
