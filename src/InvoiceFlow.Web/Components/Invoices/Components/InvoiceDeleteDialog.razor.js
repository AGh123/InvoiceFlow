const cancelHandlers = new WeakMap();
const focusOrigins = new WeakMap();

export function showDialog(dialog, initialFocus) {
    if (!cancelHandlers.has(dialog)) {
        const handleCancel = event => {
            if (dialog.dataset.busy === "true") {
                event.preventDefault();
            }
        };

        cancelHandlers.set(dialog, handleCancel);
        dialog.addEventListener("cancel", handleCancel);
    }

    if (!dialog.open) {
        focusOrigins.set(dialog, document.activeElement);
        dialog.showModal();
    }

    initialFocus.focus();
}

export function closeDialog(dialog) {
    if (dialog.open) {
        const focusOrigin = focusOrigins.get(dialog);
        dialog.close();

        queueMicrotask(() => {
            const fallback = document.querySelector(
                '.row-actions summary, .new-invoice-button, .empty-state a, .brand');
            const focusTarget = focusOrigin?.isConnected ? focusOrigin : fallback;
            focusTarget?.focus();
        });
    }
}

export function disposeDialog(dialog) {
    const handleCancel = cancelHandlers.get(dialog);
    if (handleCancel) {
        dialog.removeEventListener("cancel", handleCancel);
        cancelHandlers.delete(dialog);
    }

    focusOrigins.delete(dialog);
}
