# Shared record editor

Render `Views/Shared/_RecordDialog.cshtml` once and set `ViewData["Feature"] = "record-dialog"` on the list. Use ordinary same-origin links with `data-record-open="stable-unique-key"` and `data-record-title="Edit record"`; their href remains the full-page fallback. Keys identify the focus destination after a successful list reload.

The script sends `X-Record-Dialog: true`. GET returns an HTML partial containing `form[data-record-form]`. POST uses `FormData` (including antiforgery and file inputs), so consumers can adopt multipart uploads. Server validation returns the same partial with entered values, field errors and an accessible validation summary; successful POST returns JSON `{ "saved": true }` and sets the normal completion TempData toast. Other responses show a recoverable error without claiming success. Never redirect dialog POSTs to a sign-in or list HTML response as a success signal.

Use `data-record-close` on type-button Cancel controls. Native `showModal()` provides background inertness, keyboard containment and Escape handling; the script restores focus on close and after successful reload. During save, close/Escape and repeat submissions are blocked. An uncertain transport result is never automatically retried. Keep status/destructive actions outside the editor.

Event Type is the real-service tracer. Member (2.13) and Venue (2.14) can reuse the shell, stylesheet and script with their own form partials and presentation-only controller response handling. Standalone routes must remain usable without JavaScript.
