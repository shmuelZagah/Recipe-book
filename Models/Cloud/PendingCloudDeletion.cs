using SQLite;

namespace Recipe_book.Models.Cloud;

/// <summary>
/// Represents a queue of items that need to be deleted from the cloud (Firestore/Cloudinary) 
/// when the device regains internet connection.
/// </summary>
public class PendingCloudDeletion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string CloudId { get; set; }

    public string CloudImagePath { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.Now;
}
