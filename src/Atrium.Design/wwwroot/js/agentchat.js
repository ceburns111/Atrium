// Keep the message log pinned to the newest message as tokens stream in. Called from the AgentChat
// component after each render; the component guards prerender/disconnect, so this stays trivial.
//
// Only auto-scroll when the user is already near the bottom, so scrolling up to re-read an earlier
// message mid-stream isn't yanked back down on the next token.
export function scrollToEnd(element) {
    if (!element) {
        return;
    }
    const distanceFromBottom =
        element.scrollHeight - element.scrollTop - element.clientHeight;
    if (distanceFromBottom <= 40) {
        element.scrollTop = element.scrollHeight;
    }
}
