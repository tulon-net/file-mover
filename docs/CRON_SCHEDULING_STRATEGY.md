# Cron Scheduling Strategy - Timezone-Aware Execution

## Problem Statement

Users configure schedules with cron expressions expecting execution at their local time (e.g., `0 10 * * *` = daily at 10:00 Europe/Warsaw). However, standard cron libraries interpret expressions in UTC or server timezone, causing execution time drift.

**Example of the problem**:
- User sets: `0 10 * * *` expecting 10:00 Warsaw time
- Server interprets in UTC
- Actual execution: 08:00 Warsaw time (winter) or 09:00 (summer, due to DST)

## Solution Architecture

### Core Principles
1. **Store user intent explicitly**: Save both cron expression AND target timezone in database.
2. **Validate in user timezone**: Parse and validate cron against target timezone rules.
3. **Schedule in server timezone**: Convert next execution time to server (or UTC) for job scheduler.
4. **Display in user timezone**: Show next run time in GUI using stored timezone.

### Recommended Libraries

**Primary: Cronos + NodaTime**
- **Cronos**: Modern, lightweight cron parser with timezone support (.NET-native, no dependencies)
- **NodaTime**: Robust timezone handling (avoids .NET `TimeZoneInfo` pitfalls with historical data)

```csharp
// Install packages
dotnet add package Cronos
dotnet add package NodaTime
```

**Alternative: Quartz.NET** (if need advanced scheduling features like persistence, clustering)
```csharp
dotnet add package Quartz
dotnet add package Quartz.Serialization.Json
```

### Implementation Strategy

#### 1. Domain Model (Schedule Entity)

```csharp
public class Schedule
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    
    /// <summary>
    /// Cron expression in standard format (e.g., "0 10 * * *").
    /// Interpreted in the timezone specified by <see cref="TimeZone"/>.
    /// </summary>
    public string CronExpression { get; set; }
    
    /// <summary>
    /// IANA timezone identifier (e.g., "Europe/Warsaw").
    /// This is the timezone the user intends for the cron expression.
    /// </summary>
    public string TimeZone { get; set; } = "Europe/Warsaw"; // Default
    
    /// <summary>
    /// Next scheduled execution time in UTC.
    /// Calculated by converting cron next occurrence from user timezone to UTC.
    /// Updated after each execution.
    /// </summary>
    public DateTime? NextRunUtc { get; set; }
    
    public DateTime? LastRunUtc { get; set; }
    public bool IsActive { get; set; }
    
    // Other properties: SourcePath, DestinationPath, FtpServers, etc.
}
```

#### 2. Validation Service (Domain or API Layer)

```csharp
using Cronos;
using NodaTime;

public interface ICronValidator
{
    ValidationResult Validate(string cronExpression, string timeZone);
    DateTime? GetNextOccurrence(string cronExpression, string timeZone, DateTime? fromUtc = null);
}

public class CronValidator : ICronValidator
{
    public ValidationResult Validate(string cronExpression, string timeZone)
    {
        // 1. Validate cron expression format
        if (!CronExpression.TryParse(cronExpression, CronFormat.Standard, out var parsedCron))
        {
            return ValidationResult.Fail("Invalid cron expression format. Use standard 5-field format: 'minute hour day month dayOfWeek'.");
        }
        
        // 2. Validate timezone
        var tzdb = DateTimeZoneProviders.Tzdb;
        if (tzdb.GetZoneOrNull(timeZone) == null)
        {
            return ValidationResult.Fail($"Invalid timezone: '{timeZone}'. Use IANA timezone identifier (e.g., 'Europe/Warsaw').");
        }
        
        // 3. Check if cron produces at least one occurrence in next 2 years
        var next = GetNextOccurrence(cronExpression, timeZone, DateTime.UtcNow);
        if (next == null || next.Value > DateTime.UtcNow.AddYears(2))
        {
            return ValidationResult.Fail("Cron expression does not produce valid occurrences in reasonable timeframe.");
        }
        
        return ValidationResult.Success();
    }
    
    public DateTime? GetNextOccurrence(string cronExpression, string timeZone, DateTime? fromUtc = null)
    {
        var cron = CronExpression.Parse(cronExpression, CronFormat.Standard);
        var from = fromUtc ?? DateTime.UtcNow;
        
        // Convert UTC to user timezone
        var tzdb = DateTimeZoneProviders.Tzdb;
        var tz = tzdb[timeZone];
        var fromInUserTz = Instant.FromDateTimeUtc(from).InZone(tz).LocalDateTime;
        
        // Get next occurrence in user timezone (Cronos works with DateTimeOffset)
        var fromOffset = new DateTimeOffset(fromInUserTz.ToDateTimeUnspecified(), tz.GetUtcOffset(Instant.FromDateTimeUtc(from)));
        // Note: tz.ToTimeZoneInfo() may fail for IANA timezones without Windows mappings.
        DateTimeOffset? nextInUserTz = null;
        try
        {
            nextInUserTz = cron.GetNextOccurrence(fromOffset, tz.ToTimeZoneInfo());
        }
        catch (NodaTime.TimeZones.DateTimeZoneConversionException)
        {
            // Unsupported timezone conversion; cannot calculate next occurrence.
            return null;
        }
        
        if (nextInUserTz == null)
            return null;
        
        // Convert back to UTC for storage
        var nextZoned = LocalDateTime.FromDateTime(nextInUserTz.Value.DateTime).InZoneLeniently(tz);
        return nextZoned.ToDateTimeUtc();
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
    
    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string error) => new() { IsValid = false, ErrorMessage = error };
}
```

#### 3. Schedule Service (Update NextRunUtc)

```csharp
public class ScheduleService
{
    private readonly ICronValidator _cronValidator;
    private readonly IScheduleRepository _repository;
    
    public async Task<Result> CreateSchedule(CreateScheduleDto dto)
    {
        // Validate cron + timezone
        var validation = _cronValidator.Validate(dto.CronExpression, dto.TimeZone);
        if (!validation.IsValid)
            return Result.Fail(validation.ErrorMessage);
        
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            CronExpression = dto.CronExpression,
            TimeZone = dto.TimeZone,
            IsActive = true
        };
        
        // Calculate first NextRunUtc
        schedule.NextRunUtc = _cronValidator.GetNextOccurrence(
            schedule.CronExpression, 
            schedule.TimeZone, 
            DateTime.UtcNow
        );
        
        await _repository.AddAsync(schedule);
        return Result.Success();
    }
    
    public async Task UpdateNextRun(Guid scheduleId)
    {
        var schedule = await _repository.GetByIdAsync(scheduleId);
        if (schedule == null) return;
        
        schedule.LastRunUtc = DateTime.UtcNow;
        schedule.NextRunUtc = _cronValidator.GetNextOccurrence(
            schedule.CronExpression,
            schedule.TimeZone,
            DateTime.UtcNow
        );
        
        await _repository.UpdateAsync(schedule);
    }
}
```

#### 4. Background Job Scheduler (Hosted Service)

```csharp
public class ScheduleTriggerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleTriggerService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
                var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
                var scheduleService = scope.ServiceProvider.GetRequiredService<IScheduleService>();
                
                var now = DateTime.UtcNow;
                
                // Find schedules due for execution (NextRunUtc <= now)
                var dueSchedules = await repository.GetDueSchedulesAsync(now);
                
                foreach (var schedule in dueSchedules)
                {
                    _logger.LogInformation(
                        "Triggering schedule {ScheduleId} ({Name}). Configured time: {CronExpression} {TimeZone}",
                        schedule.Id, schedule.Name, schedule.CronExpression, schedule.TimeZone
                    );
                    
                    // Publish GenerateFileMessage to RabbitMQ
                    await queueService.PublishAsync(new GenerateFileMessage
                    {
                        ScheduleId = schedule.Id,
                        JobId = Guid.NewGuid(),
                        Timestamp = now
                    });
                    
                    // Update NextRunUtc for next occurrence
                    await scheduleService.UpdateNextRun(schedule.Id);
                }
                
                // Check every 30 seconds (adjust based on required precision)
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in schedule trigger service");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); // Back off on error
            }
        }
    }
}
```

#### 5. API Endpoint (Create/Update Schedule)

```csharp
[ApiController]
[Route("api/schedules")]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleService _scheduleService;
    private readonly ICronValidator _cronValidator;
    
    [HttpPost]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleDto dto)
    {
        var result = await _scheduleService.CreateSchedule(dto);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
    
    [HttpPost("validate-cron")]
    public IActionResult ValidateCron([FromBody] ValidateCronDto dto)
    {
        var validation = _cronValidator.Validate(dto.CronExpression, dto.TimeZone);
        
        if (!validation.IsValid)
            return BadRequest(new { isValid = false, error = validation.ErrorMessage });
        
        // Calculate next 5 occurrences for preview
        var occurrences = new List<DateTime>();
        var currentUtc = DateTime.UtcNow;
        
        for (int i = 0; i < 5; i++)
        {
            var next = _cronValidator.GetNextOccurrence(dto.CronExpression, dto.TimeZone, currentUtc);
            if (next == null) break;
            
            occurrences.Add(next.Value);
            currentUtc = next.Value.AddSeconds(1); // Move past this occurrence
        }
        
        return Ok(new
        {
            isValid = true,
            nextOccurrences = occurrences,
            timeZone = dto.TimeZone,
            cronExpression = dto.CronExpression
        });
    }
}

public class CreateScheduleDto
{
    [Required]
    public string Name { get; set; }
    
    [Required]
    public string CronExpression { get; set; }
    
    [Required]
    public string TimeZone { get; set; } = "Europe/Warsaw";
    
    // Other fields...
}

public class ValidateCronDto
{
    [Required]
    public string CronExpression { get; set; }
    
    [Required]
    public string TimeZone { get; set; }
}
```

#### 6. GUI Integration (Display in User Timezone)

```typescript
// Example: Display next run time in user's configured timezone
import { format, utcToZonedTime } from 'date-fns-tz';

function formatNextRun(nextRunUtc: string, timeZone: string): string {
  const date = new Date(nextRunUtc);
  const zonedDate = utcToZonedTime(date, timeZone);
  return format(zonedDate, 'yyyy-MM-dd HH:mm:ss zzz', { timeZone });
}

// Display: "2025-11-10 10:00:00 CET" (when nextRunUtc is stored as UTC in DB)
```

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void GetNextOccurrence_Daily10AM_Warsaw_ReturnsCorrectUtc()
{
    // Arrange
    var validator = new CronValidator();
    var cron = "0 10 * * *"; // Daily at 10:00
    var timeZone = "Europe/Warsaw";
    var fromUtc = new DateTime(2025, 11, 10, 8, 0, 0, DateTimeKind.Utc); // 09:00 Warsaw (winter time)
    
    // Act
    var nextUtc = validator.GetNextOccurrence(cron, timeZone, fromUtc);
    
    // Assert
    // Next 10:00 Warsaw = 09:00 UTC (CET is UTC+1 in winter)
    Assert.Equal(new DateTime(2025, 11, 10, 9, 0, 0, DateTimeKind.Utc), nextUtc);
}

[Fact]
public void GetNextOccurrence_Daily10AM_Warsaw_Summer_ReturnsCorrectUtc()
{
    // Summer: CEST (UTC+2)
    var validator = new CronValidator();
    var cron = "0 10 * * *";
    var timeZone = "Europe/Warsaw";
    var fromUtc = new DateTime(2025, 7, 10, 7, 0, 0, DateTimeKind.Utc); // 09:00 Warsaw summer time
    
    var nextUtc = validator.GetNextOccurrence(cron, timeZone, fromUtc);
    
    // Next 10:00 Warsaw = 08:00 UTC (CEST is UTC+2 in summer)
    Assert.Equal(new DateTime(2025, 7, 10, 8, 0, 0, DateTimeKind.Utc), nextUtc);
}

[Theory]
[InlineData("0 10 * * *", true)]  // Valid: daily at 10:00
[InlineData("*/15 * * * *", true)] // Valid: every 15 minutes
[InlineData("0 0 31 2 *", true)]   // Valid format but no Feb 31
[InlineData("invalid", false)]     // Invalid format
[InlineData("0 10 * * * *", false)] // 6-field (seconds) not supported in Standard
public void Validate_CronExpression_ReturnsExpectedResult(string cron, bool expectedValid)
{
    var validator = new CronValidator();
    var result = validator.Validate(cron, "Europe/Warsaw");
    Assert.Equal(expectedValid, result.IsValid);
}
```

### Integration Test (Full Flow)

```csharp
[Fact]
public async Task CreateSchedule_WithWarsaw10AM_ExecutesAtCorrectTime()
{
    // Create schedule
    var dto = new CreateScheduleDto
    {
        Name = "Daily Report",
        CronExpression = "0 10 * * *",
        TimeZone = "Europe/Warsaw"
    };
    
    var scheduleId = await _scheduleService.CreateSchedule(dto);
    var schedule = await _repository.GetByIdAsync(scheduleId);
    
    // Verify NextRunUtc is 09:00 UTC (10:00 CET)
    Assert.Equal(9, schedule.NextRunUtc.Value.Hour);
    
    // Simulate time passing to next run
    _timeProvider.SetUtcNow(schedule.NextRunUtc.Value);
    
    // Trigger service should pick it up
    var dueSchedules = await _repository.GetDueSchedulesAsync(_timeProvider.UtcNow);
    Assert.Contains(schedule, dueSchedules);
}
```

---

## Configuration

### appsettings.json

```json
{
  "Scheduling": {
    "DefaultTimeZone": "Europe/Warsaw",
    "PollingIntervalSeconds": 30,
    "MaxConcurrentTriggers": 10
  }
}
```

### Validation Rules

- **Cron format**: Standard 5-field (minute hour day month dayOfWeek)
- **Timezone**: IANA identifier (validate against NodaTime `DateTimeZoneProviders.Tzdb`)
- **Min interval**: Warn if cron executes more frequently than every 5 minutes (risk of queue flooding)
- **Ambiguous times**: During DST transitions, NodaTime's `InZoneLeniently` picks earliest valid time

---

## Benefits of This Approach

1. ✅ **User sets 10:00, job runs at 10:00** in their timezone (Europe/Warsaw)
2. ✅ **DST-aware**: Automatically adjusts for summer/winter time transitions
3. ✅ **Portable**: Works across server timezones (server can run in UTC, users still get correct local times)
4. ✅ **Testable**: Clear separation of concerns; easy to mock time
5. ✅ **GUI-friendly**: Can display "Next run: 2025-11-10 10:00 CET" accurately
6. ✅ **Scalable**: NextRunUtc indexed in DB for efficient queries

---

## Migration from Old System

If migrating from a system with timezone drift:

```csharp
// One-time migration script
public async Task RecalculateAllNextRuns()
{
    var schedules = await _repository.GetAllAsync();
    
    foreach (var schedule in schedules)
    {
        // Assume old system stored cron but no timezone
        schedule.TimeZone = "Europe/Warsaw"; // Set default
        
        // Recalculate NextRunUtc with correct timezone
        schedule.NextRunUtc = _cronValidator.GetNextOccurrence(
            schedule.CronExpression,
            schedule.TimeZone,
            DateTime.UtcNow
        );
        
        await _repository.UpdateAsync(schedule);
    }
}
```

---

## Alternative: Quartz.NET Approach

If using Quartz.NET (more features but heavier):

```csharp
var trigger = TriggerBuilder.Create()
    .WithIdentity($"schedule-{scheduleId}")
    .WithCronSchedule(
        cronExpression,
        x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"))
    )
    .Build();

await scheduler.ScheduleJob(job, trigger);
```

**Drawback**: Quartz requires persistent job store if scaling across multiple API instances; adds complexity.

---

## Recommendation

**Use Cronos + NodaTime** for this project:
- Lightweight, .NET-native
- Explicit control over timezone conversions
- Fits event-driven architecture (just trigger RabbitMQ messages; no persistent scheduler state)
- Easy to test and reason about

Store `CronExpression`, `TimeZone`, and `NextRunUtc` in Schedule entity. Validate at API layer. Background service polls due schedules every 30 seconds.

