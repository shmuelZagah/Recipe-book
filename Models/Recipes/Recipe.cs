using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;
using System.Collections.ObjectModel;
using Plugin.Firebase.Firestore;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// The core entity representing a recipe, containing metadata and serving as a parent container for its ingredients and steps.
/// </summary>
public partial class Recipe : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    [FirestoreProperty("Id")]
    public int Id { get; set; }

    [FirestoreProperty("Title")]
    public string Title { get; set; }

    [FirestoreProperty("Description")]
    public string Description { get; set; }

    //-----------------------
    #region Image Handling 
    //-----------------------

    public string LocalImagePath { get; set; }

    [FirestoreProperty("CloudImagePath")]
    public string CloudImagePath { get; set; }

    [Ignore]
    public string DisplayImage
    {
        get
        {
            if (!string.IsNullOrEmpty(LocalImagePath) && File.Exists(LocalImagePath))
            {
                return LocalImagePath;
            }
            return CloudImagePath;
        }
    }

    #endregion
    //----------------------

    [FirestoreProperty("CloudId")]
    public string CloudId { get; set; }

    [ObservableProperty]
    [property: FirestoreProperty("IsFavorite")]
    public bool isFavorite = false;

    [FirestoreProperty("LastCookedDate")]
    public DateTime? LastCookedDate { get; set; }

    // Observable collections populated dynamically for the UI. Ignored by the SQLite database but synced to Firestore.
    [Ignore]
    public ObservableCollection<Ingredient> Ingredients { get; set; } = new ObservableCollection<Ingredient>();

    [Ignore]
    public ObservableCollection<RecipeStep> Steps { get; set; } = new ObservableCollection<RecipeStep>();




    // Firebase-friendly translation using Dictionary (Bypasses serialization errors)
    [Ignore]
    [FirestoreProperty("Ingredients")]
    public List<Dictionary<string, object>> FirestoreIngredients
    {
        get
        {
            if (Ingredients == null) return new List<Dictionary<string, object>>();
            return Ingredients.Select(i => new Dictionary<string, object>
            {
                { "Name", i.Name ?? "" },
                { "Quantity", i.Quantity ?? 0.0 },
                { "Unit", i.Unit ?? "" },
                { "OrderIndex", i.OrderIndex }
            }).ToList();
        }
        set
        {
            var list = new ObservableCollection<Ingredient>();
            if (value != null)
            {
                foreach (var dict in value)
                {
                    list.Add(new Ingredient
                    {
                        Name = dict.ContainsKey("Name") ? dict["Name"]?.ToString() : "",
                        Quantity = dict.ContainsKey("Quantity") && dict["Quantity"] != null ? Convert.ToDouble(dict["Quantity"]) : 0.0,
                        Unit = dict.ContainsKey("Unit") ? dict["Unit"]?.ToString() : "",
                        OrderIndex = dict.ContainsKey("OrderIndex") && dict["OrderIndex"] != null ? Convert.ToInt32(dict["OrderIndex"]) : 0
                    });
                }
            }
            Ingredients = list;
        }
    }

    [Ignore]
    [FirestoreProperty("Steps")]
    public List<Dictionary<string, object>> FirestoreSteps
    {
        get
        {
            if (Steps == null) return new List<Dictionary<string, object>>();
            return Steps.Select(s => new Dictionary<string, object>
            {
                { "Description", s.Description ?? "" },
                { "StepNumber", s.StepNumber },
                { "IsOptional", s.IsOptional }
            }).ToList();
        }
        set
        {
            var list = new ObservableCollection<RecipeStep>();
            if (value != null)
            {
                foreach (var dict in value)
                {
                    list.Add(new RecipeStep
                    {
                        Description = dict.ContainsKey("Description") ? dict["Description"]?.ToString() : "",
                        StepNumber = dict.ContainsKey("StepNumber") && dict["StepNumber"] != null ? Convert.ToInt32(dict["StepNumber"]) : 0,
                        IsOptional = dict.ContainsKey("IsOptional") && dict["IsOptional"] != null && Convert.ToBoolean(dict["IsOptional"]),
                        IsCompleted = false
                    });
                }
            }
            Steps = list;
        }
    }
}