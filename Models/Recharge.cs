using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class Recharge
{
    public int Id { get; set; }

    public string? DeviceType { get; set; }

    public string? Hrn { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentToken { get; set; }

    public string? ProductId { get; set; }

    public string? VoucherType { get; set; }

    public string? ZainCashWalletNumber { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public int LineId { get; set; }

    public string? Amount { get; set; }

    public string? AmountWithTax { get; set; }

    public virtual Line Line { get; set; } = null!;
}
