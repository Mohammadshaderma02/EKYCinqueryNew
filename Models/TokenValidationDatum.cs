using System;
using System.Collections.Generic;

namespace EkycInquiry.Models;

public partial class TokenValidationDatum
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Field { get; set; } = null!;

    public bool OcrCheck { get; set; }

    public bool CanBeEdited { get; set; }

    public int? TokenUserDataId { get; set; }

    public int? TokenDocumentDataId { get; set; }

    public int? TokenUserAddressDataId { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ModificationTime { get; set; }

    public DateTime? DeleteTime { get; set; }

    public bool Deleted { get; set; }

    public virtual TokenDocumentDatum? TokenDocumentData { get; set; }

    public virtual TokenUserAddressDatum? TokenUserAddressData { get; set; }

    public virtual TokenUserDatum? TokenUserData { get; set; }
}
