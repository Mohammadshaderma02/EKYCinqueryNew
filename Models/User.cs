using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class User
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string? Firstname { get; set; }

    public string? Lastname { get; set; }

    public string? PhonePrefix { get; set; }

    public string? PhoneNumber { get; set; }

    public string? BirthCountry { get; set; }

    public string? BirthProvince { get; set; }

    public string? BirthCity { get; set; }

    public string? Nationality { get; set; }

    public int? Age { get; set; }

    public string? Parents { get; set; }

    public int SessionId { get; set; }

    public string? FiscalCode { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();

    public virtual Session Session { get; set; } = null!;

    public virtual UserAddress? UserAddress { get; set; }

    public virtual UserDocumentDatum? UserDocumentDatum { get; set; }
}
