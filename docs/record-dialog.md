# Shared record editor

Render `Views/Shared/_RecordDialog.cshtml` once and set `ViewData["Feature"] = "record-dialog"` on the list. Use ordinary same-origin links with `data-record-open="stable-unique-key"` and `data-record-title="Edit record"`; their href remains the full-page fallback. Keys identify the focus destination after a successful list reload.

Add `data-record-subtitle` to the trigger when the editor needs a persisted identity summary under the title (for example a member's name and student number). Omit it for create actions so the subtitle stays hidden.

The script sends `X-Record-Dialog: true`. GET returns an HTML partial containing `form[data-record-form]`. POST uses `FormData` (including antiforgery and file inputs), so consumers can adopt multipart uploads. Server validation returns the same partial with entered values, field errors and an accessible validation summary; successful POST returns JSON `{ "saved": true }` and sets the normal completion TempData toast. Other responses show a recoverable error without claiming success. Never redirect dialog POSTs to a sign-in or list HTML response as a success signal.

Use `data-record-close` on type-button Cancel controls. Native `showModal()` provides background inertness, keyboard containment and Escape handling; the script restores focus on close and after successful reload. Only a pointer outside the panel rectangle counts as a backdrop click and closes the dialog; a click on the dialog's own padding or header keeps it open and retains input. During save, close/Escape and repeat submissions are blocked. An uncertain transport result is never automatically retried. Keep status/destructive actions outside the editor.

Focus enters the first editable control: readonly and disabled inputs are skipped, so a read-only email never takes initial focus. A file input with a `[data-record-preview]` host previews the selected image through `URL.createObjectURL`; the image is created on selection so an empty-src image never ships, `data-record-preview-alt` supplies its alt text, and the URL is revoked on change, close and reload. A failed multipart save cannot retain the selected file, so the server renders reselect guidance beside the input and the persisted image path is rehydrated for the current-photo panel.

Member, Event Type and Venue all use the shell, stylesheet and script with their own form partials and presentation-only controller response handling. Standalone routes must remain usable without JavaScript.
