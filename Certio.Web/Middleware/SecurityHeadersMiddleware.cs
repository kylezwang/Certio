namespace Certio.Web.Middleware
{
    /// <summary>
    /// Adds baseline security response headers. Runs first in the pipeline so the headers also cover
    /// static files and error responses, not just MVC output.
    ///
    /// Deliberately does not set Content-Security-Policy. The application renders inline scripts in most
    /// views, so any CSP that did not include 'unsafe-inline' would break the UI, and a CSP that includes
    /// 'unsafe-inline' provides no XSS protection while creating the impression of it. Extracting the
    /// inline scripts is a prerequisite and is tracked separately.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Stop browsers from MIME-sniffing a response away from its declared Content-Type, which is
            // what turns an uploaded file served as text/plain into executable script.
            headers["X-Content-Type-Options"] = "nosniff";

            // Clickjacking protection. SAMEORIGIN rather than DENY: it blocks the actual attack (a third
            // party framing an authenticated Certio page) while staying safe for same-origin framing.
            // Certio only ever frames external content - Google Maps embeds and the Office Online/WOPI
            // document viewer - so this does not affect the document viewer.
            headers["X-Frame-Options"] = "SAMEORIGIN";

            // Send the full URL only to same-origin destinations; send origin-only cross-origin over
            // HTTPS and nothing when downgrading. Without this, matter and organization IDs in paths leak
            // to third parties through the Referer header on outbound links and embeds.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Blocks Adobe cross-domain policy files from granting access to this origin.
            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            await _next(context);
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}
