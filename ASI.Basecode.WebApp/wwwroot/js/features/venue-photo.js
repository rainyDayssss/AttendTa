(() => {
    'use strict';
    // Delegated so the preview also works after the record dialog swaps its body.
    let objectUrl;

    function clear(preview) {
        if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
        preview.querySelector('img')?.remove();
        preview.hidden = true;
    }

    document.addEventListener('change', event => {
        const input = event.target.closest('[data-photo-input]');
        if (!input) return;
        const preview = input.closest('.field')?.querySelector('[data-photo-preview]');
        if (!preview) return;
        const file = input.files && input.files[0];
        const isAccepted = file && (file.type === 'image/jpeg' || file.type === 'image/jpg' || file.type === 'image/png');
        if (!isAccepted) { clear(preview); return; }
        if (objectUrl) URL.revokeObjectURL(objectUrl);
        objectUrl = URL.createObjectURL(file);
        let image = preview.querySelector('img');
        if (!image) {
            image = document.createElement('img');
            preview.prepend(image);
        }
        image.src = objectUrl;
        image.alt = `Preview of the selected photo: ${file.name}`;
        preview.hidden = false;
    });
})();
