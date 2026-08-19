namespace EmployeeTaskTracker.Api.Notifications;

/// <summary>
/// Binds to the "Email" section of configuration.
///
/// The username and password are deliberately left empty in the committed
/// appsettings.json. Supply them with dotnet user-secrets or environment
/// variables instead - see the Email notifications section of the README. A
/// mailbox password does not belong in source control.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Master switch. When false, or when <see cref="SmtpHost"/> is blank, the
    /// application logs each notification instead of sending it. That keeps a
    /// fresh clone working for anyone who has not configured a mailbox.
    /// </summary>
    public bool Enabled { get; set; }

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// True for implicit TLS on port 465. Leave false for port 587, which
    /// upgrades to TLS with STARTTLS - the usual choice, and what Gmail wants.
    /// </summary>
    public bool UseImplicitTls { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Employee Task Tracker";

    /// <summary>Seconds to wait on the SMTP conversation before giving up.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// True only when there is enough configuration to attempt a send. Checked
    /// rather than assumed, so a half-filled section degrades to logging instead
    /// of throwing at runtime.
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && !string.IsNullOrWhiteSpace(FromAddress);
}
