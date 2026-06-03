using Plugin.Firebase.Firestore;
using System.Collections.Generic;
using System;

namespace Recipe_book.Models.Cloud;

public class SharedCloudItemDto
{
    public string N { get; set; }
    public double Q { get; set; }
    public string U { get; set; }
    public string C { get; set; }
    public bool IsBought { get; set; }
}

public class SharedCloudConversionDto
{
    public string K { get; set; }
    public string B { get; set; }
    public double A { get; set; }
    public string C { get; set; }
}

public class SharedShoppingListCloudModel
{
    [FirestoreDocumentId]
    public string CloudId { get; set; }

    [FirestoreProperty("ListName")]
    public string ListName { get; set; }

    [FirestoreProperty("ItemsJson")]
    public string ItemsJson { get; set; }

    [FirestoreProperty("ConversionsJson")]
    public string ConversionsJson { get; set; }

    [FirestoreProperty("PartnerUids")]
    public List<string> PartnerUids { get; set; } = new();

    [FirestoreProperty("LastActionBy")]
    public string LastActionBy { get; set; }

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