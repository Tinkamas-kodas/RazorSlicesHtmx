// Long-press drag for time-tracking entries.
// Interaction: press and hold a time entry (left button) to pick it up, move the
// pointer to a target slot, then left-click to drop (complete) or right-click to
// cancel. Escape also cancels. A short click still opens the edit dialog, so the
// hold threshold distinguishes "edit" from "drag".
(() => {
  const GRID = "#time-tracking-grid";
  const SLOT = ".tt-grid-slot-cell";
  const CARD = ".tt-entry-card";
  const SINK = "#tt-drag-sink";

  const HOLD_MS = 450;
  const MOVE_CANCEL_PX = 8;

  let state = null; // { id, card, ghost, target, height }
  let suppressClick = false;
  let holdTimer = null;
  let pressStart = null; // { x, y, card }

  const getMoveBase = () => {
    const grid = document.querySelector(GRID);
    return grid?.dataset?.moveUrl || null;
  };

  const createGhost = (card) => {
    const ghost = document.createElement("div");
    ghost.className = "tt-entry-ghost";

    const time = card.querySelector(".tt-entry-time");
    const desc = card.querySelector(".tt-entry-description");
    const label = document.createElement("span");
    label.className = "tt-entry-ghost-label";
    label.textContent =
      (time ? time.textContent.trim() : "") +
      (desc && desc.textContent.trim() ? " \u00b7 " + desc.textContent.trim() : "");
    ghost.appendChild(label);

    const height = card.getBoundingClientRect().height;
    ghost.style.height = Math.max(20, height) + "px";
    document.body.appendChild(ghost);
    return ghost;
  };

  const clearTarget = () => {
    if (state?.target) {
      state.target.classList.remove("tt-slot-drop-target");
    }
    if (state) {
      state.target = null;
    }
  };

  const updateGhost = (x, y) => {
    const ghost = state?.ghost;
    if (!ghost) {
      return;
    }

    const hit = document.elementFromPoint(x, y);
    const cell = hit instanceof Element ? hit.closest(SLOT) : null;

    clearTarget();

    if (cell instanceof HTMLElement) {
      state.target = cell;
      cell.classList.add("tt-slot-drop-target");
      const rect = cell.getBoundingClientRect();
      ghost.classList.remove("is-invalid");
      ghost.style.display = "flex";
      ghost.style.top = rect.top + "px";
      ghost.style.left = rect.left + "px";
      ghost.style.width = rect.width + "px";
      ghost.style.height = state.height + "px";
    } else {
      ghost.classList.add("is-invalid");
      ghost.style.display = "flex";
      ghost.style.width = "140px";
      ghost.style.height = state.height + "px";
      // Keep the cursor at the top edge of the icon (centered under the pointer),
      // clamped within the viewport so the ghost is never offset away from the cursor.
      var ghostWidth = 140;
      var ghostTop = Math.max(0, Math.min(y, window.innerHeight - state.height - 4));
      var ghostLeft = Math.max(0, Math.min(x - ghostWidth / 2, window.innerWidth - ghostWidth - 4));
      ghost.style.top = ghostTop + "px";
      ghost.style.left = ghostLeft + "px";
    }
  };

  const destroyGhost = () => {
    if (state?.ghost) {
      state.ghost.remove();
    }
    clearTarget();
  };

  const beginDrag = (card) => {
    if (!(card instanceof HTMLElement)) {
      return;
    }

    const id = card.dataset.entryId;
    if (!id) {
      return;
    }

    const height = card.getBoundingClientRect().height;
    state = { id, card, ghost: null, target: null, height };
    state.ghost = createGhost(card);
    document.body.classList.add("tt-dragging");
    card.classList.add("is-dragging-source");
  };

  const endDrag = () => {
    document.body.classList.remove("tt-dragging");
    if (state?.card) {
      state.card.classList.remove("is-dragging-source");
    }
    destroyGhost();
    state = null;
    suppressClick = true;
  };

  const cancelDrag = () => {
    if (!state) {
      return;
    }
    endDrag();
  };

  const completeDrag = () => {
    if (!state) {
      return;
    }

    const target = state.target;
    const id = state.id;
    endDrag();

    if (!target) {
      return;
    }

    const date = target.dataset.date;
    const time = target.dataset.time;
    const base = getMoveBase();
    if (!date || !time || !base || !window.htmx) {
      return;
    }

    const url =
      `${base}/${encodeURIComponent(id)}` +
      `?date=${encodeURIComponent(date)}&time=${encodeURIComponent(time)}`;

    const sink = document.querySelector(SINK);
    htmx.ajax("POST", url, {
      target: sink,
      swap: "innerHTML",
      triggeringElement: sink
    });
  };

  const clearHold = () => {
    if (holdTimer) {
      clearTimeout(holdTimer);
      holdTimer = null;
    }
    pressStart = null;
  };

  // Capture-phase mousedown: while dragging, left-click drops and right-click cancels.
  // When idle, a left-press on an entry card starts a hold timer that arms the drag.
  document.addEventListener(
    "mousedown",
    (event) => {
      if (state) {
        if (event.button === 2) {
          event.preventDefault();
          event.stopPropagation();
          cancelDrag();
          return;
        }
        if (event.button === 0) {
          event.preventDefault();
          event.stopPropagation();
          completeDrag();
        }
        return;
      }

      if (event.button !== 0) {
        return;
      }

      const card =
        event.target instanceof Element ? event.target.closest(CARD) : null;
      if (!(card instanceof HTMLElement)) {
        return;
      }

      // Do not preventDefault here: a short click must still open the edit dialog.
      pressStart = { x: event.clientX, y: event.clientY, card };
      holdTimer = setTimeout(() => {
        holdTimer = null;
        beginDrag(card);
        suppressClick = true;
      }, HOLD_MS);
    },
    true
  );

  // Release before the hold elapses: cancel the pending drag so the click can edit.
  document.addEventListener(
    "mouseup",
    () => {
      if (state) {
        return; // drag already armed; drop/cancel happen via later clicks
      }
      clearHold();
    },
    true
  );

  // Suppress the click that results from grab/drop/cancel so htmx (edit/create) does not fire.
  document.addEventListener(
    "click",
    (event) => {
      if (state || suppressClick) {
        event.preventDefault();
        event.stopPropagation();
        suppressClick = false;
      }
    },
    true
  );

  // Right-click should cancel, never open the browser context menu while dragging.
  document.addEventListener(
    "contextmenu",
    (event) => {
      if (state) {
        event.preventDefault();
        cancelDrag();
      }
    },
    true
  );

  document.addEventListener(
    "mousemove",
    (event) => {
      if (state) {
        event.preventDefault();
        updateGhost(event.clientX, event.clientY);
        return;
      }

      // Moving away before the hold completes means this is not a drag.
      if (holdTimer && pressStart) {
        const dx = event.clientX - pressStart.x;
        const dy = event.clientY - pressStart.y;
        if (dx * dx + dy * dy > MOVE_CANCEL_PX * MOVE_CANCEL_PX) {
          clearHold();
        }
      }
    },
    true
  );

  document.addEventListener(
    "keydown",
    (event) => {
      if (state && event.key === "Escape") {
        event.preventDefault();
        cancelDrag();
      }
    },
    true
  );
})();
