(() => {
  const preserveFlagKey = "tt-week-preserve-next";
  const scrollTopKey = "tt-week-scroll-top";
  const detailTargetId = document.body.dataset.detailTargetId || "details-pane";

  const applyTimeTrackingScrollBehavior = () => {
    const wrap = document.querySelector(".tt-week-grid-wrap");
    if (!(wrap instanceof HTMLElement)) {
      return;
    }

    if (wrap.dataset.ttScrollReady === "1") {
      return;
    }

    wrap.dataset.ttScrollReady = "1";

    for (const button of document.querySelectorAll(".tt-week-nav-btn")) {
      if (!(button instanceof HTMLElement) || button.dataset.ttScrollBound === "1") {
        continue;
      }

      button.dataset.ttScrollBound = "1";
      button.addEventListener("click", () => {
        sessionStorage.setItem(preserveFlagKey, "1");
        sessionStorage.setItem(scrollTopKey, String(wrap.scrollTop));
      });
    }

    const shouldPreserve = sessionStorage.getItem(preserveFlagKey) === "1";
    const savedTop = Number(sessionStorage.getItem(scrollTopKey));
    const eightCell = wrap.querySelector('.tt-grid-time-cell[data-time="08:00"]');

    const targetTop = shouldPreserve && Number.isFinite(savedTop)
      ? Math.max(0, savedTop)
      : (eightCell instanceof HTMLElement ? Math.max(0, eightCell.offsetTop - 34) : 0);

    const apply = () => {
      wrap.scrollTop = targetTop;
    };

    requestAnimationFrame(apply);
    setTimeout(apply, 120);

    sessionStorage.removeItem(preserveFlagKey);
  };

  const detailsPane = document.getElementById(detailTargetId);
  if (detailsPane instanceof HTMLElement) {
    const observer = new MutationObserver(() => {
      applyTimeTrackingScrollBehavior();
    });

    observer.observe(detailsPane, { childList: true, subtree: true });
    document.addEventListener("htmx:after:swap", applyTimeTrackingScrollBehavior);
  }

  document.addEventListener("DOMContentLoaded", () => {
    applyTimeTrackingScrollBehavior();
    setTimeout(applyTimeTrackingScrollBehavior, 180);
  });
})();
