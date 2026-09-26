export function selectOnFocus(input) {
    input.addEventListener("focus", () => input.select());
}
