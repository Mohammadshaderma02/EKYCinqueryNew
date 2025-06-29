using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Line
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string? Kitcode { get; set; }

    public string? LineType { get; set; }

    public string? MarketType { get; set; }

    public string? Msisdn { get; set; }

    public string? PassportBarcode { get; set; }

    public string? ReferenceNumber { get; set; }

    public bool? RequireReferenceNumber { get; set; }

    public string? SimCard { get; set; }

    public string? SimCardType { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }
    public string? flow {  get; set; }
    public string? nationalId {  get; set; }

    public virtual Package? Package { get; set; }

    public virtual Recharge? Recharge { get; set; }
}

public class LineNoNavigation
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string? Kitcode { get; set; }

    public string? LineType { get; set; }

    public string? MarketType { get; set; }

    public string? Msisdn { get; set; }

    public string? PassportBarcode { get; set; }

    public string? ReferenceNumber { get; set; }

    public bool? RequireReferenceNumber { get; set; }

    public string? SimCard { get; set; }

    public string? SimCardType { get; set; }

    public DateTime? CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }
    public string? flow { get; set; }
    public string? nationalId { get; set; }
}
