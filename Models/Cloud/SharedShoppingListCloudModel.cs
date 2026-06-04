using Plugin.Firebase.Firestore;
using System.Collections.Generic;
using System;

namespace Recipe_book.Models.Cloud;

public class SharedCloudItemDto
{
    [FirestoreDocumentId]
    public string DocumentId { get; set; } 

    [FirestoreProperty("N")] public string N { get; set; }
    [FirestoreProperty("Q")] public double Q { get; set; }
    [FirestoreProperty("U")] public string U { get; set; }
    [FirestoreProperty("C")] public string C { get; set; }
    [FirestoreProperty("IsBought")] public bool IsBought { get; set; }

    [FirestoreProperty("LastActionBy")] public string LastActionBy { get; set; }
}

public class SharedCloudConversionDto
{
    [FirestoreDocumentId]
    public string DocumentId { get; set; }

    [FirestoreProperty("K")] public string K { get; set; }
    [FirestoreProperty("B")] public string B { get; set; }
    [FirestoreProperty("A")] public double A { get; set; }
    [FirestoreProperty("C")] public string C { get; set; }
}

public class SharedShoppingListCloudModel
{
    [FirestoreDocumentId]
    public string CloudId { get; set; }

    [FirestoreProperty("ListName")]
    public string ListName { get; set; }

    [FirestoreProperty("PartnerUids")]
    public List<string> PartnerUids { get; set; } = new();

    [FirestoreProperty("UpdatedAtTicks")]
    public long UpdatedAtTicks { get; set; }

    [FirestoreProperty("ExpiresAtTicks")]
    public long ExpiresAtTicks { get; set; }

    public DateTime UpdatedAt
    {
        get => new DateTime(UpdatedAtTicks, DateTimeKind.Utc);
        set => UpdatedAtTicks = value.Ticks;
    }

    public DateTime ExpiresAt
    {
        get => new DateTime(ExpiresAtTicks, DateTimeKind.Utc);
        set => ExpiresAtTicks = value.Ticks;
    }
}