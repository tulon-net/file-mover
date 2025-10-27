namespace FileMover.Domain.Entities;

/// <summary>
/// Represents a schedule for file generation and sending operations
/// </summary>
public class Schedule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string FtpHost { get; set; } = string.Empty;
    public string FtpUsername { get; set; } = string.Empty;
    public string SecretVaultKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
}
