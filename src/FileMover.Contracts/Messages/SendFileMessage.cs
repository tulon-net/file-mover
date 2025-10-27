namespace FileMover.Contracts.Messages;

/// <summary>
/// Message sent to SendFile queue to initiate file sending to FTP
/// </summary>
public record SendFileMessage
{
    public Guid JobId { get; init; }
    public Guid ScheduleId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string FtpHost { get; init; } = string.Empty;
    public string FtpUsername { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public string SecretVaultKey { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
