// Drives the native <dialog> element. showModal() is what gives us the free focus trap, Esc-to-close,
// top-layer stacking and ::backdrop — the `open` attribute alone would not. Both calls are guarded so
// Blazor re-renders can't double-open or double-close (either throws in the DOM).
export function showModal(dialog) {
    if (dialog && !dialog.open) {
        dialog.showModal();
    }
}

export function close(dialog) {
    if (dialog && dialog.open) {
        dialog.close();
    }
}
