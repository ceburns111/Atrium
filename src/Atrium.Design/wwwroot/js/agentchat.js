// Keep the message log pinned to the newest message as tokens stream in. Called from the AgentChat
// component after each render; the component guards prerender/disconnect, so this stays trivial.
export function scrollToEnd(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}
