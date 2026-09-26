const observers = new WeakMap();
const comboKeys = new WeakMap();
const calendarKeys = new WeakMap();

export function installCalendarKeys(root) {
    if (calendarKeys.has(root)) return;
    const handler = event => {
        if (event.target.classList.contains("calendar-day") &&
            ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown"].includes(event.key)) {
            event.preventDefault();
        }
    };
    root.addEventListener("keydown", handler);
    calendarKeys.set(root, handler);
}

export function uninstallCalendarKeys(root) {
    const handler = calendarKeys.get(root);
    if (handler) {
        root.removeEventListener("keydown", handler);
        calendarKeys.delete(root);
    }
}

export function installComboboxKeys(input) {
    if (comboKeys.has(input)) return;
    const handler = event => {
        if (input.getAttribute("aria-expanded") === "true" &&
            ["ArrowDown", "ArrowUp", "Enter", "Escape"].includes(event.key)) {
            event.preventDefault();
        }
    };
    input.addEventListener("keydown", handler);
    comboKeys.set(input, handler);
}

export function uninstallComboboxKeys(input) {
    const handler = comboKeys.get(input);
    if (handler) {
        input.removeEventListener("keydown", handler);
        comboKeys.delete(input);
    }
}

export function watchOutside(root, callback) {
    if (!root?.isConnected) return;
    if (observers.has(root)) return;
    const pointerHandler = event => {
        if (!root.contains(event.target)) callback.invokeMethodAsync("CloseFromOutside");
    };
    const focusHandler = event => {
        if (!root.contains(event.target)) callback.invokeMethodAsync("CloseFromOutside");
    };
    const keyHandler = event => {
        if (event.target.matches('.sort-trigger, .actions-trigger, [role="option"], [role="menuitem"]') &&
            ["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
            event.preventDefault();
        }
    };
    document.addEventListener("pointerdown", pointerHandler);
    document.addEventListener("focusin", focusHandler);
    root.addEventListener("keydown", keyHandler);
    observers.set(root, { pointerHandler, focusHandler, keyHandler });
}

export function unwatchOutside(root) {
    const handlers = observers.get(root);
    if (handlers) {
        document.removeEventListener("pointerdown", handlers.pointerHandler);
        document.removeEventListener("focusin", handlers.focusHandler);
        root.removeEventListener("keydown", handlers.keyHandler);
        observers.delete(root);
    }
}

export function focusById(id) { document.getElementById(id)?.focus(); }

export function focusIfConnected(element) {
    if (element?.isConnected) element.focus();
}

export function placeMenu(trigger, menu) {
    if (!trigger?.isConnected || !menu?.isConnected) return;
    const rect = trigger.getBoundingClientRect();
    menu.dataset.flip = window.innerHeight - rect.bottom < menu.offsetHeight + 12 && rect.top > menu.offsetHeight + 12
        ? "true" : "false";
}
