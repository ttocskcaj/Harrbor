using Harrbor.Api.Connections;
using Harrbor.Api.Dashboard;
using Harrbor.Api.Jobs;
using Harrbor.Api.Releases;

namespace Harrbor.Api;

/// <summary>
/// Single registration point for the HTTP API. New resource groups add one
/// line here and live under their own folder in <c>Api/</c>.
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapConnectionEndpoints();
        app.MapJobEndpoints();
        app.MapReleaseEndpoints();
        app.MapDashboardEndpoints();
        return app;
    }
}
