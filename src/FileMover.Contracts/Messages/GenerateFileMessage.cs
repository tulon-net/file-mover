namespace FileMover.Contracts.Messages;

/// <summary>
/// Message sent to GenerateFile queue to initiate file generation
/// </summary>
public record GenerateFileMessage
{
    public Guid JobId { get; init; }
    public Guid ScheduleId { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
