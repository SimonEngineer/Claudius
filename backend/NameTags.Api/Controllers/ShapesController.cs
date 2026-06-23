using Microsoft.AspNetCore.Mvc;
using NameTags.Core.Svg;

namespace NameTags.Api.Controllers;

[ApiController]
[Route("api/shapes")]
public class ShapesController : ControllerBase
{
    /// <summary>Validates an uploaded SVG can be parsed into a usable outline; returns the bytes back for the client to resubmit with a generate/save request.</summary>
    [HttpPost("svg-upload")]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> UploadSvg(IFormFile file)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var bytes = stream.ToArray();

        try
        {
            // Validate only -- parsing here just proves the SVG is usable before the client persists it.
            SvgOutlineParser.Parse(bytes, targetWidthMm: 70, targetHeightMm: 30);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new { bytes = Convert.ToBase64String(bytes) });
    }
}
