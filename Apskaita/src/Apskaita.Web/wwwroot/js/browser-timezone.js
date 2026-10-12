// Reports the browser's UTC offset so server-rendered calendar events display in the visitor's
// timezone. Value is minutes ahead of UTC (Vilnius summer = 180 = -getTimezoneOffset()); refreshed
// on every page load. Loaded in <head> so the cookie exists before any htmx request is issued.
(function () {
    try {
        var minutes = -new Date().getTimezoneOffset();
        document.cookie = "tz-offset=" + minutes + ";path=/;max-age=3456000;SameSite=Lax";
    } catch (e) { }
})();
