// Door scanner camera adapter. Uses the locally served jsQR decoder and the shared
// scanner API; a denied or missing camera renders the camera-unavailable outcome while the pass
// code and manual lookup modes stay usable. The camera stops after an outcome and Scan next resumes.
(function () {
    'use strict';

    var api = window.AttendTaScanner;
    var root = document.querySelector('[data-scanner]');
    if (!api || !root || typeof window.jsQR !== 'function') return;

    var video = root.querySelector('[data-scanner-video]');
    var note = root.querySelector('[data-camera-note]');
    var startButton = root.querySelector('[data-camera-start]');
    var stopButton = root.querySelector('[data-camera-stop]');
    var nextButton = root.querySelector('[data-scan-next]');
    var scanForm = root.querySelector('[data-scan-form]');
    var canvas = document.createElement('canvas');
    var context = canvas.getContext('2d', { willReadFrequently: true });

    if (!scanForm) return;

    var stream = null;
    var frame = null;
    var scanning = false;
    var busy = false;
    var starting = false;
    var generation = 0;

    var unavailableMessage = 'The camera is not available. Use the pass code or manual lookup instead.';
    var deniedMessage = 'The camera could not be started. Allow camera access or use the pass code or manual lookup instead.';

    function setNote(text) {
        if (note) note.textContent = text;
    }

    function showStart() {
        if (startButton) { startButton.hidden = false; startButton.disabled = false; }
        if (stopButton) stopButton.hidden = true;
        if (nextButton) nextButton.hidden = true;
    }

    function showNext() {
        if (startButton) { startButton.hidden = true; startButton.disabled = false; }
        if (stopButton) stopButton.hidden = true;
        if (nextButton) nextButton.hidden = false;
    }

    function stop() {
        // Permission prompts can resolve after switching modes or leaving the page. Invalidate
        // their continuations so a late stream cannot restart a hidden camera or leak tracks.
        generation += 1;
        starting = false;
        scanning = false;
        if (frame) {
            cancelAnimationFrame(frame);
            frame = null;
        }
        if (stream) {
            stream.getTracks().forEach(function (track) { track.stop(); });
            stream = null;
        }
        if (video) video.srcObject = null;
        showStart();
    }

    function unavailable(message) {
        stop();
        setNote(message);
        api.render({ outcome: 'camera-unavailable', message: message });
    }

    function tick() {
        if (!scanning) return;

        if (video && video.readyState === video.HAVE_ENOUGH_DATA && context) {
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            context.drawImage(video, 0, 0, canvas.width, canvas.height);
            var image = context.getImageData(0, 0, canvas.width, canvas.height);
            var code = window.jsQR(image.data, image.width, image.height, { inversionAttempts: 'dontInvert' });

            if (code && code.data && !busy) {
                busy = true;
                scanning = false;
                if (nextButton) nextButton.hidden = true;
                api.postScan(scanForm, code.data).then(function () {
                    busy = false;
                    stop();
                    // One resume control: the outcome is shown, so offer Scan next rather than
                    // both Scan next and Start camera.
                    showNext();
                });
                return;
            }
        }

        frame = requestAnimationFrame(tick);
    }

    function start() {
        if (starting || scanning || busy) return;
        if (!navigator.mediaDevices || typeof navigator.mediaDevices.getUserMedia !== 'function') {
            unavailable(unavailableMessage);
            return;
        }

        var requestGeneration = ++generation;
        starting = true;
        // Keep a visible, disabled control while the permission prompt is open.
        if (startButton) { startButton.hidden = false; startButton.disabled = true; }
        if (nextButton) nextButton.hidden = true;
        setNote('Waiting for camera access. You can use manual lookup at any time.');
        navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false })
            .then(function (mediaStream) {
                if (requestGeneration !== generation) {
                    mediaStream.getTracks().forEach(function (track) { track.stop(); });
                    return;
                }
                starting = false;
                if (startButton) startButton.disabled = false;
                stream = mediaStream;
                if (video) {
                    video.srcObject = mediaStream;
                    var played = video.play();
                    if (played && typeof played.catch === 'function') played.catch(function () {});
                }
                scanning = true;
                busy = false;
                if (startButton) startButton.hidden = true;
                if (stopButton) stopButton.hidden = false;
                if (nextButton) nextButton.hidden = true;
                setNote("Point the camera at the member's pass.");
                frame = requestAnimationFrame(tick);
            })
            .catch(function () {
                if (requestGeneration !== generation) return;
                unavailable(deniedMessage);
            });
    }

    if (startButton) startButton.addEventListener('click', start);
    if (stopButton) stopButton.addEventListener('click', stop);
    if (nextButton) nextButton.addEventListener('click', start);

    document.addEventListener('scanner:camera-stop', stop);
    window.addEventListener('pagehide', stop);
}());
