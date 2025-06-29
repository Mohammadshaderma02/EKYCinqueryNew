using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class PrismaMigration1
{
    public string Id { get; set; } = null!;

    public string Checksum { get; set; } = null!;

    public DateTime? FinishedAt { get; set; }

    public string MigrationName { get; set; } = null!;

    public string? Logs { get; set; }

    public DateTime? RolledBackAt { get; set; }

    public DateTime StartedAt { get; set; }

    public int AppliedStepsCount { get; set; }
}
