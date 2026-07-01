// Theme interop for the ThemeToggle. The host page's inline <script> sets the initial data-theme
// before first paint (no flash); this module only handles runtime flips and reads the current value.
export function get() {
    return document.documentElement.dataset.theme || "light";
}

export function set(theme) {
    document.documentElement.dataset.theme = theme;
    try {
        localStorage.setItem("atrium.theme", theme);
    } catch (e) {
        // localStorage may be unavailable (private mode / blocked) — the choice just won't persist.
    }
}
