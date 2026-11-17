(() => {
  // Subtle entrance for number balls already handled in CSS.
  // Keep a tiny progressive enhancement hook for confirm dialogs.
  document.querySelectorAll("[data-confirm]").forEach((el) => {
    el.addEventListener("click", (e) => {
      const msg = el.getAttribute("data-confirm");
      if (msg && !window.confirm(msg)) {
        e.preventDefault();
      }
    });
  });
})();
