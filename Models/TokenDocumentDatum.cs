using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class TokenDocumentDatum
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string? Number { get; set; }

    public string? IssuingCountry { get; set; }

    public int TokenId { get; set; }

    public DateTime? IssuingDate { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual Token Token { get; set; } = null!;

    public virtual ICollection<TokenValidationDatum> TokenValidationData { get; set; } = new List<TokenValidationDatum>();
}
