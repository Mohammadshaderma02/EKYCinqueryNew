using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Session
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string status { get; set; } = null!;

    public bool EmailConfirmed { get; set; }

    public bool PhoneConfirmed { get; set; }

    public bool PolicyAccepted { get; set; }

    public string? OtpPhone { get; set; }

    public string? OtpEmail { get; set; }

    public OcrData.Root? OcrData { get; set; }

    public string? AdaptedOcrData { get; set; }

    public string? FaceMatchThreshold { get; set; }

    public string? FaceTemplates { get; set; }

    public string? LivenessThreshold { get; set; }

    public string? LivenessOutput { get; set; }

    public int TokenId { get; set; }

    public bool DocumentAccepted { get; set; }

    public bool LivenessAccepted { get; set; }

    public bool OcrDataAccepted { get; set; }

    public string? RejectMotivation { get; set; }

    public string? SocketId { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public string? DossierShareUid { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public int? ArchiveAttempts { get; set; }

    public int? IdentificationAttempts { get; set; }

    public int? CheckArchiveAttempts { get; set; }

    public bool Tsa { get; set; }

    public DateTime? TsaTime { get; set; }

    public virtual ICollection<Audit> Audits { get; set; } = new List<Audit>();

    public virtual ICollection<ContactRequest> ContactRequests { get; set; } = new List<ContactRequest>();

    public virtual SessionWorkflow? SessionWorkflow { get; set; }

    public virtual Token Token { get; set; } = null!;

    public virtual User? User { get; set; }

    public virtual Validation? Validation { get; set; }
}
