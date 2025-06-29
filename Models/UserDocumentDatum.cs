using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class UserDocumentDatum
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string? Type { get; set; }

    public string? TypeLabel { get; set; }

    public string? Number { get; set; }

    public string? IssuingCountry { get; set; }

    public string? IcaoDocumentType { get; set; }

    public string? IcaoDocumentFormat { get; set; }

    public string? MrzCode { get; set; }

    public bool Expired { get; set; }

    public bool ValidMrzComposite { get; set; }

    public bool ValidMrzDateOfBirth { get; set; }

    public bool ValidMrzDocumentNumber { get; set; }

    public bool ValidMrzDateOfExpiry { get; set; }

    public bool ValidDocument { get; set; }

    public bool ValidTexture { get; set; }

    public bool ValidColors { get; set; }

    public bool ValidLight { get; set; }

    public bool ValidSize { get; set; }

    public bool ValidNumber { get; set; }

    public string? OcrConfidence { get; set; }

    public int UserId { get; set; }

    public DateTime? IssuingDate { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();

    public virtual User User { get; set; } = null!;
}
