using SQLite;
using System;

namespace Recipe_book.Models.Cloud;

/// <summary>
/// A dedicated FIFO queue for shared folders TTL (Time-To-Live).
/// </summary>
public class PendingSharedFolderDeletion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string SharedFolderId { get; set; }

    public DateTime ExpiresAt { get; set; }
}