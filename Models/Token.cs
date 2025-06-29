using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Token
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Token1 { get; set; } = null!;

    public string UserCreationUid { get; set; } = null!;

    public string UserCreationEmail { get; set; } = null!;

    public bool SendEmail { get; set; }

    public bool SendSms { get; set; }

    public string? FileData { get; set; }

    public string UiStyle { get; set; } = null!;

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public string? Tenant { get; set; }

    public virtual AdditionalDatum? AdditionalDatum { get; set; }

    public virtual ICollection<Audit> Audits { get; set; } = new List<Audit>();

    public virtual ICollection<Link> Links { get; set; } = new List<Link>();

    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();

    public virtual Session? Session { get; set; }

    public virtual TokenDocumentDatum? TokenDocumentDatum { get; set; }

    public virtual TokenUserAddressDatum? TokenUserAddressDatum { get; set; }

    public virtual TokenUserDatum? TokenUserDatum { get; set; }
}
