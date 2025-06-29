using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Audit
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string? Content { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public int? SessionId { get; set; }

    public int? TokenId { get; set; }

    public virtual Session? Session { get; set; }

    public virtual Token? Token { get; set; }
}
