# PDF.js 6.5.438

Self-hosted browser renderer from Mozilla's `pdfjs-dist@6.5.438` npm distribution.
Upstream: https://github.com/mozilla/pdf.js
Usage reference: https://mozilla.github.io/pdf.js/examples/

The adjacent minified modules and LICENSE are copied without modification from that exact
package's build directory and license. No CDN requests are required. The certificate modal
loads the renderer on demand and points its worker to this same local directory.

To update, retrieve the reviewed pinned package with `npm pack pdfjs-dist@<version>` and copy
`build/pdf.min.mjs`, `build/pdf.worker.min.mjs` and `LICENSE`
into this directory. Update the version here and rerun the certificate browser-flow tests.
