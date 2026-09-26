import { watchOutside, unwatchOutside, focusById, focusIfConnected } from "../../../js/floating-control.js";

const calendarKeys = new WeakMap();

export { watchOutside, unwatchOutside, focusById, focusIfConnected };

export function installCalendarKeys(root) {
    if (calendarKeys.has(root)) return;
    const handler = event => {
        if (event.target.classList.contains("date-picker__day") &&
            ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown"].includes(event.key)) {
            event.preventDefault();
        }
    };
    root.addEventListener("keydown", handler);
    calendarKeys.set(root, handler);
}

export function uninstallCalendarKeys(root) {
    const handler = calendarKeys.get(root);
    if (!handler) return;
    root.removeEventListener("keydown", handler);
    calendarKeys.delete(root);
}
