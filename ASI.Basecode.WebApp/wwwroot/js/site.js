$(function () {
    // Shared dialog policy belongs with the existing delegated dialog handlers.
    // Feature modules reuse this API; they do not install another dismissal system.
    const dialogs = (() => {
        'use strict';
        const baselines = new WeakMap();
        function snapshot(dialog) {
            return JSON.stringify(Array.from(dialog.querySelectorAll('input, select, textarea'))
                .filter(field => !field.disabled && field.type !== 'hidden')
                .map(field => [field.name, field.type === 'checkbox' || field.type === 'radio'
                    ? field.checked : field.type === 'file'
                        ? Array.from(field.files || []).map(file => [file.name, file.size, file.lastModified])
                        : field.value]));
        }
        function track(dialog, reset = false) {
            if (!baselines.has(dialog) || reset) baselines.set(dialog, snapshot(dialog));
        }
        function hasChanges(dialog) {
            return baselines.has(dialog) && baselines.get(dialog) !== snapshot(dialog);
        }
        function requestClose(dialog) {
            if (!dialog || dialog.getAttribute('aria-busy') === 'true' || dialog.querySelector('form[aria-busy="true"]')) return false;
            if (!hasChanges(dialog)) { dialog.close(); return true; }
            const existing = dialog.querySelector('[data-discard-prompt]');
            if (existing) { existing.querySelector('button').focus(); return false; }
            const returnFocus = document.activeElement;
            const prompt = document.createElement('section');
            prompt.className = 'dialog-discard-notice';
            prompt.dataset.discardPrompt = '';
            prompt.setAttribute('role', 'group');
            prompt.setAttribute('aria-label', 'Unsaved changes');
            const text = document.createElement('p');
            text.textContent = 'You have unsaved changes. Keep editing or discard them?';
            const actions = document.createElement('div');
            actions.className = 'dialog-discard-actions';
            const keep = document.createElement('button');
            keep.type = 'button'; keep.className = 'button button-secondary'; keep.textContent = 'Keep editing';
            keep.addEventListener('click', () => { prompt.remove(); returnFocus?.focus(); });
            const discard = document.createElement('button');
            discard.type = 'button'; discard.className = 'button button-danger'; discard.textContent = 'Discard changes';
            discard.addEventListener('click', () => { prompt.remove(); dialog.close(); });
            actions.append(keep, discard); prompt.append(text, actions); dialog.append(prompt); keep.focus();
            return false;
        }
        document.addEventListener('focusin', event => {
            const dialog = event.target.closest('dialog');
            if (dialog) track(dialog);
        });
        document.addEventListener('cancel', event => {
            const dialog = event.target;
            if (dialog instanceof HTMLDialogElement && (hasChanges(dialog) || dialog.getAttribute('aria-busy') === 'true' || dialog.querySelector('form[aria-busy="true"]'))) {
                event.preventDefault(); requestClose(dialog);
            }
        }, true);
        document.addEventListener('close', event => {
            if (!(event.target instanceof HTMLDialogElement)) return;
            baselines.delete(event.target);
            event.target.querySelector('[data-discard-prompt]')?.remove();
        }, true);
        return { track, hasChanges, requestClose };
    })();
    window.AttendTaDialogs = dialogs;

    const token = document.querySelector('meta[name="RequestVerificationToken"]')?.content;
    if (token) $.ajaxSetup({ headers: { RequestVerificationToken: token } });

    document.addEventListener('click', function (event) {
        const opener = event.target.closest('[data-dialog-open]');
        if (opener) {
            const dialog = document.getElementById(opener.dataset.dialogOpen);
            if (!dialog || typeof dialog.showModal !== 'function') return;
            if (opener.tagName === 'A') event.preventDefault();
            dialog.querySelector('[data-list-cancel-retry-error]')?.remove();
            const cancelSubmit = dialog.querySelector('[data-list-cancel-submit]');
            if (cancelSubmit) cancelSubmit.disabled = false;

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
            const returnFocus = resolveDialogReturnFocus(opener);
            opener.closest('[popover]')?.hidePopover?.();
            dialog.addEventListener('close', () => restoreDialogFocus(returnFocus), { once: true });
            dialogs.track(dialog, true);
            dialog.showModal();
            dialog.querySelector('textarea, input:not([type="hidden"])')?.focus();
            return;
        }

        const closer = event.target.closest('[data-dialog-close]');
        if (closer) dialogs.requestClose(closer.closest('dialog'));
    });

    // A native dialog's own padding is part of the dialog element, so the event target alone
    // cannot distinguish an interior click from a backdrop click. Decide from where the press
    // started: a text selection that begins inside the panel and releases over the backdrop must
    // not discard input. A busy dialog refuses every dismissal.
    let pressedBackdrop = false;
    document.addEventListener('pointerdown', function (event) {
        const dialog = event.target;
        if (!(dialog instanceof HTMLDialogElement)) {
            pressedBackdrop = false;
            return;
        }
        const bounds = dialog.getBoundingClientRect();
        pressedBackdrop = event.clientX < bounds.left || event.clientX > bounds.right ||
            event.clientY < bounds.top || event.clientY > bounds.bottom;
    });
    document.addEventListener('click', function (event) {
        const dialog = event.target;
        const fromBackdrop = pressedBackdrop;
        pressedBackdrop = false;
        if (!(dialog instanceof HTMLDialogElement) || !fromBackdrop) return;
        if (dialog.getAttribute('aria-busy') === 'true') return;
        dialogs.requestClose(dialog);
    });

    document.addEventListener('submit', function (event) {
        if (event.defaultPrevented) return;
        // A whitespace-only reason passes the native `required` check, so refuse it here before
        // the POST loses the dialog and the typed text.
        const reason = event.target.querySelector('textarea[name="reason"]');
        if (reason && !reason.value.trim()) {
            reason.setCustomValidity('A reason is required.');
            reason.reportValidity();
            event.preventDefault();
            return;
        }

        const submit = event.submitter || event.target.querySelector('button[type="submit"]');
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
    if (reopen && typeof reopen.showModal === 'function') {
        // The dossier may already render the failed reason into its textarea.
        // Capture the empty baseline so that restored input is still protected.
        const reason = reopen.querySelector('textarea[name="reason"]');
        const restoredReason = reopen.dataset.reopenReason ?? reason?.value ?? '';
        if (reason) reason.value = '';
        dialogs.track(reopen, true);
        reopen.querySelector('[data-dialog-field="id"]').value = reopen.dataset.reopenId;
        if (reason) reason.value = restoredReason;
        if (reopen.dataset.returnFocus) {
            const invoker = document.querySelector('[popovertarget="' + reopen.dataset.returnFocus + '"]');
            reopen.addEventListener('close', () => restoreDialogFocus(
                invoker && invoker.isConnected !== false ? invoker : document.querySelector('[data-list-region]')
            ), { once: true });
        }
        reopen.showModal();
        reopen.querySelector('textarea, input:not([type="hidden"])')?.focus();
    }

    function coverUnavailable(image) {
        const dialog = image.closest('dialog');
        const figure = image.closest('[data-cover-figure]');
        const message = dialog?.querySelector('[data-cover-error]');
        if (figure) figure.hidden = true;
        if (message) message.hidden = false;
    }
    document.addEventListener('error', event => {
        if (event.target.matches?.('[data-cover-image]')) coverUnavailable(event.target);
    }, true);
    document.querySelectorAll?.('[data-cover-image]').forEach(image => {
        if (image.complete && image.naturalWidth === 0) coverUnavailable(image);
    });

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

    // The fetch triggers sit inside a row-menu popover. Showing the modal hides that popover, so the
    // clicked button is no longer visible and cannot take focus back. Resolve the popover's own
    // invoker in the row while the popover is still open, and fall back to the trigger itself.
    function resolveDialogReturnFocus(trigger) {
        const popover = trigger.closest('[popover]');
        if (!popover || !popover.id) return trigger;
        return document.querySelector('[popovertarget="' + popover.id + '"]') || trigger;
    }

    // preventScroll keeps the table where the reader left it: the default restore would scroll the
    // row back into view and move the page under the pointer.
    function restoreDialogFocus(target) {
        if (!target || target.isConnected === false) return;
        target.focus({ preventScroll: true });
    }

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
            const returnFocus = resolveDialogReturnFocus(trigger);
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
                        restoreDialogFocus(returnFocus);
                    }, { once: true });
                    dialogs.track(dialog, true);
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
