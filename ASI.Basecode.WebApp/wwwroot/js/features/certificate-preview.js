(() => {
    'use strict';
    const dialog = document.querySelector('#certificate-preview');
    if (!dialog || typeof dialog.showModal !== 'function') return;
    const pages = dialog.querySelector('[data-pdf-pages]');
    const assets = new URL('../../lib/pdfjs/', document.currentScript.src);
    const message = dialog.querySelector('[data-pdf-message]');
    const actions = dialog.querySelector('[data-pdf-actions]');
    const help = dialog.querySelector('[data-pdf-help]');
    const retry = dialog.querySelector('[data-pdf-retry]');
    const download = dialog.querySelector('[data-pdf-download]');
    let trigger, request, objectUrl, documentTask, renderTask, pdfLibrary;
    function release() {
        renderTask?.cancel();
        renderTask = null;
        documentTask?.destroy().catch(() => {});
        documentTask = null;
        pages.replaceChildren();
        pages.hidden = true;
        download.removeAttribute('href');
        if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
    }
    async function load() {
        request?.abort();
        request = new AbortController();
        const current = request;
        release();
        actions.hidden = help.hidden = retry.hidden = true;
        message.textContent = 'Loading certificate…';
        dialog.dataset.pdfLoading = 'true';
        try {
            const response = await fetch(trigger.dataset.certificatePdf, { signal: current.signal });
            if (!response.ok || response.redirected || !response.headers.get('content-type')?.toLowerCase().startsWith('application/pdf')) throw new Error();
            const blob = await response.blob();
            if (request !== current || !dialog.open) return;
            objectUrl = URL.createObjectURL(blob);
            dialog.querySelector('[data-pdf-open]').href = trigger.dataset.certificatePdf;
            download.href = objectUrl;
            download.download = `${trigger.dataset.certificateNumber || 'certificate'}.pdf`;
            actions.hidden = help.hidden = false;
            pdfLibrary ??= await import(new URL('pdf.min.mjs', assets).href);
            const pdf = pdfLibrary;
            if (request !== current || !dialog.open) return;
            pdf.GlobalWorkerOptions.workerSrc = new URL('pdf.worker.min.mjs', assets).href;
            const data = new Uint8Array(await blob.arrayBuffer());
            if (request !== current || !dialog.open) return;
            documentTask = pdf.getDocument({ data, isEvalSupported: false });
            const pdfDocument = await documentTask.promise;
            if (request !== current || !dialog.open) return;
            pages.hidden = false;
            for (let number = 1; number <= pdfDocument.numPages; number++) {
                const page = await pdfDocument.getPage(number);
                if (request !== current || !dialog.open) return;
                const base = page.getViewport({ scale: 1 });
                const scale = Math.max(1, pages.clientWidth / base.width) * Math.min(window.devicePixelRatio || 1, 2);
                const viewport = page.getViewport({ scale });
                const canvas = window.document.createElement('canvas');
                canvas.width = Math.ceil(viewport.width);
                canvas.height = Math.ceil(viewport.height);
                canvas.setAttribute('aria-hidden', 'true');
                pages.append(canvas);
                renderTask = page.render({ canvasContext: canvas.getContext('2d'), viewport });
                await renderTask.promise;
                const text = await page.getTextContent();
                if (request !== current || !dialog.open) return;
                const transcript = window.document.createElement('p');
                transcript.className = 'sr-only';
                transcript.textContent = text.items.map(item => item.str || '').join(' ');
                pages.append(transcript);
            }
            if (request !== current || !dialog.open) return;
            message.textContent = 'Certificate loaded.';
        } catch (error) {
            if (error.name === 'AbortError' || request !== current || !dialog.open) return;
            renderTask = null;
            pages.replaceChildren();
            pages.hidden = true;
            message.textContent = objectUrl
                ? 'The PDF preview could not be displayed. Open it in a new tab, download it, or retry.'
                : 'The certificate document is unavailable. Retry, or contact an officer for help. Your list has been kept.';
            retry.hidden = false;
            message.focus();
        } finally {
            if (request === current) dialog.dataset.pdfLoading = 'false';
        }
    }
    document.addEventListener('click', event => {
        const link = event.target.closest('[data-certificate-pdf]');
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const url = new URL(link.dataset.certificatePdf, location.href);
        if (url.origin !== location.origin || document.querySelector('dialog[open]')) return;
        event.preventDefault();
        trigger = link;
        dialog.querySelector('[data-pdf-context]').textContent = link.dataset.certificateTitle;
        dialog.querySelector('#certificate-preview-title').textContent = link.dataset.certificateNumber ? `Certificate ${link.dataset.certificateNumber}` : 'Certificate';
        dialog.showModal();
        dialog.querySelector('[data-dialog-close]').focus();
        load();
    });
    retry.addEventListener('click', load);
    dialog.addEventListener('close', () => {
        request?.abort();
        release();
        trigger?.focus({ preventScroll: true });
    });
})();
