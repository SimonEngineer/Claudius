using Cronos;
using Microsoft.AspNetCore.Mvc;

namespace Weaver.Api.Controllers;

public record CronPreviewRequest(string Expression);

public record CronPreviewResponse(bool Valid, string? Error, List<DateTimeOffset> NextOccurrences);

/// <summary>Validates a cron expression and previews its next occurrences, so the UI can show
/// "this will run at ..." live while the user types.</summary>
[ApiController]
[Route("api/cron")]
public class CronController : ControllerBase
{
    [HttpPost("preview")]
    public ActionResult<CronPreviewResponse> Preview(CronPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return new CronPreviewResponse(false, "Expression is empty.", new List<DateTimeOffset>());
        }

        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(request.Expression.Trim());
        }
        catch (CronFormatException ex)
        {
            return new CronPreviewResponse(false, ex.Message, new List<DateTimeOffset>());
        }

        var occurrences = new List<DateTimeOffset>();
        var from = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            var next = cron.GetNextOccurrence(from, inclusive: false);
            if (next is null)
            {
                break;
            }

            occurrences.Add(new DateTimeOffset(next.Value, TimeSpan.Zero));
            from = next.Value;
        }

        return new CronPreviewResponse(true, null, occurrences);
    }
}
