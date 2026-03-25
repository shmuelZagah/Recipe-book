using Recipe_book.Models.Cloud;
using Recipe_book.Models.Organization;
using Recipe_book.Models.Recipes;
using Recipe_book.Models.Shopping;
using SQLite;
using System.Text.Json;

namespace Recipe_book.Services;

/// <summary>
/// The central local database service using SQLite.
/// Handles all CRUD operations, many-to-many relationships, initial data seeding, and cloud syncing tasks.
/// </summary>
public class RecipesDatabase
{
    private SQLiteAsyncConnection Database;
    private FirestoreService _firestoreService = new FirestoreService();

    //--------------
    #region Constructor & Initialization
    //--------------

    public RecipesDatabase()
    {
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            _ = Task.Run(async () => await ProcessPendingDeletionsAsync());
            _ = Task.Run(async () => await ProcessExpiredSharedFoldersAsync());
        }

        Connectivity.Current.ConnectivityChanged += (s, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                System.Diagnostics.Debug.WriteLine("Internet is BACK! Waking up the Garbage Collector...");
                _ = Task.Run(async () => await ProcessPendingDeletionsAsync());
                _ = Task.Run(async () => await ProcessExpiredSharedFoldersAsync());
            }
        };
    }

    private async Task Init()
    {
        if (Database is not null)
            return;

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "MyRecipes.db3");
        Database = new SQLiteAsyncConnection(dbPath);

        // --- Core: Recipes & Ingredients ---
        await Database.CreateTableAsync<Recipe>();
        await Database.CreateTableAsync<Ingredient>();
        await Database.CreateTableAsync<RecipeStep>();

        // --- Organization: Folders & Mapping ---
        await Database.CreateTableAsync<RecipeFolder>();
        await Database.CreateTableAsync<RecipeFolderMapping>();

        // --- Meal Planner ---
        await Database.CreateTableAsync<ScheduledMeal>();
        await Database.CreateTableAsync<DailyMealCategory>();

        // --- Shopping Lists & Conversions ---
        await Database.CreateTableAsync<SavedShoppingList>();
        await Database.CreateTableAsync<SavedShoppingListItem>();
        await Database.CreateTableAsync<IngredientConversion>();

        // --- Cloud & Garbage Collection ---
        await Database.CreateTableAsync<PendingCloudDeletion>();
        await Database.CreateTableAsync<PendingSharedFolderDeletion>();

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            _ = Task.Run(async () => await ProcessPendingDeletionsAsync());
        }

        await SeedIngredientsFromJsonAsync();
    }
    #endregion
    //--------------


    //--------------
    #region Background Tasks & Garbage Collection (Cloud)
    //--------------

    /// <summary>
    /// Background worker that checks the queue and attempts to delete orphaned cloud records (Recipes & Images).
    /// </summary>
    private async Task ProcessPendingDeletionsAsync()
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            await Init();

            var pendingItems = await Database.Table<PendingCloudDeletion>().ToListAsync();

            System.Diagnostics.Debug.WriteLine($"[TEST-2] Garbage Collector woke up! Found {pendingItems.Count} items in the Pending Queue.");

            if (!pendingItems.Any()) return;

            foreach (var item in pendingItems)
            {
                bool textDeleted = true;
                bool imageDeleted = true;

                if (!string.IsNullOrEmpty(item.CloudImagePath))
                    imageDeleted = await _firestoreService.DeleteImageFromCloudAsync(item.CloudImagePath);

                if (!string.IsNullOrEmpty(item.CloudId))
                    textDeleted = await _firestoreService.DeleteRecipeFromCloudAsync(item.CloudId);

                // If both succeeded, we can safely remove this item from the queue
                if (textDeleted && imageDeleted)
                {
                    await Database.DeleteAsync(item);
                }
                else
                {
                    // Partial success: update the record so we only retry what failed next time
                    item.CloudId = !textDeleted ? item.CloudId : null;
                    item.CloudImagePath = !imageDeleted ? item.CloudImagePath : null;
                    await Database.UpdateAsync(item);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing pending deletions: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes the dedicated Shared Folders TTL queue.
    /// Uses O(1) checking: If the oldest item hasn't expired, it exits immediately.
    /// </summary>
    private async Task ProcessExpiredSharedFoldersAsync()
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
            await Init();

            // O(1) CHECK: Get ONLY the oldest item in the queue
            var oldestItem = await Database.Table<PendingSharedFolderDeletion>()
                                           .OrderBy(x => x.ExpiresAt)
                                           .FirstOrDefaultAsync();

            if (oldestItem == null) return; // Queue is empty

            // The Magic: If the oldest item hasn't expired, NONE of the items after it have. Exit!
            if (DateTime.UtcNow < oldestItem.ExpiresAt)
            {
                System.Diagnostics.Debug.WriteLine("Folder GC: Oldest item still valid. Exiting early (O(1)).");
                return;
            }

            // If we reached here, there is AT LEAST ONE expired item. Let's fetch all expired ones and delete them.
            var expiredItems = await Database.Table<PendingSharedFolderDeletion>()
                                             .Where(x => x.ExpiresAt <= DateTime.UtcNow)
                                             .ToListAsync();

            foreach (var item in expiredItems)
            {
                bool folderDeleted = await _firestoreService.DeleteSharedFolderFromCloudAsync(item.SharedFolderId);

                if (folderDeleted)
                {
                    await Database.DeleteAsync(item);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing expired folders: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a shared folder to the dedicated TTL queue.
    /// </summary>
    public async Task RegisterSharedFolderForDeletionAsync(string cloudId, DateTime expirationDate)
    {
        await Init();
        await Database.InsertAsync(new PendingSharedFolderDeletion
        {
            SharedFolderId = cloudId,
            ExpiresAt = expirationDate
        });
    }

    #endregion
    //--------------


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
    /// Performs an offline-first cascading delete. 
    /// Removes the local recipe immediately, adds cloud IDs to a queue, 
    /// and triggers the background cleaner to clear the cloud.
    /// </summary>
    public async Task DeleteRecipeAsync(Recipe recipe)
    {
        await Init();

        // 1. Delete associated local records
        var ingredients = await Database.Table<Ingredient>().Where(i => i.RecipeId == recipe.Id).ToListAsync();
        foreach (var ing in ingredients) { await Database.DeleteAsync(ing); }

        var steps = await Database.Table<RecipeStep>().Where(s => s.RecipeId == recipe.Id).ToListAsync();
        foreach (var step in steps) { await Database.DeleteAsync(step); }

        var scheduledMeals = await Database.Table<ScheduledMeal>().Where(m => m.RecipeId == recipe.Id).ToListAsync();
        foreach (var meal in scheduledMeals) { await Database.DeleteAsync(meal); }

        // 2. Capture Cloud Data BEFORE deleting the local recipe
        string cloudIdToDelete = recipe.CloudId;
        string cloudImageToDelete = recipe.CloudImagePath;

        // 3. Delete the local SQLite recipe (UI reacts immediately)
        await Database.DeleteAsync(recipe);

        // 4. Save to the pending queue FIRST, before attempting cloud deletion
        if (!string.IsNullOrEmpty(cloudIdToDelete) || !string.IsNullOrEmpty(cloudImageToDelete))
        {
            await Database.InsertAsync(new PendingCloudDeletion
            {
                CloudId = cloudIdToDelete,
                CloudImagePath = cloudImageToDelete
            });

            // 5. Wake up the background cleaner. If there's internet, it cleans now. If not, the listener will catch it later.
            _ = Task.Run(async () => await ProcessPendingDeletionsAsync());
        }
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
    #region Cloud Sharing & Importing (Folders)
    //--------------

    /// <summary>
    /// Builds a full cloud-ready tree model of a folder, its recipes, and all subfolders.
    /// Automatically uploads any local-only recipes to the cloud during the process.
    /// </summary>
    public async Task<SharedFolderCloudModel> BuildSharedFolderTreeAsync(RecipeFolder rootFolder)
    {
        await Init();

        // Fetch all folders once to avoid querying the DB in every recursive step
        var allFolders = await GetFoldersAsync();

        var rootNode = await BuildFolderNodeRecursiveAsync(rootFolder, allFolders);

        // Serialize the entire complex tree into a single, safe JSON string
        string jsonPayload = System.Text.Json.JsonSerializer.Serialize(rootNode);

        return new SharedFolderCloudModel
        {
            BookName = rootFolder.Name,
            RootFolderJson = jsonPayload
        };
    }

    /// <summary>
    /// Recursive helper to build nodes for the folder tree.
    /// </summary>
    private async Task<SharedFolderNode> BuildFolderNodeRecursiveAsync(RecipeFolder currentFolder, List<RecipeFolder> allFolders)
    {
        var node = new SharedFolderNode { FolderName = currentFolder.Name };

        // 1. Process Recipes in this folder
        var recipesInFolder = await GetRecipesInFolderAsync(currentFolder.Id);
        foreach (var recipe in recipesInFolder)
        {
            // If the recipe hasn't been uploaded yet, upload it now to get a CloudId
            if (string.IsNullOrEmpty(recipe.CloudId))
            {
                // Ensure full recipe data is loaded before upload (ingredients & steps)
                var ingredients = await GetIngredientsAsync(recipe.Id);
                var steps = await GetStepsAsync(recipe.Id);
                recipe.Ingredients = new System.Collections.ObjectModel.ObservableCollection<Ingredient>(ingredients);
                recipe.Steps = new System.Collections.ObjectModel.ObservableCollection<RecipeStep>(steps);

                await _firestoreService.SaveRecipeToCloudAsync(recipe);
                await SaveRecipeAsync(recipe); // Save the new CloudId to local SQLite
            }

            if (!string.IsNullOrEmpty(recipe.CloudId))
            {
                node.RecipeIds.Add(recipe.CloudId);
            }
        }

        // 2. Process Subfolders recursively
        var subFolders = allFolders.Where(f => f.ParentFolderId == currentFolder.Id).ToList();
        foreach (var subFolder in subFolders)
        {
            var childNode = await BuildFolderNodeRecursiveAsync(subFolder, allFolders);
            node.SubFolders.Add(childNode);
        }

        return node;
    }

    /// <summary>
    /// Unpacks a shared folder JSON string, downloads missing recipes, and builds the local folder tree.
    /// </summary>
    public async Task ImportSharedFolderAsync(string jsonPayload, int? targetParentFolderId)
    {
        await Init();

        if (string.IsNullOrWhiteSpace(jsonPayload)) return;

        try
        {
            // 1. Deserialize the flat JSON string back into our C# tree structure
            var rootNode = JsonSerializer.Deserialize<SharedFolderNode>(jsonPayload);

            if (rootNode != null)
            {
                // 2. Start the recursive unpacking process
                await ProcessFolderNodeAsync(rootNode, targetParentFolderId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error unpacking shared folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Recursively processes a node, creates local folders, downloads missing recipes, and maps them.
    /// </summary>
    private async Task ProcessFolderNodeAsync(SharedFolderNode node, int? parentFolderId)
    {
        // Step A: Create the local folder in SQLite
        var newFolder = new RecipeFolder
        {
            Name = node.FolderName,
            ParentFolderId = parentFolderId
        };

        await SaveFolderAsync(newFolder);
        int currentLocalFolderId = newFolder.Id;

        // Step B: Process the recipes in this folder
        if (node.RecipeIds != null && node.RecipeIds.Any())
        {
            foreach (var cloudId in node.RecipeIds)
            {
                // Check if we already have this recipe locally (saves bandwidth!)
                var existingRecipe = await Database.Table<Recipe>().Where(r => r.CloudId == cloudId).FirstOrDefaultAsync();

                int localRecipeId = 0;

                if (existingRecipe != null)
                {
                    localRecipeId = existingRecipe.Id;
                }
                else
                {
                    // Recipe is missing locally. Fetch it from Firestore!
                    var downloadedRecipe = await _firestoreService.GetRecipeFromCloudAsync(cloudId);

                    if (downloadedRecipe != null)
                    {
                        // Save the base recipe to SQLite to generate a local ID
                        await SaveRecipeAsync(downloadedRecipe);
                        localRecipeId = downloadedRecipe.Id;

                        // Save its inner ingredients locally
                        if (downloadedRecipe.Ingredients != null)
                        {
                            foreach (var ing in downloadedRecipe.Ingredients)
                            {
                                ing.RecipeId = localRecipeId;
                                await SaveIngredientAsync(ing);
                            }
                        }

                        // Save its inner steps locally
                        if (downloadedRecipe.Steps != null)
                        {
                            foreach (var step in downloadedRecipe.Steps)
                            {
                                step.RecipeId = localRecipeId;
                                await SaveStepAsync(step);
                            }
                        }
                    }
                }

                // Map the recipe (either existing or newly downloaded) to our new local folder
                if (localRecipeId != 0)
                {
                    await AddRecipeToFolderAsync(localRecipeId, currentLocalFolderId);
                }
            }
        }

        // Step C: Dive down! Process subfolders recursively
        if (node.SubFolders != null && node.SubFolders.Any())
        {
            foreach (var subNode in node.SubFolders)
            {
                // Notice how we pass 'currentLocalFolderId' as the new parent ID
                await ProcessFolderNodeAsync(subNode, currentLocalFolderId);
            }
        }
    }

    #endregion
    //--------------


    //--------------
    #region Meal Planner (Scheduled Meals & Categories)
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
    #region Saved Shopping Lists CRUD
    //--------------

    /// <summary>
    /// Retrieves all saved shopping lists, ordered by creation date (newest first).
    /// </summary>
    public async Task<List<SavedShoppingList>> GetSavedShoppingListsAsync()
    {
        await Init();
        return await Database.Table<SavedShoppingList>().OrderByDescending(l => l.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Retrieves a specific shopping list by its ID.
    /// </summary>
    public async Task<SavedShoppingList> GetSavedShoppingListAsync(int listId)
    {
        await Init();
        return await Database.Table<SavedShoppingList>().Where(l => l.Id == listId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Inserts a new shopping list or updates an existing one.
    /// </summary>
    public async Task<int> SaveShoppingListAsync(SavedShoppingList list)
    {
        await Init();
        if (list.Id != 0)
        {
            return await Database.UpdateAsync(list);
        }
        else
        {
            list.CreatedAt = DateTime.Now;
            return await Database.InsertAsync(list);
        }
    }

    /// <summary>
    /// Deletes a shopping list and all its associated items.
    /// </summary>
    public async Task DeleteShoppingListAsync(SavedShoppingList list)
    {
        await Init();

        var items = await GetItemsForShoppingListAsync(list.Id);
        foreach (var item in items)
        {
            await Database.DeleteAsync(item);
        }

        await Database.DeleteAsync(list);
    }

    /// <summary>
    /// Retrieves all items belonging to a specific shopping list.
    /// </summary>
    public async Task<List<SavedShoppingListItem>> GetItemsForShoppingListAsync(int listId)
    {
        await Init();
        return await Database.Table<SavedShoppingListItem>().Where(i => i.ListId == listId).ToListAsync();
    }

    public async Task<int> SaveShoppingListItemAsync(SavedShoppingListItem item)
    {
        await Init();
        if (item.Id != 0)
            return await Database.UpdateAsync(item);
        else
            return await Database.InsertAsync(item);
    }

    public async Task<int> DeleteShoppingListItemAsync(SavedShoppingListItem item)
    {
        await Init();
        return await Database.DeleteAsync(item);
    }

    /// <summary>
    /// Replaces all items in a specific list with a new set of items. 
    /// Useful for updating dynamic lists generated from the meal planner.
    /// </summary>
    public async Task SyncShoppingListItemsAsync(int listId, List<SavedShoppingListItem> newItems)
    {
        await Init();

        // 1. Delete old items
        var existingItems = await GetItemsForShoppingListAsync(listId);
        foreach (var item in existingItems)
        {
            await Database.DeleteAsync(item);
        }

        // 2. Insert new items
        foreach (var newItem in newItems)
        {
            newItem.ListId = listId;
            await Database.InsertAsync(newItem);
        }
    }

    #endregion
    //--------------
}