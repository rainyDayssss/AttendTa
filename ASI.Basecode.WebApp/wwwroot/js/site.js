$(function () {
    const token = document.querySelector('meta[name="RequestVerificationToken"]')?.content;
    if (token) $.ajaxSetup({ headers: { RequestVerificationToken: token } });

    document.addEventListener('click', function (event) {
        const opener = event.target.closest('[data-dialog-open]');
        if (opener) {
            const dialog = document.getElementById(opener.dataset.dialogOpen);
            if (!dialog) return;

            for (const [key, value] of Object.entries(opener.dataset)) {
                if (!key.startsWith('dialogValue')) continue;
                const field = key.slice('dialogValue'.length);
                const name = field.charAt(0).toLowerCase() + field.slice(1);
                const target = dialog.querySelector('[data-dialog-field="' + name + '"]');
                if (!target) continue;
                if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
                    target.value = value;
                } else {
                    target.textContent = value;
                }
            }

            dialog.querySelectorAll('textarea').forEach(function (textarea) { textarea.value = ''; });
            dialog.showModal();
            dialog.querySelector('textarea, input:not([type="hidden"])')?.focus();
            return;
        }

        const closer = event.target.closest('[data-dialog-close]');
        if (closer) closer.closest('dialog')?.close();
    });

    document.addEventListener('click', function (event) {
        if (!(event.target instanceof HTMLDialogElement)) return;
        const bounds = event.target.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right ||
            event.clientY < bounds.top || event.clientY > bounds.bottom) {
            event.target.close();
        }
    });

    document.addEventListener('submit', function (event) {
        const submit = event.target.querySelector('button[type="submit"]');
        if (!submit) return;
        const label = submit.dataset.submittingLabel;
        submit.disabled = true;
        if (label) submit.textContent = label;
    });

    const reopen = document.querySelector('dialog[data-reopen="true"]');
    if (reopen) {
        reopen.querySelector('[data-dialog-field="id"]').value = reopen.dataset.reopenId;
        reopen.querySelector('textarea[name="reason"]').value = reopen.dataset.reopenReason ?? '';
        reopen.showModal();
        reopen.querySelector('textarea, input:not([type="hidden"])')?.focus();
    }

    // Single host for the read-only deactivation preview dialog. The partial returns a
    // complete <dialog> so the native element supplies Escape, focus trapping and restore.
    const previewHost = document.createElement('div');
    previewHost.id = 'deactivation-preview-host';
    document.body.appendChild(previewHost);

    function clearError() {
        document.getElementById('deactivation-preview-error')?.remove();
    }

    function showError(message) {
        clearError();
        const notice = document.createElement('div');
        notice.id = 'deactivation-preview-error';
        notice.className = 'notice notice-error';
        notice.setAttribute('role', 'alert');
        notice.textContent = message;
        const main = document.getElementById('main-content');
        (main || document.body).prepend(notice);
    }

    let previewRequestId = 0;

    document.addEventListener('click', function (event) {
        const trigger = event.target.closest('[data-deactivation-preview]');
        if (!trigger) return;

        event.preventDefault();
        const requestId = ++previewRequestId;
        clearError();
        previewHost.innerHTML = '';

        fetch(trigger.dataset.deactivationPreview, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin'
        })
            .then(function (response) {
                if (requestId !== previewRequestId) return null;
                if (!response.ok) throw new Error('Deactivation preview request failed.');
                return response.text();
            })
            .then(function (html) {
                if (html === null || requestId !== previewRequestId) return;
                previewHost.innerHTML = html;
                const dialog = previewHost.querySelector('dialog');
                if (!dialog) throw new Error('Deactivation preview dialog was not returned.');
                dialog.addEventListener('close', function () {
                    previewHost.innerHTML = '';
                    trigger.focus();
                }, { once: true });
                dialog.showModal();
            })
            .catch(function () {
                if (requestId !== previewRequestId) return;
                previewHost.innerHTML = '';
                showError('Unable to load the deactivation preview. Please try again.');
            });
    });
});
