(() => {
  const token = document
    .querySelector('meta[name="request-verification-token"]')
    ?.getAttribute("content");

  if (!token) {
    return;
  }

  document.addEventListener("htmx:config:request", event => {
    const detail = event.detail ?? {};
    // htmx 4 exposes headers via ctx.request.headers; htmx 1/2 shapes kept as fallback.
    const headers = detail.ctx?.request?.headers ?? detail.headers ?? detail.requestConfig?.headers ?? detail.fetchOptions?.headers;

    if (headers) {
      headers.RequestVerificationToken = token;
    }
  });
})();
