using Plugin.Firebase.Firestore;
using System;

namespace Recipe_book.Models.Cloud;

/// <summary>
/// Represents a shared shopping list in the cloud.
/// </summary>
public class SharedShoppingListCloudModel
{
    [FirestoreDocumentId]
    public string CloudId { get; set; }

    [FirestoreProperty("listName")]
    public string ListName { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // The entire payload (Items + Ingredient Conversions) is packaged into this single JSON string
    [FirestoreProperty("payloadJson")]
    public string PayloadJson { get; set; }

    // TTL for the Garbage Collector (10 days)
    [FirestoreProperty("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(10);
}