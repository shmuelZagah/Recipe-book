using SQLite;
using System;

namespace Recipe_book.Models.Cloud;

/// <summary>
/// A dedicated FIFO queue for shared shopping lists TTL (Time-To-Live).
/// </summary>
public class PendingSharedListDeletion
{
    [PrimaryKey, AutoIncrement]


    public int Id { get; set; }

    public string SharedListId { get; set; }

    public DateTime ExpiresAt { get; set; }
}