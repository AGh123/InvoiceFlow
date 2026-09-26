import { watchOutside, unwatchOutside } from "../../../js/floating-control.js";

const comboKeys = new WeakMap();

export { watchOutside, unwatchOutside };

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
    if (!handler) return;
    input.removeEventListener("keydown", handler);
    comboKeys.delete(input);
}
