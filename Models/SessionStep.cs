using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class SessionStep
{
    public int Id { get; set; }

    public string Uuid { get; set; } = null!;

    public string Uid { get; set; } = null!;

    public int Index { get; set; }

    public string Route { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string Icon { get; set; } = null!;

    public bool? Resume { get; set; }

    public bool Completed { get; set; }

    public bool Disabled { get; set; }

    public bool Review { get; set; }

    public bool Skip { get; set; }

    public bool Active { get; set; }

    public int SessionWorkflowId { get; set; }

    public string? Dependencies { get; set; }

    public string? PostActions { get; set; }

    public string? PreActions { get; set; }

    public string? BodyActions { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual ICollection<SessionComponent> SessionComponents { get; set; } = new List<SessionComponent>();

    public virtual ICollection<SessionSection> SessionSections { get; set; } = new List<SessionSection>();

    public virtual SessionWorkflow SessionWorkflow { get; set; } = null!;
}

public class SessionStepNoNavigations
{
    public int Id { get; set; }

    public string Uuid { get; set; } = null!;

    public string Uid { get; set; } = null!;

    public int Index { get; set; }

    public string Route { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string Icon { get; set; } = null!;

    public bool? Resume { get; set; }

    public bool Completed { get; set; }

    public bool Disabled { get; set; }

    public bool Review { get; set; }

    public bool Skip { get; set; }

    public bool Active { get; set; }

    public int SessionWorkflowId { get; set; }

    public string? Dependencies { get; set; }

    public string? PostActions { get; set; }

    public string? PreActions { get; set; }

    public string? BodyActions { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }
}