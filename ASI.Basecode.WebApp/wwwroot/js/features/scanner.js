// Door scanner shared behaviour. Loaded by the CheckIn view after the locally
// served jsQR decoder. The camera adapter uses the small API this file exposes to post scans
// and render the JSON outcome.
(function () {
    'use strict';

    var root = document.querySelector('[data-scanner]');
    if (!root) return;

    var result = root.querySelector('[data-scan-result]');
    var redirectOutcome = root.querySelector('[data-redirect-outcome]');
    if (redirectOutcome) redirectOutcome.focus();
    var modes = Array.prototype.slice.call(root.querySelectorAll('[data-scanner-mode]'));

    function panelFor(mode) {
        var id = mode.getAttribute('aria-controls');
        return id ? document.getElementById(id) : null;
    }

    function selectMode(name, moveFocus) {
        modes.forEach(function (mode) {
            var selected = mode.getAttribute('data-scanner-mode') === name;
            mode.setAttribute('aria-selected', selected ? 'true' : 'false');
            mode.setAttribute('tabindex', selected ? '0' : '-1');
            var panel = panelFor(mode);
            if (panel) panel.hidden = !selected;
            if (selected && moveFocus) mode.focus();
        });

        if (name !== 'camera') {
            document.dispatchEvent(new CustomEvent('scanner:camera-stop'));
        }
    }

    modes.forEach(function (mode, index) {
        mode.setAttribute('tabindex', mode.getAttribute('aria-selected') === 'true' ? '0' : '-1');
        mode.addEventListener('click', function () {
            selectMode(mode.getAttribute('data-scanner-mode'));
        });
        mode.addEventListener('keydown', function (event) {
            var next = null;
            if (event.key === 'ArrowRight') next = modes[(index + 1) % modes.length];
            else if (event.key === 'ArrowLeft') next = modes[(index - 1 + modes.length) % modes.length];
            else if (event.key === 'Home') next = modes[0];
            else if (event.key === 'End') next = modes[modes.length - 1];
            if (!next) return;
            event.preventDefault();
            selectMode(next.getAttribute('data-scanner-mode'), true);
        });
    });

    // The panels render visible so manual lookup works without JavaScript.
    // With JavaScript the tabs take over and show only the selected mode.
    var initialMode = modes.filter(function (mode) {
        return mode.getAttribute('aria-selected') === 'true';
    })[0] || modes[0];
    if (initialMode) selectMode(initialMode.getAttribute('data-scanner-mode'));

    function title(outcome) {
        switch (outcome) {
            case 'success': return 'Checked in';
            case 'repeat': return 'Already checked in';
            case 'closed': return 'Check-in closed';
            case 'camera-unavailable': return 'Camera unavailable';
            default: return 'Not checked in';
        }
    }

    function formatTime(value) {
        var date = new Date(value);
        if (isNaN(date.getTime())) return '';
        var parts = {};
        new Intl.DateTimeFormat('en-CA', {
            timeZone: 'Asia/Manila',
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            hour12: true
        }).formatToParts(date).forEach(function (part) {
            parts[part.type] = part.value;
        });
        return parts.year + '-' + parts.month + '-' + parts.day + ' ' + parts.hour + ':' + parts.minute;
    }

    function render(outcome) {
        if (!result) return;
        result.innerHTML = '';

        var box = document.createElement('div');
        box.id = 'check-in-result';
        box.setAttribute('tabindex', '-1');
        box.className = 'scanner-outcome scanner-outcome-' + (outcome.outcome || 'invalid');
        box.setAttribute('role', outcome.outcome === 'success' || outcome.outcome === 'repeat' ? 'status' : 'alert');

        var heading = document.createElement('h3');
        heading.textContent = title(outcome.outcome);
        box.appendChild(heading);

        if (outcome.memberName) {
            var name = document.createElement('p');
            name.className = 'scanner-outcome-name';
            name.textContent = outcome.memberName;
            box.appendChild(name);
        }

        if (outcome.studentNumber) {
            var student = document.createElement('p');
            student.className = 'scanner-outcome-meta numeric';
            student.textContent = outcome.studentNumber;
            box.appendChild(student);
        }

        if (outcome.recordedAt) {
            var time = document.createElement('p');
            time.className = 'scanner-outcome-meta';
            time.textContent = formatTime(outcome.recordedAt);
            box.appendChild(time);
        }

        if (outcome.message) {
            var message = document.createElement('p');
            message.textContent = outcome.message;
            box.appendChild(message);
        }

        result.appendChild(box);
    }

    function cell(text, label) {
        var td = document.createElement('td');
        td.textContent = text || '';
        td.setAttribute('data-label', label);
        return td;
    }

    // A successful scan adds its row to the server-rendered log without a page reload, so the
    // operator sees the recorded check-in immediately. An existing row keeps its own action state.
    function appendLog(outcome) {
        var body = document.querySelector('[data-scan-log]');
        if (!body || !outcome.registrationId) return;
        if (body.querySelector('tr[data-registration-id="' + outcome.registrationId + '"]')) return;

        var empty = body.querySelector('td.empty-state');
        if (empty && empty.parentElement) empty.parentElement.remove();

        var row = document.createElement('tr');
        row.setAttribute('data-registration-id', outcome.registrationId);
        row.appendChild(cell(outcome.memberName, 'Participant'));

        var student = cell(outcome.studentNumber, 'Student number');
        student.className = 'numeric cell-identifier';
        row.appendChild(student);

        var time = cell(formatTime(outcome.recordedAt), 'Recorded');
        time.className = 'numeric cell-date';
        row.appendChild(time);

        var outcomeCell = document.createElement('td');
        outcomeCell.setAttribute('data-label', 'Outcome');
        var badge = document.createElement('span');
        badge.className = 'badge badge-open';
        badge.textContent = 'Recorded';
        outcomeCell.appendChild(badge);
        row.appendChild(outcomeCell);

        row.appendChild(cell('—', 'Reason'));

        var action = document.createElement('td');
        action.className = 'actions';
        action.setAttribute('data-label', 'Action');
        var actions = document.createElement('div');
        actions.className = 'row-actions';
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'button button-quiet-danger';
        button.setAttribute('data-dialog-open', 'invalidate-dialog');
        button.setAttribute('data-dialog-value-id', String(outcome.registrationId));
        button.setAttribute('data-dialog-value-summary',
            'Invalidate the check-in for ' + outcome.memberName + ' (' + outcome.studentNumber + ')?');
        button.textContent = 'Invalidate';
        actions.appendChild(button);
        action.appendChild(actions);
        row.appendChild(action);

        body.prepend(row);
    }

    function requestToken() {
        var meta = document.querySelector('meta[name="RequestVerificationToken"]');
        return meta ? meta.getAttribute('content') : null;
    }

    function postScan(form, value) {
        var body = new URLSearchParams();
        body.set('eventId', form.elements.eventId.value);
        body.set('token', value);

        // AD-8: AJAX mutations carry the layout meta token as the antiforgery header, not in the body.
        var headers = {
            'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
            'X-Requested-With': 'XMLHttpRequest',
            'Accept': 'application/json'
        };
        var token = requestToken();
        if (token) headers.RequestVerificationToken = token;

        return fetch(form.action, {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: body.toString()
        })
            .then(function (response) {
                return response.json().catch(function () {
                    return { outcome: 'invalid', message: 'The scan could not be processed. Try again.' };
                });
            })
            .then(function (outcome) {
                render(outcome);
                if (outcome.outcome === 'success' || outcome.outcome === 'repeat') appendLog(outcome);
                return outcome;
            })
            .catch(function () {
                var outcome = {
                    outcome: 'invalid',
                    message: 'The scan could not be processed. Check the connection and try again.'
                };
                render(outcome);
                return outcome;
            });
    }

    window.AttendTaScanner = {
        postScan: postScan,
        render: render,
        selectMode: selectMode
    };
}());
