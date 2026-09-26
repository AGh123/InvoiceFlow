const focusHandlers = new WeakMap();

export function selectOnFocus(input) {
    if (focusHandlers.has(input)) return;
    const handler = () => input.select();
    input.addEventListener("focus", handler);
    focusHandlers.set(input, handler);
}

export function uninstallSelectOnFocus(input) {
    const handler = focusHandlers.get(input);
    if (!handler) return;
    input.removeEventListener("focus", handler);
    focusHandlers.delete(input);
}
