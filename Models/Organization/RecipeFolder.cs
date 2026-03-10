using SQLite;

namespace Recipe_book.Models.Organization;

/// <summary>
/// Represents a folder used to organize recipes.
/// Supports a hierarchical folder structure via the ParentFolderId.
/// </summary>
public class RecipeFolder
{
    /// <summary>
    /// Gets or sets the unique identifier for the folder.
    /// </summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the folder.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the ID of the parent folder. 
    /// If null, this is considered a root folder.
    /// </summary>
    public int? ParentFolderId { get; set; }
}