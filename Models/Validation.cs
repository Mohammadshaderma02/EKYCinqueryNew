using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Validation
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Token { get; set; } = null!;

    public int SessionId { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual Session Session { get; set; } = null!;
}
