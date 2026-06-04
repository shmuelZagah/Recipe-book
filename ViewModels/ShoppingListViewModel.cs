using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Firebase.Firestore;
using Recipe_book.Helpers;
using Recipe_book.Models.Cloud;
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

    //------------------------------
    #region UI Properties
    //------------------------------

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

    public ObservableCollection<AbstractShoppingList> AbstractLists { get; } = new();
    private int? _selectedAbstractListIdForCreation;
    [ObservableProperty] private bool isEditingTemplateMode = false;
    [ObservableProperty] private AbstractShoppingList currentAbstractTemplate;

    private IDisposable _cloudListener;
    private bool _isSyncingFromCloud = false;

    #endregion
    //------------------------------

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

        var abstractLists = await _database.GetAbstractShoppingListsAsync();
        AbstractLists.Clear();
        foreach (var aList in abstractLists) AbstractLists.Add(aList);
    }

    [RelayCommand] public void ToggleListsMenu() => IsListsMenuOpen = !IsListsMenuOpen;

    
    [RelayCommand]
    public async Task SwitchListAsync(SavedShoppingList selectedList)
    {
        if (selectedList == null) return;
        IsEditingTemplateMode = false;
        CurrentShoppingList = selectedList;
        IsListsMenuOpen = false; HasValidList = true;
        UpdateStatusText();

        _cloudListener?.Dispose();
        _cloudListener = null;

        await GenerateListAsync();

        if (CurrentShoppingList.IsShared && !string.IsNullOrEmpty(CurrentShoppingList.CloudId))
        {
            var firestore = new FirestoreService();
            _cloudListener = firestore.ListenToSharedListItems(CurrentShoppingList.CloudId, OnCloudListItemsUpdated);
        }
    }


    /// <summary>
    /// Implements surgical Smart UI Merging using strict Value Comparison.
    /// Updates only changed properties directly in the UI without full reloads, 
    /// completely eliminating screen flickering and duplication loops under heavy load.
    /// </summary>
    private void OnCloudListItemsUpdated(IEnumerable<SharedCloudItemDto> cloudItems)
    {
        if (cloudItems == null || CurrentShoppingList == null || CurrentShoppingList.Id == -1) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _isSyncingFromCloud = true; // Prevents triggering local PropertyChanged events which cause echo uploads

            try
            {
                // 1. Sync the local SQLite database silently
                var flatList = new List<SavedShoppingListItem>();
                foreach (var cloudItem in cloudItems)
                {
                    string n = cloudItem.N ?? "";
                    string u = string.IsNullOrWhiteSpace(cloudItem.U) ? "יחידות" : cloudItem.U;
                    string c = string.IsNullOrWhiteSpace(cloudItem.C) ? "כללי" : cloudItem.C;

                    string displayUnit = u == "יחידות" ? "" : u;
                    string displayTxt = string.IsNullOrWhiteSpace(displayUnit) ? $"{cloudItem.Q} {n}" : $"{cloudItem.Q} {displayUnit} {n}";

                    flatList.Add(new SavedShoppingListItem
                    {
                        ListId = CurrentShoppingList.Id,
                        Name = n,
                        Quantity = cloudItem.Q,
                        Unit = displayUnit,
                        Category = c,
                        DisplayText = displayTxt, // התיקון: שומרים את הטקסט כדי שלא יימחק מהמסד!
                        IsBought = cloudItem.IsBought
                    });
                }

                await _database.SaveStaticShoppingListItemsAsync(CurrentShoppingList.Id, flatList);

                // 2. Surgical UI Merge: Compare values and update existing objects
                var allUiItems = GroupedShoppingItems.SelectMany(g => g).ToList();

                foreach (var cloudItem in cloudItems)
                {
                    var existingUiItem = allUiItems.FirstOrDefault(i => i.Name == cloudItem.N);

                    string n = cloudItem.N ?? "";
                    string u = string.IsNullOrWhiteSpace(cloudItem.U) ? "יחידות" : cloudItem.U;
                    string c = string.IsNullOrWhiteSpace(cloudItem.C) ? "כללי" : cloudItem.C;
                    string displayUnit = u == "יחידות" ? "" : u;
                    string displayTxt = string.IsNullOrWhiteSpace(displayUnit) ? $"{cloudItem.Q} {n}" : $"{cloudItem.Q} {displayUnit} {n}";

                    if (existingUiItem != null)
                    {
                        bool isChanged = false;

                        if (existingUiItem.Quantity != cloudItem.Q)
                        {
                            existingUiItem.Quantity = cloudItem.Q;
                            isChanged = true;
                        }

                        if (existingUiItem.IsBought != cloudItem.IsBought)
                        {
                            existingUiItem.IsBought = cloudItem.IsBought;
                            isChanged = true;
                        }

                        if (isChanged)
                        {
                            existingUiItem.DisplayText = displayTxt; // מעדכן ישירות את השדה
                            existingUiItem.UpdateDisplayText();
                        }

                        allUiItems.Remove(existingUiItem);
                    }
                    else
                    {
                        // New item added from cloud: Inject surgically without full reload
                        var newItem = new SavedShoppingListItem
                        {
                            ListId = CurrentShoppingList.Id,
                            Name = n,
                            Quantity = cloudItem.Q,
                            Unit = displayUnit,
                            Category = c,
                            DisplayText = displayTxt, // התיקון: דואג שהטקסט יופיע מיד על המסך!
                            IsBought = cloudItem.IsBought
                        };
                        newItem.UpdateDisplayText();

                        // Attach listener just like GenerateListAsync does
                        newItem.PropertyChanged += async (s, e) =>
                        {
                            if (e.PropertyName == nameof(SavedShoppingListItem.IsBought))
                            {
                                await _database.SaveShoppingListItemAsync(newItem);
                                if (!_isSyncingFromCloud && CurrentShoppingList.IsShared)
                                {
                                    var firestore = new FirestoreService();
                                    var dto = new SharedCloudItemDto
                                    {
                                        DocumentId = newItem.Name.Replace("/", "_"),
                                        N = newItem.Name,
                                        Q = newItem.Quantity,
                                        U = newItem.Unit,
                                        C = newItem.Category,
                                        IsBought = newItem.IsBought
                                    };
                                    await firestore.UpdateSharedListItemAsync(CurrentShoppingList.CloudId, dto);
                                }
                            }
                        };

                        var targetGroup = GroupedShoppingItems.FirstOrDefault(g => g.CategoryName == c);
                        if (targetGroup != null)
                        {
                            targetGroup.Add(newItem);
                        }
                        else
                        {
                            GroupedShoppingItems.Add(new ShoppingItemGroup(c, new[] { newItem }));
                        }
                    }
                }

                // 3. Process deletions: items left in 'allUiItems' no longer exist in the cloud
                foreach (var deletedItem in allUiItems)
                {
                    var group = GroupedShoppingItems.FirstOrDefault(g => g.CategoryName == deletedItem.Category);
                    if (group != null)
                    {
                        group.Remove(deletedItem);
                        if (group.Count == 0)
                        {
                            GroupedShoppingItems.Remove(group);
                        }
                    }
                }
            }
            finally
            {
                _isSyncingFromCloud = false;
            }
        });
    }


    [RelayCommand]
    public async Task SwitchToTemplateEditAsync(AbstractShoppingList template)
    {
        if (template == null) return;
        CurrentAbstractTemplate = template;
        IsEditingTemplateMode = true;
        CurrentShoppingList = new SavedShoppingList { Title = template.Title, Id = -2 };

        IsListsMenuOpen = false;
        HasValidList = true;
        UpdateStatusText();
        await GenerateListAsync();
    }

    private void UpdateStatusText()
    {
        if (IsEditingTemplateMode)
        {
            StatusText = "מצב עריכת שלד בסיסי (השינויים יישמרו בתבנית)";
            return;
        }

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
        _selectedAbstractListIdForCreation = null;
        IsListsMenuOpen = false; IsEmptyListTemp = false; SelectedRangeType = DateRangeType.Week;
        UpdateVisibility();
        IsHeaderOpen = true;
    }

    [RelayCommand]
    public async Task CreateListFromTemplateAsync(AbstractShoppingList template)
    {
        if (template == null) return;

        string listName = await Application.Current.MainPage.DisplayPromptAsync(
            "רשימה חדשה מתבנית", $"איך תרצה לקרוא לרשימה המבוססת על '{template.Title}'?", "המשך", "ביטול", $"{template.Title} - מתוכנן");
        if (string.IsNullOrWhiteSpace(listName)) return;

        _pendingNewListName = listName;
        _selectedAbstractListIdForCreation = template.Id;
        IsListsMenuOpen = false; IsEmptyListTemp = false; SelectedRangeType = DateRangeType.Week;
        UpdateVisibility();
        IsHeaderOpen = true;
    }

    [RelayCommand]
    public async Task CreateNewTemplateAsync()
    {
        string templateName = await Application.Current.MainPage.DisplayPromptAsync("שלד רשימה חדש", "הזן שם עבור תבנית הרשימה האבסטרקטית:", "צור", "ביטול");
        if (string.IsNullOrWhiteSpace(templateName)) return;

        var newTemplate = new AbstractShoppingList
        {
            Title = templateName.Trim(),
            CreatedAt = DateTime.Now
        };

        await _database.SaveAbstractShoppingListAsync(newTemplate);
        await LoadAllListsAsync();

        await SwitchToTemplateEditAsync(newTemplate);
    }

    [RelayCommand]
    public async Task DeleteTemplateAsync(AbstractShoppingList templateToDelete)
    {
        if (templateToDelete == null) return;
        if (await Application.Current.MainPage.DisplayAlert("מחיקת שלד", $"האם אתה בטוח שברצונך למחוק את תבנית השלד '{templateToDelete.Title}'?", "כן, מחק", "ביטול"))
        {
            await _database.DeleteAbstractShoppingListAsync(templateToDelete);
            await LoadAllListsAsync();
        }
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

        if (!IsEmptyListTemp || _selectedAbstractListIdForCreation.HasValue)
        {
            IsLoading = true; LoadingText = "מחלץ וממזג מצרכים...";
            try
            {
                // Delegate compilation payload build out to service with option abstract blueprint injection
                var compiledFlatSnapshot = await _builderService.BuildIngredientsFromScheduleAsync(
                    newList.Id,
                    IsEmptyListTemp ? null : targetStartDate,
                    IsEmptyListTemp ? null : targetEndDate,
                    _selectedAbstractListIdForCreation);

                await _database.SaveStaticShoppingListItemsAsync(newList.Id, compiledFlatSnapshot);
            }
            finally
            {
                IsLoading = false;
                _selectedAbstractListIdForCreation = null; // Flush assignment state pipeline context
            }
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

        if (IsEditingTemplateMode)
        {
            if (CurrentAbstractTemplate == null) return;
            var templateItems = await _database.GetItemsForAbstractListAsync(CurrentAbstractTemplate.Id);

            // Map Abstract items to SavedShoppingListItems so the XAML GUI requires zero configuration changes
            var mappedItems = templateItems.Select(i =>
            {
                var item = new SavedShoppingListItem
                {
                    Id = i.Id,
                    ListId = i.ListId,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Category = i.Category,
                    IsBought = false
                };

                item.UpdateDisplayText();

                return item;
            }).ToList();

            var grouped = mappedItems.GroupBy(x => x.Category).Select(g => new ShoppingItemGroup(g.Key, g)).OrderBy(g => g.CategoryName);
            foreach (var group in grouped) GroupedShoppingItems.Add(group);
            return;
        }

        if (CurrentShoppingList == null || CurrentShoppingList.Id == -1) return;
        var items = await _database.GetItemsForShoppingListAsync(CurrentShoppingList.Id);
        var aggregatedGrouped = items.GroupBy(x => x.Category).Select(g => new ShoppingItemGroup(g.Key, g)).OrderBy(g => g.CategoryName);

        foreach (var group in aggregatedGrouped)
        {
            foreach (var item in group)
            {
                item.PropertyChanged += async (s, e) =>
                {
                    if (e.PropertyName == nameof(SavedShoppingListItem.IsBought))
                    {
                        await _database.SaveShoppingListItemAsync(item);

                        if (!_isSyncingFromCloud && CurrentShoppingList.IsShared)
                        {
                            var firestore = new FirestoreService();
                            var dto = new SharedCloudItemDto
                            {
                                DocumentId = item.Name.Replace("/", "_"),
                                N = item.Name,
                                Q = item.Quantity,
                                U = item.Unit,
                                C = item.Category,
                                IsBought = item.IsBought
                            };
                            await firestore.UpdateSharedListItemAsync(CurrentShoppingList.CloudId, dto);
                        }
                    }
                };
            }
            GroupedShoppingItems.Add(group);
        }
    }

    [RelayCommand]
    public async Task AddManualItemAsync()
    {
        if (!IsEditingTemplateMode && (CurrentShoppingList == null || CurrentShoppingList.Id == -1)) return;
        if (IsEditingTemplateMode && CurrentAbstractTemplate == null) return;

        string itemName = await Application.Current.MainPage.DisplayPromptAsync("הוספת מצרך", "מה תרצה להוסיף?", "המשך", "ביטול");
        if (string.IsNullOrWhiteSpace(itemName)) return;
        itemName = itemName.Trim();

        string quantityStr = await Application.Current.MainPage.DisplayPromptAsync("כמות", $"כמה {itemName} להוסיף?", "הוסף", "ביטול", keyboard: Keyboard.Numeric);
        if (!double.TryParse(quantityStr, out double addedQty) || addedQty <= 0) return;

        string finalCategory = null;
        var itemVariations = TextHelpers.GetPossibleSingulars(itemName);

        if (IsEditingTemplateMode)
        {
            var templateItems = await _database.GetItemsForAbstractListAsync(CurrentAbstractTemplate.Id);
            var match = templateItems.FirstOrDefault(i => itemVariations.Intersect(TextHelpers.GetPossibleSingulars(i.Name)).Any() && (string.IsNullOrWhiteSpace(i.Unit) || i.Unit == "יחידות"));
            if (match != null)
            {
                match.Quantity += addedQty;
                match.UpdateDisplayText();
                await _database.SaveAbstractShoppingListItemAsync(match);
                await GenerateListAsync();
                return;
            }
        }
        else
        {
            var currentItems = await _database.GetItemsForShoppingListAsync(CurrentShoppingList.Id);
            var match = currentItems.FirstOrDefault(i => itemVariations.Intersect(TextHelpers.GetPossibleSingulars(i.Name)).Any() && (string.IsNullOrWhiteSpace(i.Unit) || i.Unit == "יחידות"));
            if (match != null)
            {
                match.Quantity += addedQty;
                match.UpdateDisplayText();
                await _database.SaveShoppingListItemAsync(match);

                if (CurrentShoppingList.IsShared)
                {
                    var firestore = new FirestoreService();
                    var dto = new SharedCloudItemDto
                    {
                        DocumentId = match.Name.Replace("/", "_"),
                        N = match.Name,
                        Q = match.Quantity,
                        U = match.Unit,
                        C = match.Category,
                        IsBought = match.IsBought
                    };
                    await firestore.UpdateSharedListItemAsync(CurrentShoppingList.CloudId, dto);
                }

                await GenerateListAsync();
                return;
            }
        }

        var conversions = await _database.GetIngredientConversionsAsync();
        var convMatch = conversions.FirstOrDefault(c => itemVariations.Contains(c.Keyword));

        if (convMatch != null && !string.IsNullOrWhiteSpace(convMatch.Category))
        {
            finalCategory = convMatch.Category;
        }
        else
        {
            string selectedCategory = await Application.Current.MainPage.DisplayActionSheet($"לאיזו מחלקה שייך '{itemName}'?", "דלג", null, AppConstants.ShoppingCategories);
            finalCategory = (selectedCategory == "דלג" || string.IsNullOrEmpty(selectedCategory)) ? "כללי" : selectedCategory;
        }

        if (IsEditingTemplateMode)
        {
            var newItem = new AbstractShoppingListItem
            {
                ListId = CurrentAbstractTemplate.Id,
                Name = itemName,
                Quantity = addedQty,
                Unit = "יחידות",
                Category = finalCategory
            };
            newItem.UpdateDisplayText();
            await _database.SaveAbstractShoppingListItemAsync(newItem);
        }
        else
        {
            var newItem = new SavedShoppingListItem
            {
                ListId = CurrentShoppingList.Id,
                Name = itemName,
                Quantity = addedQty,
                Unit = "יחידות",
                Category = finalCategory,
                IsBought = false
            };
            newItem.UpdateDisplayText();
            await _database.SaveShoppingListItemAsync(newItem);

            if (CurrentShoppingList != null && CurrentShoppingList.IsShared)
            {
                var authService = IPlatformApplication.Current.Services.GetService<IFirebaseAuthService>();
                var firestore = new FirestoreService();
                var dto = new SharedCloudItemDto
                {
                    DocumentId = newItem.Name.Replace("/", "_"),
                    N = newItem.Name,
                    Q = newItem.Quantity,
                    U = newItem.Unit,
                    C = newItem.Category,
                    IsBought = newItem.IsBought,
                    LastActionBy = authService?.GetCurrentUserId()
                };
                await firestore.UpdateSharedListItemAsync(CurrentShoppingList.CloudId, dto);
            }
        }

        await GenerateListAsync();
    }

    [RelayCommand]
    public async Task DeleteListAsync(SavedShoppingList listToDelete)
    {
        if (listToDelete == null) return;
        if (await Application.Current.MainPage.DisplayAlert("מחיקת רשימה", $"האם אתה בטוח שברצונך למחוק את '{listToDelete.Title}'?", "כן, מחק", "ביטול"))
        {
            if (listToDelete.IsShared && !string.IsNullOrEmpty(listToDelete.CloudId))
            {
                try
                {
                    var firestore = new FirestoreService();
                    var cloudList = await firestore.GetSharedListFromCloudAsync(listToDelete.CloudId);

                    if (cloudList != null)
                    {
                        var authService = IPlatformApplication.Current.Services.GetService<IFirebaseAuthService>();
                        string currentUid = authService?.GetCurrentUserId();

                        if (!string.IsNullOrEmpty(currentUid) && cloudList.PartnerUids.Contains(currentUid))
                        {
                            cloudList.PartnerUids.Remove(currentUid);

                            if (cloudList.PartnerUids.Count == 0)
                            {
                                await firestore.DeleteSharedListFromCloudAsync(listToDelete.CloudId);
                            }
                            else
                            {
                                await firestore.UpdateSharedListMetadataAsync(cloudList);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling cloud deletion: {ex.Message}");
                }
            }

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
            {
                foreach (var item in group)
                {
                    if (item.IsBought)
                    {
                        item.IsBought = false; 
                    }
                }
            }
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
            await _actionService.ExecuteSharePipelineAsync(CurrentShoppingList, GroupedShoppingItems, shareOption);

            // Attaches the real-time cloud listener for the creator immediately after sharing
            if (CurrentShoppingList.IsShared)
            {
                await SwitchListAsync(CurrentShoppingList);
            }
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
