document.querySelectorAll("[data-password-toggle]").forEach((toggleButton) => {
    toggleButton.addEventListener("click", () => {
        const fieldContainer = toggleButton.closest(".password-field");
        const input = fieldContainer?.querySelector("input");

        if (!input) {
            return;
        }

        const isPassword = input.type === "password";
        input.type = isPassword ? "text" : "password";
        toggleButton.textContent = isPassword ? "Hide" : "Show";
        toggleButton.setAttribute("aria-label", isPassword ? "Hide password" : "Show password");
    });
});
