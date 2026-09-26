import { watchOutside as watch, unwatchOutside, focusById, focusIfConnected } from "../../../../js/floating-control.js";

export { unwatchOutside, focusById, focusIfConnected };

export function watchOutside(root, callback) {
    watch(root, callback, '.invoice-sort__trigger, [role="option"]');
}
