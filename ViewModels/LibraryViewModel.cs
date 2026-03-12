using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Recipe_book.Models.Organization;
using Recipe_book.Services;
using Recipe_book.Models.Recipes;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.Text;
using System.Text.Json;
using System.IO;
using System.IO.Compression;

namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for managing the recipe library, including folder navigation, search, and recipe operations.
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    public ObservableCollection<RecipeFolder> Folders { get; } = new();
    public ObservableCollection<Recipe> FolderRecipes { get; } = new();

    [ObservableProperty]
    private string searchQuery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsInnerFolder))]
    [NotifyPropertyChangedFor(nameof(IsRootFolder))]
    private RecipeFolder currentFolder;

    public bool IsRootFolder => CurrentFolder == null;
    public bool IsInnerFolder => CurrentFolder != null;

    public string PageTitle => CurrentFolder == null ? "המתכונים שלי" : CurrentFolder.Name;

    [ObservableProperty]
    private int gridSpan = 2;

    #endregion
    //--------------

    public LibraryViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Logic & Navigation
    //--------------

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadFoldersAsync();
    }

    /// <summary>
    /// Recursively finds a folder and all its descendant subfolders.
    /// </summary>
    private List<RecipeFolder> GetFolderAndDescendants(RecipeFolder targetFolder, List<RecipeFolder> allFolders)
    {
        var result = new List<RecipeFolder> { targetFolder };
        var children = allFolders.Where(f => f.ParentFolderId == targetFolder.Id).ToList();

        foreach (var child in children)
        {
            result.AddRange(GetFolderAndDescendants(child, allFolders));
        }

        return result;
    }

    private async Task ExecuteDeleteFolderFlowAsync(RecipeFolder folder)
    {
        string deleteOption = await Application.Current.MainPage.DisplayActionSheet(
            $"מחיקת '{folder.Name}'",
            "ביטול",
            null,
            "מחק תיקייה בלבד (המתכונים יישמרו)",
            "מחק תיקייה וגם את המתכונים שבתוכה");

        if (deleteOption == "ביטול" || string.IsNullOrEmpty(deleteOption)) return;

        var allFolders = await _database.GetFoldersAsync();
        var foldersToDelete = GetFolderAndDescendants(folder, allFolders);

        if (deleteOption == "מחק תיקייה בלבד (המתכונים יישמרו)")
        {
            foreach (var fToDelete in foldersToDelete)
            {
                await _database.RemoveAllRecipesFromFolderAsync(fToDelete.Id);
                await _database.DeleteFolderAsync(fToDelete);
                Folders.Remove(fToDelete);
            }
        }
        else if (deleteOption == "מחק תיקייה וגם את המתכונים שבתוכה")
        {
            foreach (var fToDelete in foldersToDelete)
            {
                var recipesToDelete = await _database.GetRecipesInFolderAsync(fToDelete.Id);
                foreach (var recipe in recipesToDelete)
                {
                    await _database.DeleteRecipeAsync(recipe);
                }

                await _database.RemoveAllRecipesFromFolderAsync(fToDelete.Id);
                await _database.DeleteFolderAsync(fToDelete);
                Folders.Remove(fToDelete);
            }
        }

        await LoadFoldersAsync();
    }

    #endregion
    //--------------

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public async Task LoadFoldersAsync()
    {
        var allFolders = await _database.GetFoldersAsync();

        Folders.Clear();
        FolderRecipes.Clear();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            // Normal mode: navigate through the folder hierarchy
            int? targetParentId = CurrentFolder?.Id;

            foreach (var folder in allFolders.Where(f => f.ParentFolderId == targetParentId))
            {
                Folders.Add(folder);
            }

            List<Recipe> recipesToShow;
            if (targetParentId == null || targetParentId == 0)
                recipesToShow = await _database.GetRecipesWithoutFolderAsync();
            else
                recipesToShow = await _database.GetRecipesInFolderAsync(targetParentId.Value);

            foreach (var recipe in recipesToShow)
            {
                FolderRecipes.Add(recipe);
            }
        }
        else
        {
            // Search mode: flatten the view and search across all folders and recipes
            var filteredFolders = allFolders.Where(f => f.Name != null && f.Name.Contains(SearchQuery)).ToList();
            foreach (var folder in filteredFolders)
            {
                Folders.Add(folder);
            }

            var allRecipes = await _database.GetRecipesAsync();
            var filteredRecipes = allRecipes.Where(r => r.Title != null && r.Title.Contains(SearchQuery)).ToList();
            foreach (var recipe in filteredRecipes)
            {
                FolderRecipes.Add(recipe);
            }
        }
    }

    [RelayCommand]
    public async Task AddFolderAsync()
    {
        string folderName = await Application.Current.MainPage.DisplayPromptAsync(
            "תיקייה חדשה",
            "הכנס שם לתיקייה החדשה:",
            "צור",
            "ביטול");

        if (!string.IsNullOrWhiteSpace(folderName))
        {
            var newFolder = new RecipeFolder
            {
                Name = folderName,
                ParentFolderId = CurrentFolder?.Id
            };

            await _database.SaveFolderAsync(newFolder);
            Folders.Add(newFolder);
        }
    }

    [RelayCommand]
    public void OpenFolder(RecipeFolder folder)
    {
        if (folder == null) return;
        CurrentFolder = folder;
        _ = LoadFoldersAsync();
    }

    [RelayCommand]
    public async Task GoUpAsync()
    {
        if (CurrentFolder == null) return;

        if (CurrentFolder.ParentFolderId == null || CurrentFolder.ParentFolderId == 0)
        {
            CurrentFolder = null;
        }
        else
        {
            var allFolders = await _database.GetFoldersAsync();
            CurrentFolder = allFolders.FirstOrDefault(f => f.Id == CurrentFolder.ParentFolderId);
        }

        await LoadFoldersAsync();
    }

    [RelayCommand]
    public void ToggleView()
    {
        if (GridSpan == 1) GridSpan = 2;
        else if (GridSpan == 2) GridSpan = 3;
        else GridSpan = 1;
    }

    [RelayCommand]
    public async Task RenameFolderAsync(RecipeFolder folder)
    {
        if (folder == null) return;

        string newName = await Application.Current.MainPage.DisplayPromptAsync(
            "שינוי שם תיקייה",
            "הכנס שם חדש:",
            "שמור",
            "ביטול",
            initialValue: folder.Name);

        if (!string.IsNullOrWhiteSpace(newName) && newName != folder.Name)
        {
            folder.Name = newName;
            await _database.SaveFolderAsync(folder);
            await LoadFoldersAsync();
        }
    }

    [RelayCommand]
    public async Task FolderOptionsAsync(RecipeFolder folder)
    {
        if (folder == null) return;

        string action = await Application.Current.MainPage.DisplayActionSheet(
            "אפשרויות תיקייה",
            "ביטול",
            null,
            "שנה שם",
            "מחק",
            "שתף תיקייה");

        if (action == "שתף תיקייה")
        {
            await ShareFolderAsync(folder);
        }

        if (action == "שנה שם")
        {
            await RenameFolderAsync(folder);
        }
        else if (action == "מחק")
        {
            await ExecuteDeleteFolderFlowAsync(folder);
        }
    }

    [RelayCommand]
    public async Task OpenRecipeAsync(Recipe recipe)
    {
        if (recipe == null) return;

        var navParam = new Dictionary<string, object> { { "Recipe", recipe } };
        await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeViewerPage), navParam);
    }

    [RelayCommand]
    public async Task RecipeOptionsAsync(Recipe recipe)
    {
        if (recipe == null) return;

        string action = await Application.Current.MainPage.DisplayActionSheet(
            $"אפשרויות מתכון",
            "ביטול",
            null,
            "ערוך מתכון",
            "ניהול תיקיות",
            "מחק מתכון",
            "שתף מתכון");

        if (action == "שתף מתכון")
        {
            await ShareRecipeAsync(recipe);
        }


        if (action == "ערוך מתכון")
        {
            var navParam = new Dictionary<string, object> { { "RecipeToEdit", recipe } };
            await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeEditorPage), navParam);
        }
        else if (action == "ניהול תיקיות")
        {
            var navParam = new Dictionary<string, object> { { "Recipe", recipe } };
            await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navParam);
        }
        else if (action == "מחק מתכון")
        {
            bool answer = await Application.Current.MainPage.DisplayAlert(
                "מחיקת מתכון",
                $"האם אתה בטוח שברצונך למחוק את '{recipe.Title}'?",
                "כן, מחק",
                "ביטול");

            if (answer)
            {
                await _database.DeleteRecipeAsync(recipe);
                FolderRecipes.Remove(recipe);
            }
        }
    }

    [RelayCommand]
    public async Task OpenAllRecipesAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.SubPages.AllRecipesPage));
    }

    #endregion
    //--------------

    //--------------
    #region Sharing & Importing Logic
    //--------------

    private string EncodePayload(object dto)
    {
        string json = JsonSerializer.Serialize(dto);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        // כיווץ הנתונים (מכווץ תיקיות ענקיות לטקסט קצר)
        using var mso = new MemoryStream();
        using (var gs = new GZipStream(mso, CompressionLevel.Optimal))
        {
            gs.Write(bytes, 0, bytes.Length);
        }

        // המרה לטקסט בטוח לקישורים (URL-Safe Base64) שמונע קריסות
        return Convert.ToBase64String(mso.ToArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private T DecodePayload<T>(string text, string searchKey)
    {
        int startIndex = text.IndexOf(searchKey);
        string payload = text.Substring(startIndex + searchKey.Length).Trim().Split('\n', '\r', ' ')[0];

        // פענוח חזרה מ-URL-Safe
        payload = Uri.UnescapeDataString(payload);
        string base64 = payload.Replace("-", "+").Replace("_", "/");
        int padding = base64.Length % 4;
        if (padding > 0) base64 += new string('=', 4 - padding);

        byte[] compressed = Convert.FromBase64String(base64);

        // חילוץ הכיווץ חזרה לטקסט
        using var msi = new MemoryStream(compressed);
        using var mso = new MemoryStream();
        using (var gs = new GZipStream(msi, CompressionMode.Decompress))
        {
            gs.CopyTo(mso);
        }

        string json = Encoding.UTF8.GetString(mso.ToArray());
        return JsonSerializer.Deserialize<T>(json);
    }

    private async Task ShareRecipeAsync(Recipe recipe)
    {
        var dto = new SharedRecipeDto { T = recipe.Title, D = recipe.Description };

        var ingredients = await _database.GetIngredientsAsync(recipe.Id);
        foreach (var ing in ingredients) dto.I.Add(new SharedIngDto { N = ing.Name, Q = ing.Quantity, U = ing.Unit });

        var steps = await _database.GetStepsAsync(recipe.Id);
        foreach (var step in steps) dto.S.Add(new SharedStepDto { D = step.Description });

        string safeBase64 = EncodePayload(dto);
        string deepLink = $"recipebook://sharerecipe?data={safeBase64}";

        var sb = new StringBuilder();
        sb.AppendLine($"👨‍🍳 *{recipe.Title}*");
        sb.AppendLine("שלחתי לך מתכון מהאפליקציה!");
        sb.AppendLine("📌 *איך לייבא?* העתק את כל ההודעה הזו, כנס לאפליקציית המתכונים ולחץ על 'ייבא מהלוח'.");
        sb.AppendLine();
        sb.AppendLine(deepLink);

        await Share.Default.RequestAsync(new ShareTextRequest { Text = sb.ToString(), Title = "שתף מתכון" });
    }

    private async Task ShareFolderAsync(RecipeFolder folder)
    {
        var dto = new SharedFolderDto { N = folder.Name };
        var recipesInFolder = await _database.GetRecipesInFolderAsync(folder.Id);

        foreach (var r in recipesInFolder)
        {
            var rDto = new SharedRecipeDto { T = r.Title, D = r.Description };
            var ingredients = await _database.GetIngredientsAsync(r.Id);
            var steps = await _database.GetStepsAsync(r.Id);

            foreach (var ing in ingredients) rDto.I.Add(new SharedIngDto { N = ing.Name, Q = ing.Quantity, U = ing.Unit });
            foreach (var step in steps) rDto.S.Add(new SharedStepDto { D = step.Description });
            dto.R.Add(rDto);
        }

        string safeBase64 = EncodePayload(dto);
        string deepLink = $"recipebook://sharefolder?data={safeBase64}";

        var sb = new StringBuilder();
        sb.AppendLine($"📁 *תיקיית מתכונים: {folder.Name}*");
        sb.AppendLine($"כוללת {recipesInFolder.Count} מתכונים שווים!");
        sb.AppendLine("📌 *איך לייבא?* העתק את כל ההודעה הזו, כנס לאפליקציית המתכונים ולחץ על 'ייבא מהלוח'.");
        sb.AppendLine();
        sb.AppendLine(deepLink);

        await Share.Default.RequestAsync(new ShareTextRequest { Text = sb.ToString(), Title = "שתף תיקייה" });
    }

    [RelayCommand]
    public async Task ImportFromClipboardAsync()
    {
        try
        {
            if (!Clipboard.Default.HasText)
            {
                await Application.Current.MainPage.DisplayAlert("שגיאה", "אין טקסט מועתק בלוח.", "אישור");
                return;
            }

            string clipboardText = await Clipboard.Default.GetTextAsync();
            if (string.IsNullOrWhiteSpace(clipboardText)) return;

            if (clipboardText.Contains("recipebook://sharerecipe?data="))
            {
                await ImportRecipeAsync(clipboardText, "recipebook://sharerecipe?data=");
            }
            else if (clipboardText.Contains("recipebook://sharefolder?data="))
            {
                await ImportFolderAsync(clipboardText, "recipebook://sharefolder?data=");
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("לא נמצא קישור", "לא זיהינו מתכון או תיקייה בטקסט שהעתקת. ודא שהעתקת את כל ההודעה.", "אישור");
            }
        }
        catch (Exception)
        {
            await Application.Current.MainPage.DisplayAlert("שגיאה בייבוא", "הטקסט שהעתקת אינו תקין או פגום.", "אישור");
        }
    }

    private async Task ImportRecipeAsync(string text, string searchKey)
    {
        var dto = DecodePayload<SharedRecipeDto>(text, searchKey);
        if (dto != null)
        {
            var newRecipe = await SaveDtoToRecipeAsync(dto);

            await Application.Current.MainPage.DisplayAlert("הצלחה!", $"המתכון '{newRecipe.Title}' קפץ פנימה. בוא נבחר באיזו תיקייה לשמור אותו.", "המשך");

            var navParam = new Dictionary<string, object>
            {
                { "Recipe", newRecipe },
                { "IsFromNewRecipe", false }
            };
            await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navParam);

            await LoadFoldersAsync();
        }
    }

    private async Task ImportFolderAsync(string text, string searchKey)
    {
        var dto = DecodePayload<SharedFolderDto>(text, searchKey);
        if (dto != null)
        {
            var newFolder = new RecipeFolder
            {
                Name = dto.N + " (מיובא)",
                ParentFolderId = CurrentFolder?.Id
            };
            await _database.SaveFolderAsync(newFolder);

            int recipeCount = 0;
            foreach (var rDto in dto.R)
            {
                var newRecipe = await SaveDtoToRecipeAsync(rDto);
                await _database.AddRecipeToFolderAsync(newRecipe.Id, newFolder.Id);
                recipeCount++;
            }

            await Application.Current.MainPage.DisplayAlert("תיקייה יובאה! 🎉", $"התיקייה '{newFolder.Name}' יובאה בהצלחה יחד עם {recipeCount} מתכונים.", "מעולה");
            await LoadFoldersAsync();
        }
    }

    private async Task<Recipe> SaveDtoToRecipeAsync(SharedRecipeDto dto)
    {
        var recipe = new Recipe { Title = dto.T, Description = dto.D };
        await _database.SaveRecipeAsync(recipe);

        for (int i = 0; i < dto.I.Count; i++)
        {
            await _database.SaveIngredientAsync(new Ingredient
            {
                RecipeId = recipe.Id,
                Name = dto.I[i].N,
                Quantity = dto.I[i].Q,
                Unit = dto.I[i].U ?? "יחידות",
                OrderIndex = i
            });
        }

        for (int i = 0; i < dto.S.Count; i++)
        {
            await _database.SaveStepAsync(new RecipeStep
            {
                RecipeId = recipe.Id,
                Description = dto.S[i].D,
                StepNumber = i + 1
            });
        }
        return recipe;
    }
    #endregion
    //--------------
}


// -------------------------------------------------------------------------
#region Data Transfer Objects (DTOs) for Recipe & Folder Sharing
// -------------------------------------------------------------------------

public class SharedRecipeDto
{
    public string T { get; set; }
    public string D { get; set; }
    public List<SharedIngDto> I { get; set; } = new();
    public List<SharedStepDto> S { get; set; } = new();
}

public class SharedIngDto
{
    public string N { get; set; }
    public double? Q { get; set; }
    public string U { get; set; }
}

public class SharedStepDto
{
    public string D { get; set; }
}

public class SharedFolderDto
{
    public string N { get; set; }
    public List<SharedRecipeDto> R { get; set; } = new();
}

#endregion
// -------------------------------------------------------------------------