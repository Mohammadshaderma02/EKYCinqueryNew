using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Package
{
    public int Id { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public int LineId { get; set; }

    public string? DataPrice { get; set; }

    public string? GsmPrice { get; set; }

    public string PackageCode { get; set; } = null!;

    public string? PackageDetails { get; set; }

    public string PackageId { get; set; } = null!;

    public string? ServiceClass { get; set; }

    public string? SwitchFee { get; set; }

    public string? ArabicDescription { get; set; }

    public string? EnglishDescription { get; set; }

    public string? PromotionSubtitle { get; set; }

    public string? PromotionSubtitleAr { get; set; }

    public string? PromotionTitle { get; set; }

    public string? PromotionTitleAr { get; set; }

    public virtual Line Line { get; set; } = null!;
}
