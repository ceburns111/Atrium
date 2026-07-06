// Drives the native <dialog> element. showModal() is what gives us the free focus trap, Esc-to-close,
// top-layer stacking and ::backdrop — the `open` attribute alone would not. Both calls are guarded so
// Blazor re-renders can't double-open or double-close (either throws in the DOM).
export function showModal(dialog) {
    if (dialog && !dialog.open) {
        dialog.showModal();
        // With no [autofocus] inside, showModal() focuses the first focusable control — the ✕ close
        // button — and the programmatic focus paints its ring, so every dialog opened with a stray
        // ring on ✕. Honor an explicit [autofocus] (showModal already focused it); otherwise park
        // focus on the panel (tabindex="-1", ring suppressed) so Tab still reaches controls in order.
        if (!dialog.querySelector("[autofocus]")) {
            dialog.querySelector(".dialog__panel")?.focus();
        }
    }
}

export function close(dialog) {
    if (dialog && dialog.open) {
        dialog.close();
    }
}
