const cancelHandlers = new WeakMap();
const closeHandlers = new WeakMap();
const focusOrigins = new WeakMap();
const keydownHandlers = new WeakMap();

function restoreFocus(dialog) {
    if (!dialog.isConnected || dialog.open || !focusOrigins.has(dialog)) return;
    const focusOrigin = focusOrigins.get(dialog);
    focusOrigins.delete(dialog);
    const isVisible = element => element?.isConnected && element.getClientRects().length > 0;
    const fallback = ['main a[href]', 'main button:not([disabled])', 'header a[href]']
        .flatMap(selector => Array.from(document.querySelectorAll(selector))).find(isVisible);
    const focusTarget = focusOrigin !== document.body && isVisible(focusOrigin)
        ? focusOrigin : fallback;
    focusTarget?.focus();
}

export function showDialog(dialog, initialFocus) {
    if (!dialog?.isConnected) return;
    if (!cancelHandlers.has(dialog)) {
        const handleCancel = event => {
            if (dialog.dataset.busy === "true") {
                event.preventDefault();
            }
        };

        cancelHandlers.set(dialog, handleCancel);
        dialog.addEventListener("cancel", handleCancel);
    }

    if (!closeHandlers.has(dialog)) {
        const handleClose = () => queueMicrotask(() => restoreFocus(dialog));

        closeHandlers.set(dialog, handleClose);
        dialog.addEventListener("close", handleClose);
    }

    if (!keydownHandlers.has(dialog)) {
        const handleKeydown = event => {
            if (event.key !== "Tab") {
                return;
            }

            const focusableElements = Array.from(dialog.querySelectorAll(
                'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'))
                .filter(element => element.getClientRects().length > 0);

            if (focusableElements.length === 0) {
                event.preventDefault();
                dialog.focus();
                return;
            }

            const first = focusableElements[0];
            const last = focusableElements[focusableElements.length - 1];
            const focusIsOutside = !dialog.contains(document.activeElement);

            if (event.shiftKey && (document.activeElement === first || focusIsOutside)) {
                event.preventDefault();
                last.focus();
            }
            else if (!event.shiftKey && (document.activeElement === last || focusIsOutside)) {
                event.preventDefault();
                first.focus();
            }
        };

        keydownHandlers.set(dialog, handleKeydown);
        dialog.addEventListener("keydown", handleKeydown);
    }

    if (!dialog.open) {
        focusOrigins.set(dialog, document.activeElement);
        dialog.showModal();
    }

    if (initialFocus?.isConnected) initialFocus.focus();
    else dialog.focus();
}

export function closeDialog(dialog) {
    if (dialog.open) {
        dialog.close();
    }
}

export function disposeDialog(dialog) {
    const handleCancel = cancelHandlers.get(dialog);
    if (handleCancel) {
        dialog.removeEventListener("cancel", handleCancel);
        cancelHandlers.delete(dialog);
    }

    const handleClose = closeHandlers.get(dialog);
    if (handleClose) {
        dialog.removeEventListener("close", handleClose);
        closeHandlers.delete(dialog);
    }

    const handleKeydown = keydownHandlers.get(dialog);
    if (handleKeydown) {
        dialog.removeEventListener("keydown", handleKeydown);
        keydownHandlers.delete(dialog);
    }

    focusOrigins.delete(dialog);
}
