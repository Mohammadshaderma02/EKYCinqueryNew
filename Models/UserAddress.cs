using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class UserAddress
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string? StreetName { get; set; }

    public string? StreetNumber { get; set; }

    public string? ZipCode { get; set; }

    public string? District { get; set; }

    public string? Floor { get; set; }

    public string? Unit { get; set; }

    public string? Country { get; set; }

    public string? Province { get; set; }

    public string? City { get; set; }

    public int UserId { get; set; }

    public string? FullAddress { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual User User { get; set; } = null!;
}
