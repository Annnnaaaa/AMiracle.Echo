using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AMiracle.Echo.Server.Endpoints;

internal static class StaticAssetEndpoints
{
    public static void MapStaticAssets(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/echo/widget.js", async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "application/javascript; charset=utf-8";
            ctx.Response.Headers["Cache-Control"] = "public, max-age=300";
            await using var stream = ReadResource("AMiracle.Echo.Server.Resources.widget.js");
            if (stream is null) { ctx.Response.StatusCode = 404; return; }
            await stream.CopyToAsync(ctx.Response.Body);
        });

        routes.MapGet("/echo/admin", ServeAdmin);

        static async Task ServeAdmin(HttpContext ctx)
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await using var stream = ReadResource("AMiracle.Echo.Server.Resources.admin.html");
            if (stream is null) { ctx.Response.StatusCode = 404; return; }
            await stream.CopyToAsync(ctx.Response.Body);
        }
    }

    private static Stream? ReadResource(string name)
        => typeof(StaticAssetEndpoints).Assembly.GetManifestResourceStream(name);
}
