using Recipe_book.Models.Organization;
using Recipe_book.Models.Recipes;
using SQLite;
using System.Text.Json;

namespace Recipe_book.Services;

/// <summary>
/// The central local database service using SQLite.
/// Handles all CRUD operations, many-to-many relationships, and initial data seeding.
/// </summary>
public class RecipesDatabase
{
    private SQLiteAsyncConnection Database;

    public RecipesDatabase()
    {
    }

    #region Database Initialization
    private async Task Init()
    {
        if (Database is not null)
            return;

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "MyRecipes.db3");
        Database = new SQLiteAsyncConnection(dbPath);

        // Initialize all required tables
        await Database.CreateTableAsync<Recipe>();
        await Database.CreateTableAsync<Ingredient>();
        await Database.CreateTableAsync<RecipeStep>();
        await Database.CreateTableAsync<RecipeFolder>();
        await Database.CreateTableAsync<RecipeFolderMapping>();
        await Database.CreateTableAsync<ScheduledMeal>();
        await Database.CreateTableAsync<DailyMealCategory>();
        await Database.CreateTableAsync<IngredientConversion>();
        await Database.CreateTableAsync<BoughtItemRecord>();

        await SeedIngredientsFromJsonAsync();
    }
    #endregion


    //--------------
    #region Recipe CRUD
    //--------------
    public async Task<List<Recipe>> GetRecipesAsync()
    {
        await Init();
        return await Database.Table<Recipe>().ToListAsync();
    }

    public async Task<Recipe> GetRecipeAsync(int id)
    {
        await Init();
        return await Database.Table<Recipe>().Where(i => i.Id == id).FirstOrDefaultAsync();
    }

    public async Task<int> SaveRecipeAsync(Recipe item)
    {
        await Init();
        if (item.Id != 0)
            return await Database.UpdateAsync(item);
        else
            return await Database.InsertAsync(item);
    }

    /// <summary>
    /// Performs a cascading delete to ensure no orphaned records remain when a recipe is removed.
    /// </summary>
    public async Task DeleteRecipeAsync(Recipe recipe)
    {
        await Init();

        // 1. Delete associated ingredients
        var ingredients = await Database.Table<Ingredient>().Where(i => i.RecipeId == recipe.Id).ToListAsync();
        foreach (var ing in ingredients) { await Database.DeleteAsync(ing); }

        // 2. Delete preparation steps
        var steps = await Database.Table<RecipeStep>().Where(s => s.RecipeId == recipe.Id).ToListAsync();
        foreach (var step in steps) { await Database.DeleteAsync(step); }

        // 3. Delete scheduled meals to prevent UI bugs with missing recipes
        var scheduledMeals = await Database.Table<ScheduledMeal>().Where(m => m.RecipeId == recipe.Id).ToListAsync();
        foreach (var meal in scheduledMeals) { await Database.DeleteAsync(meal); }

        // 4. Finally, delete the recipe itself
        await Database.DeleteAsync(recipe);
    }
    #endregion
    //--------------

    //--------------
    #region Folders CRUD
    //--------------
    public async Task<List<RecipeFolder>> GetFoldersAsync()
    {
        await Init();
        return await Database.Table<RecipeFolder>().ToListAsync();
    }

    public async Task<int> SaveFolderAsync(RecipeFolder item)
    {
        await Init();
        if (item.Id != 0)
            return await Database.UpdateAsync(item);
        else
            return await Database.InsertAsync(item);
    }

    public async Task<int> DeleteFolderAsync(RecipeFolder item)
    {
        await Init();
        return await Database.DeleteAsync(item);
    }
    #endregion
    //--------------

    //--------------
    #region Ingredients CRUD
    //--------------
    public async Task<List<Ingredient>> GetIngredientsAsync(int recipeId)
    {
        await Init();
        return await Database.Table<Ingredient>().Where(i => i.RecipeId == recipeId).ToListAsync();
    }

    public async Task<int> SaveIngredientAsync(Ingredient item)
    {
        await Init();
        if (item.Id != 0)
            return await Database.UpdateAsync(item);
        else
            return await Database.InsertAsync(item);
    }

    public async Task<int> DeleteIngredientAsync(Ingredient item)
    {
        await Init();
        return await Database.DeleteAsync(item);
    }

    public async Task<List<Ingredient>> GetIngredientsByNameAsync(string name)
    {
        await Init();
        return await Database.Table<Ingredient>().Where(i => i.Name == name).ToListAsync();
    }
    #endregion
    //--------------

    //--------------
    #region Steps CRUD
    //--------------
    public async Task<List<RecipeStep>> GetStepsAsync(int recipeId)
    {
        await Init();
        return await Database.Table<RecipeStep>().Where(s => s.RecipeId == recipeId).ToListAsync();
    }

    public async Task<int> SaveStepAsync(RecipeStep item)
    {
        await Init();
        if (item.Id != 0)
            return await Database.UpdateAsync(item);
        else
            return await Database.InsertAsync(item);
    }

    public async Task<int> DeleteStepAsync(RecipeStep item)
    {
        await Init();
        return await Database.DeleteAsync(item);
    }
    #endregion
    //--------------

    //--------------
    #region Recipe to Folder Mapping (Many-to-Many)
    //--------------
    public async Task AddRecipeToFolderAsync(int recipeId, int folderId)
    {
        await Init();

        // Check for existing relationship to prevent duplicates
        var existing = await Database.Table<RecipeFolderMapping>()
            .Where(m => m.RecipeId == recipeId && m.FolderId == folderId)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            await Database.InsertAsync(new RecipeFolderMapping
            {
                RecipeId = recipeId,
                FolderId = folderId
            });
        }
    }

    public async Task<List<Recipe>> GetRecipesInFolderAsync(int folderId)
    {
        await Init();

        var mappings = await Database.Table<RecipeFolderMapping>()
                                     .Where(m => m.FolderId == folderId)
                                     .ToListAsync();

        var recipeIds = mappings.Select(m => m.RecipeId).ToList();

        if (!recipeIds.Any()) return new List<Recipe>();

        // Retrieve actual recipe objects based on the mapped IDs
        var allRecipes = await GetRecipesAsync();
        return allRecipes.Where(r => recipeIds.Contains(r.Id)).ToList();
    }

    /// <summary>
    /// Retrieves recipes that are not assigned to any folder (for the root library view).
    /// </summary>
    public async Task<List<Recipe>> GetRecipesWithoutFolderAsync()
    {
        await Init();

        var allMappings = await Database.Table<RecipeFolderMapping>().ToListAsync();
        var mappedRecipeIds = allMappings.Select(m => m.RecipeId).Distinct().ToList();

        var allRecipes = await GetRecipesAsync();
        return allRecipes.Where(r => !mappedRecipeIds.Contains(r.Id)).ToList();
    }

    public async Task RemoveAllRecipesFromFolderAsync(int folderId)
    {
        await Init();
        var mappings = await Database.Table<RecipeFolderMapping>()
                                     .Where(m => m.FolderId == folderId)
                                     .ToListAsync();

        foreach (var map in mappings)
        {
            await Database.DeleteAsync(map);
        }
    }

    public async Task<List<RecipeFolder>> GetFoldersForRecipeAsync(int recipeId)
    {
        await Init();
        var mappings = await Database.Table<RecipeFolderMapping>()
                                     .Where(m => m.RecipeId == recipeId)
                                     .ToListAsync();

        var folderIds = mappings.Select(m => m.FolderId).ToList();
        if (!folderIds.Any()) return new List<RecipeFolder>();

        var allFolders = await GetFoldersAsync();
        return allFolders.Where(f => folderIds.Contains(f.Id)).ToList();
    }

    /// <summary>
    /// Syncs a recipe's folder assignments by completely replacing its previous mappings.
    /// </summary>
    public async Task UpdateRecipeFoldersAsync(int recipeId, List<int> newFolderIds)
    {
        await Init();

        // 1. Clear existing mappings for this recipe
        var existingMappings = await Database.Table<RecipeFolderMapping>()
                                             .Where(m => m.RecipeId == recipeId)
                                             .ToListAsync();
        foreach (var map in existingMappings)
        {
            await Database.DeleteAsync(map);
        }

        // 2. Insert the updated selections
        foreach (var folderId in newFolderIds)
        {
            await Database.InsertAsync(new RecipeFolderMapping
            {
                RecipeId = recipeId,
                FolderId = folderId
            });
        }
    }
    #endregion
    //--------------

    //--------------
    #region Scheduled Meal CRUD 
    //--------------

    public async Task<List<ScheduledMeal>> GetScheduledMealsAsync(DateTime startDate, DateTime endDate)
    {
        await Init();
        return await Database.Table<ScheduledMeal>()
                                .Where(m => m.Date >= startDate && m.Date <= endDate)
                                .ToListAsync();
    }

    public async Task<int> SaveScheduledMealAsync(ScheduledMeal meal)
    {
        await Init();
        if (meal.Id != 0)
            return await Database.UpdateAsync(meal);
        else
            return await Database.InsertAsync(meal);
    }

    public async Task<int> DeleteScheduledMealAsync(ScheduledMeal meal)
    {
        await Init();
        return await Database.DeleteAsync(meal);
    }
    #endregion
    //--------------

    //--------------
    #region DailyMealCategory CRUD
    //--------------

    public async Task<List<DailyMealCategory>> GetMealCategoriesAsync(DateTime date)
    {
        await Init();
        return await Database.Table<DailyMealCategory>()
                             .Where(c => c.Date == date)
                             .OrderBy(c => c.DisplayOrder)
                             .ToListAsync();
    }

    public async Task SaveMealCategoriesAsync(DateTime date, List<DailyMealCategory> categories)
    {
        await Init();

        // Clear existing categories for the specific date to prevent duplicates
        var existing = await Database.Table<DailyMealCategory>().Where(c => c.Date == date).ToListAsync();
        foreach (var item in existing)
        {
            await Database.DeleteAsync(item);
        }

        // Insert new categories containing updated drag-and-drop order
        await Database.InsertAllAsync(categories);
    }
    #endregion
    //--------------

    //--------------
    #region Ingredient Conversion Seeding
    //--------------

    /// <summary>
    /// Loads the initial dictionary of ingredient measurements and categories from an embedded JSON file.
    /// </summary>
    public async Task SeedIngredientsFromJsonAsync()
    {
        var count = await Database.Table<IngredientConversion>().CountAsync();

        // Only seed if the table is empty to avoid duplicates on subsequent runs
        if (count == 0)
        {
            try
            {
                // Read from MauiAsset
                using var stream = await FileSystem.OpenAppPackageFileAsync("ingredients.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                var conversions = JsonSerializer.Deserialize<List<IngredientConversion>>(json);

                if (conversions != null && conversions.Any())
                {
                    await Database.InsertAllAsync(conversions);
                    System.Diagnostics.Debug.WriteLine($"Successfully seeded {conversions.Count} ingredient conversions.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error seeding ingredients: {ex.Message}");
            }
        }
    }

    public async Task<List<IngredientConversion>> GetIngredientConversionsAsync()
    {
        return await Database.Table<IngredientConversion>().ToListAsync();
    }

    public async Task AddIngredientConversionAsync(IngredientConversion conversion)
    {
        await Init();
        await Database.InsertAsync(conversion);
    }
    #endregion
    //--------------

    //--------------
    #region Shopping List State (Bought Items)
    //--------------

    public async Task<List<BoughtItemRecord>> GetBoughtItemsAsync()
    {
        await Init();
        return await Database.Table<BoughtItemRecord>().ToListAsync();
    }

    public async Task AddBoughtItemAsync(string name)
    {
        await Init();
        await Database.InsertOrReplaceAsync(new BoughtItemRecord { ItemName = name });
    }

    public async Task RemoveBoughtItemAsync(string name)
    {
        await Init();
        await Database.DeleteAsync<BoughtItemRecord>(name);
    }
    #endregion
    //--------------
}