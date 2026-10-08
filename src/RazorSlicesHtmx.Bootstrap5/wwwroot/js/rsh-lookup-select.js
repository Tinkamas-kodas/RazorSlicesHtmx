(() => {
  class RshLookupSelect extends HTMLElement {
    connectedCallback() {
      if (this._wired) {
        return;
      }

      this._wired = true;
      this.hiddenInput = this.querySelector(`#${this.dataset.hiddenId}`);
      this.searchInput = this.querySelector(`#${this.dataset.searchId}`);
      this.results = this.querySelector(".rsh-lookup-results");
      this.clearButton = this.querySelector(`#${this.dataset.clearId}`);
      this.textHiddenInput = this.querySelector(`#${this.dataset.textHiddenId}`);
      this.resolvePath = this.dataset.resolvePath ?? "";
      this.componentDisabled = this.dataset.componentDisabled === "true" || this.searchInput?.disabled === true;

      if (!this.hiddenInput || !this.searchInput || !this.results || !this.clearButton || !this.textHiddenInput) {
        return;
      }

      this.syncHiddenState();
      this.results.classList.add("d-none");

      if (this.textHiddenInput.value && !this.searchInput.value) {
        this.searchInput.value = this.textHiddenInput.value;
      }

      this.observer = new MutationObserver(() => {
        this.toggleResults(this.results.childElementCount > 0);
      });
      this.observer.observe(this.results, { childList: true });

      this.results.addEventListener("click", event => {
        const option = event.target.closest("[data-rsh-lookup-value]");
        if (!option) {
          return;
        }

        event.preventDefault();
        event.stopPropagation();

        const value = option.dataset.rshLookupValue ?? "";
        const text = option.dataset.rshLookupText ?? option.textContent?.trim() ?? "";

        this.hiddenInput.value = value;
        this.textHiddenInput.value = text;
        this.searchInput.value = text;
        this.syncHiddenState();
        this.hideResults();

        this.hiddenInput.dispatchEvent(new Event("change", { bubbles: true }));
        this.dispatchEvent(new CustomEvent("rsh:lookup-selected", {
          bubbles: true,
          detail: { value, text }
        }));
      });

      this.clearButton.addEventListener("click", () => {
        this.hiddenInput.value = "";
        this.textHiddenInput.value = "";
        this.searchInput.value = "";
        this.syncHiddenState();
        this.hideResults();
        this.searchInput.focus();
      });

      this.searchInput.addEventListener("keydown", event => {
        if (event.key === "Escape") {
          this.hideResults();
        }
      });

      this.searchInput.addEventListener("focus", () => {
        this.toggleResults(this.results.childElementCount > 0);
      });

      document.addEventListener("click", event => {
        if (!this.contains(event.target)) {
          this.hideResults();
        }
      });

      document.body.addEventListener("htmx:beforeRequest", event => {
        if (event.detail?.target === this.results) {
          this.searchInput.classList.add("is-loading");
        }
      });

      document.body.addEventListener("htmx:afterRequest", event => {
        if (event.detail?.target === this.results) {
          this.searchInput.classList.remove("is-loading");
        }
      });

      this.resolveSelectedLabel();

    }

    disconnectedCallback() {
      this.observer?.disconnect();
      this.observer = null;
    }

    hideResults() {
      this.results.innerHTML = "";
      this.results.classList.add("d-none");
    }

    toggleResults(show) {
      this.results.classList.toggle("d-none", !show);
    }

    async resolveSelectedLabel() {
      const value = this.hiddenInput.value?.trim();
      const hasText = (this.textHiddenInput.value?.trim() ?? "") !== "" || (this.searchInput.value?.trim() ?? "") !== "";
      if (!value || hasText || !this.resolvePath || this.dataset.resolveInFlight === "1") {
        return;
      }

      this.dataset.resolveInFlight = "1";

      try {
        const url = new URL(this.resolvePath, window.location.origin);
        url.searchParams.set("value", value);

        const response = await fetch(url.toString(), {
          method: "GET",
          credentials: "same-origin",
          headers: { accept: "application/json" }
        });

        if (!response.ok || !document.body.contains(this)) {
          return;
        }

        const item = await response.json();
        if (!item) {
          return;
        }

        const resolvedText = item.text ?? "";
        const resolvedValue = item.value ?? value;

        this.hiddenInput.value = resolvedValue;
        this.textHiddenInput.value = resolvedText;
        this.searchInput.value = resolvedText;
        this.syncHiddenState();
      } finally {
        delete this.dataset.resolveInFlight;
      }
    }

    syncHiddenState() {
      const hasValue = (this.hiddenInput.value?.trim() ?? "") !== "";
      const shouldDisable = this.componentDisabled || !hasValue;

      this.hiddenInput.disabled = shouldDisable;
      this.textHiddenInput.disabled = shouldDisable;
    }
  }

  if (!customElements.get("rsh-lookup-select")) {
    customElements.define("rsh-lookup-select", RshLookupSelect);
  }
})();
