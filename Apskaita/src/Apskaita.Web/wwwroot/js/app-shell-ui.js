(() => {
  const storageKey = "sidebarCollapsed";
  const dialogHostId = document.body.dataset.dialogHostId || "dialog-host";
  const toastHostId = document.body.dataset.toastHostId || "toast-host";
  const toastDelayRaw = Number(document.body.dataset.toastDelayMs);
  const toastDelay = Number.isFinite(toastDelayRaw) ? toastDelayRaw : 2600;

  const setToggleAria = () => {
    const toggle = document.getElementById("side-nav-toggle");
    if (!toggle) {
      return;
    }

    const collapsed = document.body.classList.contains("sidebar-collapsed");
    toggle.setAttribute("aria-expanded", (!collapsed).toString());
  };

  const applySidebarState = () => {
    const collapsed = localStorage.getItem(storageKey) === "1";
    document.body.classList.toggle("sidebar-collapsed", collapsed);
    setToggleAria();
  };

  document.addEventListener("click", event => {
    const toggle = event.target instanceof Element ? event.target.closest("#side-nav-toggle") : null;
    if (!toggle) {
      return;
    }

    const collapsed = !document.body.classList.contains("sidebar-collapsed");
    document.body.classList.toggle("sidebar-collapsed", collapsed);
    localStorage.setItem(storageKey, collapsed ? "1" : "0");
    setToggleAria();
  });

  // Calendar side panel: expand/collapse via the vertical "Calendar" label. State is intentionally
  // not persisted — the panel re-renders collapsed on every grid refresh.
  document.addEventListener("click", event => {
    const toggle = event.target instanceof Element ? event.target.closest(".tt-calendar-toggle") : null;
    if (!toggle) {
      return;
    }

    const panel = toggle.closest(".tt-calendar-panel");
    if (!panel) {
      return;
    }

    const expanded = panel.classList.toggle("is-expanded");
    toggle.setAttribute("aria-expanded", expanded.toString());
  });

  const clearModalArtifacts = () => {
    document.body.classList.remove("modal-open");
    document.body.style.removeProperty("padding-right");
    document.querySelectorAll(".modal-backdrop").forEach(backdrop => backdrop.remove());
  };

  const refreshDialogHost = () => {
    const host = document.getElementById(dialogHostId);
    if (!host) {
      return;
    }

    const modal = host.querySelector(".modal");
    if (!modal) {
      clearModalArtifacts();
      return;
    }

    clearModalArtifacts();
    modal.addEventListener("hidden.bs.modal", () => {
      host.innerHTML = "";
      clearModalArtifacts();
    }, { once: true });

    bootstrap.Modal.getOrCreateInstance(modal).show();
  };

  const refreshToastHost = () => {
    const host = document.getElementById(toastHostId);
    if (!host) {
      return;
    }

    host.querySelectorAll(".toast").forEach(toast => {
      bootstrap.Toast.getOrCreateInstance(toast, { delay: toastDelay }).show();
    });
  };

  const showToastFromTrigger = event => {
    const { title = "", message = "", tone = "info" } = event.detail ?? {};
    const host = document.getElementById(toastHostId);
    if (!host) {
      return;
    }

    const bgClass = tone === "error" ? "danger" : tone;
    const toast = document.createElement("div");
    toast.setAttribute("role", "alert");
    toast.setAttribute("aria-live", "assertive");
    toast.setAttribute("aria-atomic", "true");
    toast.className = `toast align-items-center text-bg-${bgClass} border-0 shadow`;

    const bodyDiv = document.createElement("div");
    bodyDiv.className = "d-flex";

    const textDiv = document.createElement("div");
    textDiv.className = "toast-body";

    const strong = document.createElement("strong");
    strong.className = "me-2";
    strong.textContent = title;

    const span = document.createElement("span");
    span.textContent = message;

    textDiv.append(strong, span);

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "btn-close btn-close-white me-2 m-auto";
    closeButton.setAttribute("data-bs-dismiss", "toast");
    closeButton.setAttribute("aria-label", "Close");

    bodyDiv.append(textDiv, closeButton);
    toast.appendChild(bodyDiv);
    host.appendChild(toast);

    bootstrap.Toast.getOrCreateInstance(toast, { delay: toastDelay }).show();
    toast.addEventListener("hidden.bs.toast", () => toast.remove(), { once: true });
  };

  const refresh = event => {
    const target = event?.detail?.target;
    if (!target || target.id === dialogHostId) {
      refreshDialogHost();
    }

    if (!target || target.id === toastHostId) {
      refreshToastHost();
    }

    if (!target || target.id === "side-nav") {
      applySidebarState();
    }
  };

  document.addEventListener("DOMContentLoaded", () => {
    applySidebarState();
    refresh();
  });

  document.body.addEventListener("htmx:after:swap", refresh);
  document.body.addEventListener("htmx:after:oob-swap", refresh);
  document.addEventListener("show-dialog", refreshDialogHost);
  document.addEventListener("dialog:clear", () => {
    const host = document.getElementById(dialogHostId);
    if (host) {
      host.innerHTML = "";
    }

    clearModalArtifacts();
  });
  document.addEventListener("show-toast", showToastFromTrigger);
})();
