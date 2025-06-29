using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Link
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Scope { get; set; } = null!;

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public int? TokenId { get; set; }

    public string? Content { get; set; }

    public virtual Token? Token { get; set; }
}
