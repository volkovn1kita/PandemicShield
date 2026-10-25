using PandemicShield.Contracts.Events;

namespace PandemicShield.Api.Events
{
    /// <summary>
    /// Після кожного успішного запиту, що змінює дані (POST, PUT, DELETE), публікує подію за шляхом:
    /// /api/threats → сповіщення, /api/variant-markers і /api/dictionary → правила, /api/runs/{id}/… → запуск тощо.
    /// Так жоден ендпоінт не треба пам'ятати «повідомити застосунок» окремо.
    /// </summary>
    public static class ChangeEventsMiddleware
    {
        public static IApplicationBuilder UseChangeEvents(this IApplicationBuilder app) => app.Use(async (context, next) =>
        {
            await next();

            var request = context.Request;
            if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || context.Response.StatusCode >= 400) return;
            var change = ForPath(request.Path.Value);
            if (change == null) return;

            var notifier = context.RequestServices.GetRequiredService<ChangeNotifier>();
            await notifier.NotifyAsync(change);
        });

        public static ChangeEvent? ForPath(string? path)
        {
            if (path == null || !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return null;
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return null;

            return parts[1].ToLowerInvariant() switch
            {
                "analyze" => new ChangeEvent(ChangeTopics.Runs),
                "runs" => new ChangeEvent(ChangeTopics.Runs, parts.Length > 2 && Guid.TryParse(parts[2], out var runId) ? runId : null),
                "threats" => new ChangeEvent(ChangeTopics.Alerts),
                "variant-markers" or "dictionary" => new ChangeEvent(ChangeTopics.Rules),
                "imports" => new ChangeEvent(ChangeTopics.Imports),
                "surveillance" => new ChangeEvent(ChangeTopics.Surveillance),
                _ => null
            };
        }
    }
}
