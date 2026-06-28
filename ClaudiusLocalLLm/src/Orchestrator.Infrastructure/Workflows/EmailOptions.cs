namespace Orchestrator.Infrastructure.Workflows;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = "";
}
