// Renders a generated web component inside an already-sandboxed iframe and relays the
// results of the model's behavioural checks back to Blazor. The iframe carries
// sandbox="allow-scripts" (no allow-same-origin) and the srcdoc carries a strict CSP, so
// the component cannot reach this page, its storage, or the network.

export function render(iframe, srcdoc, nonce, dotNetRef) {
    let settled = false;

    function finish(results, error) {
        if (settled) return;
        settled = true;
        window.removeEventListener('message', onMessage);
        dotNetRef.invokeMethodAsync('OnChecksComplete', results, error || null);
    }

    const collected = [];

    function onMessage(e) {
        if (e.source !== iframe.contentWindow) return;
        const d = e.data;
        if (!d || d.__forge !== nonce) return;
        if (d.type === 'result' && d.result) {
            collected.push(d.result);
        } else if (d.type === 'done') {
            finish(collected, d.error);
        }
    }

    window.addEventListener('message', onMessage);
    iframe.addEventListener('load', () => {
        // If the component never runs the harness (e.g. it threw during parse), don't hang.
        setTimeout(() => finish(collected, collected.length ? null : 'The component did not report any check results.'), 4000);
    });

    iframe.setAttribute('srcdoc', srcdoc);
}
