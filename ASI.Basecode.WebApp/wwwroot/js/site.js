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
        if (event.target instanceof HTMLDialogElement) event.target.close();
    });

    document.addEventListener('submit', function (event) {
        // A whitespace-only reason passes the native `required` check, so refuse it here before
        // the POST loses the dialog and the typed text.
        const reason = event.target.querySelector('textarea[name="reason"]');
        if (reason && !reason.value.trim()) {
            reason.setCustomValidity('A reason is required.');
            reason.reportValidity();
            event.preventDefault();
            return;
        }

        const submit = event.target.querySelector('button[type="submit"]');
        if (!submit) return;
        const label = submit.dataset.submittingLabel;
        submit.disabled = true;
        if (label) submit.textContent = label;
    });

    // Clear the custom reason message as soon as the user corrects the field.
    document.addEventListener('input', function (event) {
        if (event.target instanceof HTMLTextAreaElement && event.target.name === 'reason') {
            event.target.setCustomValidity('');
        }
    });

    const reopen = document.querySelector('dialog[data-reopen="true"]');
    if (reopen) {
        reopen.querySelector('[data-dialog-field="id"]').value = reopen.dataset.reopenId;
        const reason = reopen.querySelector('textarea[name="reason"]');
        if (reason) reason.value = reopen.dataset.reopenReason ?? '';
        reopen.showModal();
        reopen.querySelector('textarea, input:not([type="hidden"])')?.focus();
    }

    // Single host for the fetched dialogs. Each partial returns a complete <dialog> so the
    // native element supplies Escape, focus trapping and focus restore.
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

    // One delegated handler serves the read-only deactivation preview and the account/membership
    // access dialogs. Each source names its trigger attribute, the dataset key holding the URL and
    // the message shown when the fetch fails.
    function wireFetchedDialog(sources) {
        document.addEventListener('click', function (event) {
            const source = sources.find(function (entry) {
                return event.target.closest(entry.selector);
            });
            if (!source) return;

            const trigger = event.target.closest(source.selector);
            event.preventDefault();
            const requestId = ++previewRequestId;
            clearError();
            previewHost.innerHTML = '';

            fetch(trigger.dataset[source.url], {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            })
                .then(function (response) {
                    if (requestId !== previewRequestId) return null;
                    if (!response.ok) throw new Error(source.error);
                    return response.text();
                })
                .then(function (html) {
                    if (html === null || requestId !== previewRequestId) return;
                    previewHost.innerHTML = html;
                    const dialog = previewHost.querySelector('dialog');
                    if (!dialog) throw new Error(source.error);
                    dialog.addEventListener('close', function () {
                        previewHost.innerHTML = '';
                        trigger.focus();
                    }, { once: true });
                    dialog.showModal();
                })
                .catch(function () {
                    if (requestId !== previewRequestId) return;
                    previewHost.innerHTML = '';
                    showError(source.error);
                });
        });
    }

    wireFetchedDialog([
        {
            selector: '[data-deactivation-preview]',
            url: 'deactivationPreview',
            error: 'Unable to load the deactivation preview. Please try again.'
        },
        {
            selector: '[data-access-dialog]',
            url: 'accessDialog',
            error: 'Unable to load the access options. Please try again.'
        }
    ]);
});
