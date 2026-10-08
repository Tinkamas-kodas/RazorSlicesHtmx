(() => {
  const token = document
    .querySelector('meta[name="request-verification-token"]')
    ?.getAttribute("content");

  if (!token) {
    return;
  }

  document.addEventListener("htmx:config:request", event => {
    const detail = event.detail ?? {};
    const headers = detail.headers ?? detail.requestConfig?.headers ?? detail.fetchOptions?.headers;

    if (headers) {
      headers.RequestVerificationToken = token;
    }
  });
})();
