(function () {
    'use strict';

    // One selection surface per list. Selection is per page and ephemeral: it lives only in the
    // checkboxes, is never stored, and is cleared on load and on a bfcache restore. Every hook is
    // scoped to [data-bulk-root] so two surfaces can coexist without sharing state.
    const ROW = '[data-bulk-row]';

    function create(root) {
        if (!root) return null;

        const count = root.querySelector('[data-bulk-count]');
        const toggleAll = root.querySelector('[data-bulk-all]');
        const openButton = root.querySelector('[data-bulk-open]');
        const clearButton = root.querySelector('[data-bulk-clear]');
        const selectAllButton = root.querySelector('[data-bulk-select-all]');
        const dialog = root.querySelector('[data-bulk-dialog]');
        const noun = root.dataset.bulkNoun || 'record';

        function rows() {
            return Array.prototype.slice.call(root.querySelectorAll(ROW));
        }

        function selected() {
            return rows().filter(function (row) { return row.checked; });
        }

        function label(row) {
            return row.dataset.bulkLabel || row.value;
        }

        function sync() {
            const chosen = selected();
            const total = rows().length;

            if (count) count.textContent = chosen.length + ' selected';
            if (openButton) openButton.disabled = chosen.length === 0;
            // An enabled control that cannot act is forbidden, so each page action is disabled
            // exactly when it would change nothing: select all when there is nothing left to
            // tick, deselect all when nothing is ticked (which also covers a page with no rows).
            if (selectAllButton) selectAllButton.disabled = total === 0 || chosen.length === total;
            if (clearButton) clearButton.disabled = chosen.length === 0;
            if (toggleAll) {
                toggleAll.disabled = total === 0;
                toggleAll.checked = total > 0 && chosen.length === total;
                toggleAll.indeterminate = chosen.length > 0 && chosen.length < total;
            }

            return chosen;
        }

        function clear() {
            rows().forEach(function (row) { row.checked = false; });
            return sync();
        }

        function selectAll() {
            rows().forEach(function (row) { row.checked = true; });
            return sync();
        }

        function open() {
            const chosen = selected();
            if (!dialog || chosen.length === 0) return false;

            const ids = dialog.querySelector('[data-bulk-dialog-ids]');
            if (ids) {
                ids.replaceChildren.apply(ids, chosen.map(function (row) {
                    const input = document.createElement('input');
                    input.type = 'hidden';
                    input.name = 'ids';
                    input.value = row.value;
                    return input;
                }));
            }

            const summary = dialog.querySelector('[data-bulk-dialog-summary]');
            if (summary) {
                summary.textContent = chosen.length + ' ' + noun + (chosen.length === 1 ? '' : 's') + ' selected.';
            }

            const list = dialog.querySelector('[data-bulk-dialog-list]');
            if (list) {
                list.replaceChildren.apply(list, chosen.map(function (row) {
                    const item = document.createElement('li');
                    item.textContent = label(row);
                    return item;
                }));
            }

            // site.js's delegated [data-dialog-open] handler would show the dialog before the ids
            // exist, so this feature opens the dialog itself once the form is populated.
            dialog.showModal();
            // showModal() focuses the first focusable descendant, which is the close button.
            // Land on the heading instead: the summary and consequence are linked to the
            // dialog through aria-describedby, so the reader hears what is about to happen.
            const heading = dialog.querySelector('h2');
            if (heading) heading.focus();
            return true;
        }

        root.addEventListener('change', function (event) {
            const target = event.target;
            if (target === toggleAll) {
                rows().forEach(function (row) { row.checked = toggleAll.checked; });
            } else if (!target || typeof target.matches !== 'function' || !target.matches(ROW)) {
                return;
            }

            sync();
        });

        if (openButton) openButton.addEventListener('click', open);
        if (clearButton) clearButton.addEventListener('click', clear);
        if (selectAllButton) selectAllButton.addEventListener('click', selectAll);

        return {
            sync: sync,
            clear: clear,
            selectAll: selectAll,
            open: open,
            selectedIds: function () {
                return selected().map(function (row) { return row.value; });
            }
        };
    }

    function initialize() {
        const instances = Array.prototype.slice.call(document.querySelectorAll('[data-bulk-root]'))
            .map(function (root) { return create(root); })
            .filter(Boolean);

        instances.forEach(function (instance) { instance.sync(); });

        if (window && typeof window.addEventListener === 'function') {
            window.addEventListener('pageshow', function (event) {
                // A bfcache restore brings the old DOM back with its ticks still set.
                if (event.persisted) instances.forEach(function (instance) { instance.clear(); });
            });
        }

        return instances;
    }

    window.AttendTaBulkSelection = { create: create, initialize: initialize };

    if (typeof document !== 'undefined') {
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize);
        else initialize();
    }
})();
