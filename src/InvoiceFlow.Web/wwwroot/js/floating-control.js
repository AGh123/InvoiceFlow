const observers = new WeakMap();

export function watchOutside(root, callback, navigationSelector = null) {
    if (!root?.isConnected || observers.has(root)) return;
    const outsideHandler = event => {
        if (!root.contains(event.target)) callback.invokeMethodAsync("CloseFromOutside");
    };
    const keyHandler = event => {
        if (navigationSelector && event.target.matches(navigationSelector) &&
            ["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
            event.preventDefault();
        }
    };
    document.addEventListener("pointerdown", outsideHandler);
    document.addEventListener("focusin", outsideHandler);
    root.addEventListener("keydown", keyHandler);
    observers.set(root, { outsideHandler, keyHandler });
}

export function unwatchOutside(root) {
    const handlers = observers.get(root);
    if (!handlers) return;
    document.removeEventListener("pointerdown", handlers.outsideHandler);
    document.removeEventListener("focusin", handlers.outsideHandler);
    root.removeEventListener("keydown", handlers.keyHandler);
    observers.delete(root);
}

export function focusById(id) { document.getElementById(id)?.focus(); }

export function focusIfConnected(element) {
    if (element?.isConnected) element.focus();
}
