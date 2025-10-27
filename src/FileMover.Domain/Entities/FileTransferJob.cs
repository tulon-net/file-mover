namespace FileMover.Domain.Entities;

/// <summary>
/// Represents an active file transfer job
/// </summary>
public class FileTransferJob
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public FileTransferStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

public enum FileTransferStatus
{
    Pending,
    Generating,
    Generated,
    Sending,
    Sent,
    Failed,
    Cancelled
}
