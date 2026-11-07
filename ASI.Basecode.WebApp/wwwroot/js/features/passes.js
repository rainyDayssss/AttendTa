// Door pass QR drawing and PNG download (register M-03). Loaded only by the Door Passes view, after
// the locally served qrcode-generator library. The token is read from a data attribute because the
// browser draws the code; 4.1 replaces this with a server-rendered PNG.
(function () {
    'use strict';

    var QUIET_ZONE = 4;
    var TARGET_SIZE = 168;

    function draw(canvas) {
        var token = canvas.getAttribute('data-pass-token');
        if (!token || typeof qrcode !== 'function') return false;

        var code = qrcode(0, 'M');
        code.addData(token);
        code.make();

        var count = code.getModuleCount();
        var modules = count + QUIET_ZONE * 2;
        // Render at least the display size so the saved PNG stays crisp; CSS scales it down.
        var scale = Math.max(1, Math.ceil(TARGET_SIZE / modules));
        var size = modules * scale;
        canvas.width = size;
        canvas.height = size;

        var context = canvas.getContext('2d');
        if (!context) return false;

        context.fillStyle = '#ffffff';
        context.fillRect(0, 0, size, size);
        context.fillStyle = '#090d16';
        for (var row = 0; row < count; row += 1) {
            for (var column = 0; column < count; column += 1) {
                if (code.isDark(row, column)) {
                    context.fillRect((column + QUIET_ZONE) * scale, (row + QUIET_ZONE) * scale, scale, scale);
                }
            }
        }

        return true;
    }

    function save(canvas, fileName) {
        var link = document.createElement('a');
        link.href = canvas.toDataURL('image/png');
        link.download = fileName || 'door-pass.png';
        link.rel = 'noopener';
        document.body.appendChild(link);
        link.click();
        link.remove();
    }

    document.querySelectorAll('canvas[data-pass-token]').forEach(function (canvas) {
        if (draw(canvas)) return;

        // Never offer a download for a code that could not be drawn.
        var card = canvas.closest('.pass-card');
        var button = card && card.querySelector('[data-pass-download]');
        if (button) button.disabled = true;
    });

    document.addEventListener('click', function (event) {
        var button = event.target.closest('[data-pass-download]');
        if (!button) return;

        var card = button.closest('.pass-card');
        var canvas = card && card.querySelector('canvas[data-pass-token]');
        if (!canvas) return;

        save(canvas, button.getAttribute('data-pass-file'));
    });
}());
