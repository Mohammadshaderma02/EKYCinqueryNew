using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Mrzblacklist
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Mrz { get; set; } = null!;

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public string? SessionUid { get; set; }
}
