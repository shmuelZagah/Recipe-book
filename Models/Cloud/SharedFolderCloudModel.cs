using Plugin.Firebase.Firestore;
using System;
using System.Collections.Generic;

namespace Recipe_book.Models.Cloud;

/// <summary>
/// Represents the main document stored in Firestore under "SharedFolders".
/// This is the "Book" that contains the root folder and all its nested contents.
/// </summary>
public class SharedFolderCloudModel
{
    [FirestoreDocumentId]
    public string CloudId { get; set; }

    [FirestoreProperty("bookName")]
    public string BookName { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Bulletproof approach: The entire recursive tree is stored as a single JSON string!
    [FirestoreProperty("rootFolderJson")]
    public string RootFolderJson { get; set; }

    [FirestoreProperty("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(15);
}

/// <summary>
/// A recursive node representing a single folder, its recipes, and its subfolders.
/// </summary>
public class SharedFolderNode
{
    public string FolderName { get; set; }
    public List<string> RecipeIds { get; set; } = new List<string>();
    public List<SharedFolderNode> SubFolders { get; set; } = new List<SharedFolderNode>();
}