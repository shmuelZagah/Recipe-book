using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recipe_book.Helpers;
using Recipe_book.Models.Enums;
using Recipe_book.Models.Shopping;
using Recipe_book.Services;
using Recipe_book.Services.Shopping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

public partial class ShoppingListViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;
    private readonly ShoppingListBuilderService _builderService;
    private readonly ShoppingListActionService _actionService;
    private string _pendingNewListName;

    #region UI Properties
    public static int? PendingImportId { get; set; }
    public static Action RefreshActivePage;

    [ObservableProperty] private string emptyViewText = "אין מצרכים ברשימה זו";
    [ObservableProperty] private SavedShoppingList currentShoppingList;
    public ObservableCollection<SavedShoppingList> SavedLists { get; } = new();
    public ObservableCollection<ShoppingItemGroup> GroupedShoppingItems { get; } = new();
    public ObservableCollection<SelectableListDto> MergeableLists { get; } = new();

    [ObservableProperty] private bool isListsMenuOpen = false;
    [ObservableProperty] private bool isLoading = false;
    [ObservableProperty] private string loadingText = String.Empty;
    [ObservableProperty] private bool isMergeModeActive = false;
    [ObservableProperty] private bool hasValidList = true;
    [ObservableProperty] private string statusText;

    // Wizard Panel State Properties
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsHeaderClosed))] private bool isHeaderOpen = false;
    public bool IsHeaderClosed => !IsHeaderOpen;

    [ObservableProperty] private DateRangeType selectedRangeType = DateRangeType.Week;
    [ObservableProperty] private bool isCustomRollingVisible;
    [ObservableProperty] private bool isSpecificDatesVisible;
    [ObservableProperty] private int customOffsetDays = 0;
    [ObservableProperty] private int customDurationDays = 7;
    [ObservableProperty] private DateTime specificStartDate = DateTime.Today;
    [ObservableProperty] private DateTime specificEndDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private bool isEmptyListTemp = false;
    #endregion

    public ShoppingListViewModel(RecipesDatabase database, ShoppingListBuilderService builderService, ShoppingListActionService actionService)
    {
        _database = database;
        _builderService = builderService;
        _actionService = actionService;

        RefreshActivePage = () => { MainThread.BeginInvokeOnMainThread(async () => await InitializeAutoLoadAsync()); };
    }

    #region Auto Load & Base List Switch
    public async Task InitializeAutoLoadAsync()
    {
        await LoadAllListsAsync();
        if (SavedLists.Count == 0)
        {
            CurrentShoppingList = new SavedShoppingList { Title = "אין רשימות כרגע", Id = -1 };
            HasValidList = false; EmptyViewText = ""; GroupedShoppingItems.Clear();
            StatusText = "לא קיימות רשימות. לחץ על ה- + ליצירת רשימה חדשה.";
            return;
        }

        HasValidList = true; EmptyViewText = "אין מצרכים ברשימה זו";

        if (PendingImportId.HasValue)
        {
            var targetList = SavedLists.FirstOrDefault(l => l.Id == PendingImportId.Value);
            PendingImportId = null;
            if (targetList != null) { await SwitchListAsync(targetList); return; }
        }

        if (CurrentShoppingList == null || CurrentShoppingList.Id == -1) CurrentShoppingList = SavedLists.First();
        await SwitchListAsync(CurrentShoppingList);
    }

    public async Task LoadAllListsAsync()
    {
        var lists = await _database.GetSavedShoppingListsAsync();
        SavedLists.Clear();
        foreach (var list in lists) SavedLists.Add(list);
    }

    [RelayCommand] public void ToggleListsMenu() => IsListsMenuOpen = !IsListsMenuOpen;

    [RelayCommand]
    public async Task SwitchListAsync(SavedShoppingList selectedList)
    {
        if (selectedList == null) return;
        CurrentShoppingList = selectedList;
        IsListsMenuOpen = false; HasValidList = true;
        UpdateStatusText();
        await GenerateListAsync();
    }

    private void UpdateStatusText()
    {
        if (CurrentShoppingList == null) { StatusText = ""; return; }
        if (CurrentShoppingList.StartDate.HasValue && CurrentShoppingList.EndDate.HasValue)
            StatusText = $"לתאריכים: {CurrentShoppingList.StartDate.Value:dd.MM} - {CurrentShoppingList.EndDate.Value:dd.MM}";
        else
            StatusText = "רשימה קבועה (ללא תאריכים)";
    }
    #endregion

    #region Creation Wizard Navigation
    [RelayCommand]
    public void SelectRangeType(string rangeTypeStr)
    {
        if (Enum.TryParse(typeof(DateRangeType), rangeTypeStr, out var parsedRange))
        {
            SelectedRangeType = (DateRangeType)parsedRange;
            IsEmptyListTemp = false; UpdateVisibility();
        }
    }

    [RelayCommand]
    public void ToggleEmptyListMode()
    {
        IsEmptyListTemp = true; SelectedRangeType = (DateRangeType)(-1); UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        IsCustomRollingVisible = SelectedRangeType == DateRangeType.CustomRolling;
        IsSpecificDatesVisible = SelectedRangeType == DateRangeType.SpecificDates;
    }

    [RelayCommand] public void CancelSelection() => IsHeaderOpen = false;

    [RelayCommand]
    public async Task CreateNewListAsync()
    {
        string listName = await Application.Current.MainPage.DisplayPromptAsync("רשימה חדשה", "איך תרצה לקרוא לרשימה?", "המשך", "ביטול");
        if (string.IsNullOrWhiteSpace(listName)) return;

        _pendingNewListName = listName;
        IsListsMenuOpen = false; IsEmptyListTemp = false; SelectedRangeType = DateRangeType.Week;
        UpdateVisibility();
        IsHeaderOpen = true;
    }

    [RelayCommand]
    public async Task ApplyAndCloseAsync()
    {
        if (SelectedRangeType == DateRangeType.SpecificDates && SpecificStartDate > SpecificEndDate)
        {
            await Application.Current.MainPage.DisplayAlert("שגיאה", "תאריך ההתחלה חייב להיות לפני תאריך הסיום.", "אישור");
            return;
        }

        IsHeaderOpen = false;
        DateTime? targetStartDate = DateTime.Today; DateTime? targetEndDate = DateTime.Today;

        if (IsEmptyListTemp) { targetStartDate = null; targetEndDate = null; }
        else
        {
            switch (SelectedRangeType)
            {
                case DateRangeType.Day: targetEndDate = DateTime.Today; break;
                case DateRangeType.Week: targetEndDate = DateTime.Today.AddDays(7); break;
                case DateRangeType.TwoWeeks: targetEndDate = DateTime.Today.AddDays(14); break;
                case DateRangeType.NextWeek: targetStartDate = DateTime.Today.AddDays(7); targetEndDate = DateTime.Today.AddDays(14); break;
                case DateRangeType.Month: targetEndDate = DateTime.Today.AddDays(30); break;
                case DateRangeType.CustomRolling: targetStartDate = DateTime.Today.AddDays(CustomOffsetDays); targetEndDate = targetStartDate.Value.AddDays(CustomDurationDays); break;
                case DateRangeType.SpecificDates: targetStartDate = SpecificStartDate.Date; targetEndDate = SpecificEndDate.Date; break;
            }
        }

        var newList = new SavedShoppingList { Title = _pendingNewListName, StartDate = targetStartDate, EndDate = targetEndDate, CreatedAt = DateTime.Now };
        await _database.SaveShoppingListAsync(newList);

        if (!IsEmptyListTemp && targetStartDate.HasValue && targetEndDate.HasValue)
        {
            IsLoading = true; LoadingText = "מחלץ מצרכים מלוח הארוחות...";
            try
            {
                // Delegate core compilation engine heavy lifting to the Builder Service
                var compiledFlatSnapshot = await _builderService.BuildIngredientsFromScheduleAsync(newList.Id, targetStartDate.Value, targetEndDate.Value);
                await _database.SaveStaticShoppingListItemsAsync(newList.Id, compiledFlatSnapshot);
            }
            finally { IsLoading = false; }
        }

        await LoadAllListsAsync();
        await SwitchListAsync(newList);
    }
    #endregion

    #region Standard Maintenance Commands
    [RelayCommand]
    public async Task GenerateListAsync()
    {
        GroupedShoppingItems.Clear();
        if (CurrentShoppingList == null || CurrentShoppingList.Id == -1) return;

        var items = await _database.GetItemsForShoppingListAsync(CurrentShoppingList.Id);
        var grouped = items.GroupBy(x => x.Category).Select(g => new ShoppingItemGroup(g.Key, g)).OrderBy(g => g.CategoryName);

        foreach (var group in grouped)
        {
            foreach (var item in group)
            {
                item.PropertyChanged += async (s, e) =>
                {
                    if (e.PropertyName == nameof(SavedShoppingListItem.IsBought)) await _database.SaveShoppingListItemAsync(item);
                };
            }
            GroupedShoppingItems.Add(group);
        }
    }

    [RelayCommand]
    public async Task AddManualItemAsync()
    {
        if (CurrentShoppingList == null || CurrentShoppingList.Id == -1) return;
        string itemName = await Application.Current.MainPage.DisplayPromptAsync("הוספת מצרך", "מה תרצה להוסיף לרשימה?", "המשך", "ביטול");
        if (string.IsNullOrWhiteSpace(itemName)) return;
        itemName = itemName.Trim();

        string quantityStr = await Application.Current.MainPage.DisplayPromptAsync("כמות", $"כמה {itemName} להוסיף?", "הוסף", "ביטול", keyboard: Keyboard.Numeric);
        if (!double.TryParse(quantityStr, out double addedQty) || addedQty <= 0) return;

        var existingItems = await _database.GetItemsForShoppingListAsync(CurrentShoppingList.Id);
        var variations = TextHelpers.GetPossibleSingulars(itemName);
        SavedShoppingListItem match = existingItems.FirstOrDefault(item => variations.Intersect(TextHelpers.GetPossibleSingulars(item.Name?.Trim() ?? "")).Any() && (string.IsNullOrWhiteSpace(item.Unit) || item.Unit == "יחידות"));

        if (match != null)
        {
            match.Quantity += addedQty; match.IsBought = false; match.UpdateDisplayText();
            await _database.SaveShoppingListItemAsync(match);
        }
        else
        {
            string selectedCategory = await Application.Current.MainPage.DisplayActionSheet($"לאיזו מחלקה שייך '{itemName}'?", "דלג", null, AppConstants.ShoppingCategories);
            string finalCategory = (selectedCategory == "דלג" || string.IsNullOrEmpty(selectedCategory)) ? "כללי" : selectedCategory;

            var newItem = new SavedShoppingListItem { ListId = CurrentShoppingList.Id, Name = itemName, Quantity = addedQty, Unit = "יחידות", Category = finalCategory, IsBought = false };
            newItem.UpdateDisplayText();
            await _database.SaveShoppingListItemAsync(newItem);
        }
        await GenerateListAsync();
    }

    [RelayCommand]
    public async Task DeleteListAsync(SavedShoppingList listToDelete)
    {
        if (listToDelete == null) return;
        if (await Application.Current.MainPage.DisplayAlert("מחיקת רשימה", $"האם אתה בטוח שברצונך למחוק את '{listToDelete.Title}'?", "כן, מחק", "ביטול"))
        {
            await _database.DeleteShoppingListAsync(listToDelete);
            await LoadAllListsAsync();
            if (CurrentShoppingList?.Id == listToDelete.Id) await InitializeAutoLoadAsync();
        }
    }

    [RelayCommand]
    public async Task ClearBoughtItemsAsync()
    {
        if (await Application.Current.MainPage.DisplayAlert("איפוס קניות", "האם אתה בטוח שברצונך לנקות את כל הסימונים מהרשימה?", "כן, נקה הכל", "ביטול"))
        {
            foreach (var group in GroupedShoppingItems)
                foreach (var item in group) if (item.IsBought) item.IsBought = false;
        }
    }

    [RelayCommand]
    public async Task RenameListAsync(SavedShoppingList listToRename)
    {
        if (listToRename == null) return;
        string newName = await Application.Current.MainPage.DisplayPromptAsync("שינוי שם רשימה", "הזן שם חדש לרשימה:", "שמור", "ביטול", listToRename.Title, maxLength: 40);
        if (!string.IsNullOrWhiteSpace(newName) && newName != listToRename.Title)
        {
            listToRename.Title = newName.Trim();
            await _database.SaveShoppingListAsync(listToRename);
            if (CurrentShoppingList?.Id == listToRename.Id) OnPropertyChanged(nameof(CurrentShoppingList));
            await LoadAllListsAsync();
        }
    }
    #endregion

    #region Merge Commands Logic
    [RelayCommand]
    public void OpenMergeMode()
    {
        MergeableLists.Clear();
        foreach (var list in SavedLists) MergeableLists.Add(new SelectableListDto { List = list, IsSelected = false });
        IsListsMenuOpen = false; IsMergeModeActive = true;
    }

    [RelayCommand] public void CancelMerge() => IsMergeModeActive = false;

    [RelayCommand]
    public async Task ConfirmMergeAsync()
    {
        var selectedLists = MergeableLists.Where(l => l.IsSelected).Select(l => l.List).ToList();
        if (selectedLists.Count < 2) { await Application.Current.MainPage.DisplayAlert("שגיאה", "יש לבחור לפחות 2 רשימות למיזוג.", "אישור"); return; }

        string newListName = await Application.Current.MainPage.DisplayPromptAsync("רשימה ממוזגת", "איך לקרוא לרשימה הממוזגת החדשה?", "המשך", "ביטול");
        if (string.IsNullOrWhiteSpace(newListName)) return;

        IsMergeModeActive = false;
        var newList = new SavedShoppingList { Title = newListName, CreatedAt = DateTime.Now };
        await _database.SaveShoppingListAsync(newList);

        // Delegate static collection flattening and merging out to Action Service
        await _actionService.MergeListsAsync(newList.Id, selectedLists);

        await LoadAllListsAsync();
        await SwitchListAsync(newList);
        await Application.Current.MainPage.DisplayAlert("הצלחה!", "הרשימות מוזגו בהצלחה לרשימה אחת מאוחדת וקבועה.", "מעולה");
    }
    #endregion

    #region Share Pipeline Delegation
    [RelayCommand]
    public async Task ShareListAsync()
    {
        if (GroupedShoppingItems == null || !GroupedShoppingItems.Any()) { await Application.Current.MainPage.DisplayAlert("רשימה ריקה", "אין מצרכים ברשימה לשתף.", "אישור"); return; }
        string shareOption = await Application.Current.MainPage.DisplayActionSheet("איך תרצה לשתף את הרשימה?", "ביטול", null, "טקסט רגיל", "קישור לאפליקציה");
        if (shareOption == "ביטול" || string.IsNullOrEmpty(shareOption)) return;

        IsLoading = true; LoadingText = "מכין שיתוף...";
        try
        {
            // Delegate external integrations pipeline out to Action Service
            await _actionService.ExecuteSharePipelineAsync(CurrentShoppingList, GroupedShoppingItems, shareOption);
        }
        finally { IsLoading = false; }
    }
    #endregion
}



public partial class SelectableListDto : ObservableObject
{
    public SavedShoppingList List { get; set; }

    [ObservableProperty]
    private bool isSelected;
}

public class SharedListDto
{
    public string T { get; set; }
    public List<SharedItemDto> I { get; set; } = new();
    public List<SharedConversionDto> C { get; set; } = new();
}

public class SharedItemDto
{
    public string N { get; set; }
    public double Q { get; set; }
    public string U { get; set; }
    public string C { get; set; }
}

public class SharedConversionDto
{
    public string K { get; set; }
    public string B { get; set; }
    public double A { get; set; }
    public string C { get; set; }
}