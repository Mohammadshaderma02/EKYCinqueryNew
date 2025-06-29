using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class SessionComponent
{
    public int Id { get; set; }

    public string Uuid { get; set; } = null!;

    public string Uid { get; set; } = null!;

    public int Index { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int SessionStepId { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual SessionStep SessionStep { get; set; } = null!;
}
