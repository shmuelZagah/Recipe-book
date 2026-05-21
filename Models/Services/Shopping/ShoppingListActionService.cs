using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Recipe_book.Helpers;
using Recipe_book.Models.Cloud;
using Recipe_book.Models.Shopping;
using Recipe_book.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Recipe_book.Services.Shopping;

public class ShoppingListActionService
{
    private readonly RecipesDatabase _database;

    public ShoppingListActionService(RecipesDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Processes and saves a new combined static shopping list aggregated from multiple existing source lists.
    /// </summary>
    public async Task MergeListsAsync(int newListId, List<SavedShoppingList> sourceLists)
    {
        var aggregatedItems = new List<SavedShoppingListItem>();
        string NormalizeUnit(string u) => string.IsNullOrWhiteSpace(u) || u.Trim() == "יחידות" ? "יחידות" : u.Trim();

        foreach (var list in sourceLists)
        {
            var items = await _database.GetItemsForShoppingListAsync(list.Id);

            foreach (var item in items)
            {
                string cleanName = item.Name?.Trim() ?? "";
                string cleanUnit = NormalizeUnit(item.Unit);

                var variations = TextHelpers.GetPossibleSingulars(cleanName);
                SavedShoppingListItem match = null;

                foreach (var aggItem in aggregatedItems)
                {
                    var aggVariations = TextHelpers.GetPossibleSingulars(aggItem.Name);
                    if (NormalizeUnit(aggItem.Unit) == cleanUnit && variations.Intersect(aggVariations).Any())
                    {
                        match = aggItem;
                        break;
                    }
                }

                if (match != null)
                {
                    match.Quantity += item.Quantity;
                }
                else
                {
                    aggregatedItems.Add(new SavedShoppingListItem
                    {
                        ListId = newListId,
                        Name = cleanName,
                        Quantity = item.Quantity,
                        Unit = cleanUnit,
                        Category = string.IsNullOrWhiteSpace(item.Category) ? "כללי" : item.Category,
                        IsBought = false
                    });
                }
            }
        }

        await _database.SaveStaticShoppingListItemsAsync(newListId, aggregatedItems);
    }

    /// <summary>
    /// Formats the layout parameters and dispatches a plain text or cloud link sheet share request.
    /// </summary>
    public async Task ExecuteSharePipelineAsync(SavedShoppingList currentList, ObservableCollection<ShoppingItemGroup> groupedItems, string shareOption)
    {
        string textToShare = "";

        if (shareOption == "טקסט רגיל")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"*{currentList?.Title ?? "רשימת קניות"}:*");
            sb.AppendLine();

            foreach (var group in groupedItems)
            {
                sb.AppendLine($"--- *{group.CategoryName}* ---");
                foreach (var item in group)
                {
                    sb.AppendLine($"{(item.IsBought ? "✅" : "🔲")} {item.DisplayText}");
                }
                sb.AppendLine();
            }
            textToShare = sb.ToString();
        }
        else if (shareOption == "קישור לאפליקציה")
        {
            var sharedDto = new SharedListDto { T = currentList?.Title ?? "רשימה משותפת" };
            var allItemNames = new HashSet<string>();

            foreach (var group in groupedItems)
            {
                foreach (var item in group)
                {
                    allItemNames.Add(item.Name);
                    sharedDto.I.Add(new SharedItemDto { N = item.Name, Q = item.Quantity, U = item.Unit, C = item.Category });
                }
            }

            var allConversions = await _database.GetIngredientConversionsAsync();
            var relevantConversions = allConversions.Where(c => allItemNames.Contains(c.Keyword)).ToList();

            foreach (var conv in relevantConversions)
            {
                sharedDto.C.Add(new SharedConversionDto { K = conv.Keyword, B = conv.BaseUnit, A = conv.AmountPerCup, C = conv.Category });
            }

            string jsonPayload = JsonSerializer.Serialize(sharedDto);
            var cloudModel = new SharedShoppingListCloudModel { ListName = sharedDto.T, PayloadJson = jsonPayload };

            var firestoreService = new FirestoreService();
            string newCloudId = await firestoreService.UploadSharedListAsync(cloudModel);

            if (string.IsNullOrEmpty(newCloudId))
            {
                await Application.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו לייצר קישור לענן. אנא בדוק את החיבור לאינטרנט.", "אישור");
                return;
            }

            await _database.RegisterSharedListForDeletionAsync(newCloudId, cloudModel.ExpiresAt);
            string deepLink = $"https://recipe-book-d9389.web.app/sharelist?id={newCloudId}";

            var sbLink = new StringBuilder();
            sbLink.AppendLine($"*{cloudModel.ListName}*");
            sbLink.AppendLine("שלחתי לך רשימת קניות מרוכזת באפליקציה!");
            sbLink.AppendLine("לחץ על הקישור כדי לייבא אותה (כולל המרות מצרכים):");
            sbLink.AppendLine();
            sbLink.AppendLine(deepLink);

            textToShare = sbLink.ToString();
        }

        await Share.Default.RequestAsync(new ShareTextRequest { Text = textToShare, Title = "שיתוף רשימת קניות" });
    }
}

