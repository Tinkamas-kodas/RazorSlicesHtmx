(() => {
  if (!window.htmx?.config) {
    return;
  }

  window.htmx.config.logAll = true;

  const describeElement = element => {
    if (!(element instanceof Element)) {
      return "(none)";
    }

    const id = element.id ? `#${element.id}` : "";
    return `${element.tagName.toLowerCase()}${id}`;
  };

  const logEvent = event => {
    const detail = event.detail ?? {};
    const source = detail.elt ?? detail.ctx?.sourceElement ?? null;
    const target = detail.target ?? detail.ctx?.target ?? event.target;
    const status = detail.ctx?.response?.status ?? null;
    const triggerHeader = detail.ctx?.hx?.trigger ?? null;

    console.log("[HTMX DEBUG]", event.type, {
      source: describeElement(source),
      target: describeElement(target),
      status,
      triggerHeader,
      detail
    });
  };

  const eventsToLog = [
    "htmx:config:request",
    "htmx:before:request",
    "htmx:before:response",
    "htmx:after:request",
    "htmx:before:swap",
    "htmx:after:swap",
    "htmx:after:settle",
    "htmx:response:error",
    "htmx:error",
    "clients-list-refresh"
  ];

  for (const eventName of eventsToLog) {
    document.addEventListener(eventName, logEvent, true);
  }

  console.info("[HTMX DEBUG] Enabled globally in layout.");
})();
