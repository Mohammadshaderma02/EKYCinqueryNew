using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class SessionWorkflow
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual Session Session { get; set; } = null!;

    public virtual ICollection<SessionStep> SessionSteps { get; set; } = new List<SessionStep>();
}
