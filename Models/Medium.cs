using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Medium
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? Code { get; set; }

    public string Filename { get; set; } = null!;

    public string? Path { get; set; }

    public string? Url { get; set; }

    public string? Side { get; set; }

    public string? Extension { get; set; }

    public int? TemplateId { get; set; }

    public string? Country { get; set; }

    public bool MainDocument { get; set; }

    public int? UserId { get; set; }

    public int? TokenId { get; set; }

    public int? UserDocumentDataId { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public string? DossierUid { get; set; }

    public string? ExternalUid { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public bool? Archived { get; set; }

    public string? ArchiveId { get; set; }

    public bool? SentToArchive { get; set; }

    //public virtual Token? Token { get; set; }

    //public virtual User? User { get; set; }

    //public virtual UserDocumentDatum? UserDocumentData { get; set; }
}
