import { watchOutside as watch, unwatchOutside, focusById, focusIfConnected } from "../../../../js/floating-control.js";

export { unwatchOutside, focusById, focusIfConnected };

export function watchOutside(root, callback) {
    watch(root, callback, '.invoice-actions__trigger, [role="menuitem"]');
}

export function placeMenu(trigger, menu) {
    if (!trigger?.isConnected || !menu?.isConnected) return;
    const rect = trigger.getBoundingClientRect();
    menu.dataset.flip = window.innerHeight - rect.bottom < menu.offsetHeight + 12 && rect.top > menu.offsetHeight + 12
        ? "true" : "false";
}
