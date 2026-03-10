using SQLite;

namespace Recipe_book.Models.Organization;

/// <summary>
/// Represents a mapping entity that links a recipe to a specific folder.
/// Allows for a many-to-many or one-to-many relationship between recipes and folders.
/// </summary>
public class RecipeFolderMapping
{
    /// <summary>
    /// Gets or sets the unique identifier for the mapping record.
    /// </summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the associated recipe.
    /// Indexed to ensure lightning-fast database queries.
    /// </summary>
    [Indexed]
    public int RecipeId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the associated folder.
    /// Indexed to optimize folder-based recipe retrieval.
    /// </summary>
    [Indexed]
    public int FolderId { get; set; }
}