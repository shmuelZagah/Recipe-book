using System.Text.Json;
using System.Text;
using Recipe_book.Models.Shopping;
using Recipe_book.Services;
using Recipe_book.ViewModels;

namespace Recipe_book;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }


    protected override async void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);

        // Check if the link belongs to the shopping list deep link format
        if (uri.Scheme.ToLower() == "http" && uri.Host.ToLower() == "recipebook.app")
        {
            try
            {
                // 1. Extract the encoded text from the URL query
                string query = uri.Query;
                string base64Data = query.Replace("?data=", "");
                string decodedData = Uri.UnescapeDataString(base64Data);

                // Auto-fix Base64 padding if it was truncated by the URL formatting
                int mod4 = decodedData.Length % 4;
                if (mod4 > 0) decodedData += new string('=', 4 - mod4);

                // 2. Decode the Base64 string back into the DTO object
                byte[] bytes = Convert.FromBase64String(decodedData);
                string json = Encoding.UTF8.GetString(bytes);

                var sharedDto = JsonSerializer.Deserialize<SharedListDto>(json);

                if (sharedDto != null)
                {
                    // 3. Get the database service instance
                    var db = IPlatformApplication.Current.Services.GetService<RecipesDatabase>();

                    // 4. Create a new static shopping list
                    var newList = new SavedShoppingList
                    {
                        Title = sharedDto.T + " (מיובא)", // Append "(Imported)" in Hebrew
                        IsStatic = true, // Static list, detached from meal schedule
                        CreatedAt = DateTime.Now
                    };

                    await db.SaveShoppingListAsync(newList); // Save to generate the new ID

                    // 5. Convert the lightweight DTO items to actual database items
                    var flatList = new List<SavedShoppingListItem>();
                    foreach (var item in sharedDto.I)
                    {
                        string displayUnit = item.U == "יחידות" ? "" : item.U;
                        string displayTxt = string.IsNullOrWhiteSpace(displayUnit) ? $"{item.Q} {item.N}" : $"{item.Q} {displayUnit} {item.N}";

                        flatList.Add(new SavedShoppingListItem
                        {
                            ListId = newList.Id,
                            Name = item.N,
                            Quantity = item.Q,
                            Unit = displayUnit,
                            Category = item.C,
                            DisplayText = displayTxt,
                            IsBought = false
                        });
                    }

                    // Save all the converted items to the new list
                    await db.SyncShoppingListItemsAsync(newList.Id, flatList);

                    // 6. Notify the user of the successful import
                    await App.Current.MainPage.DisplayAlert("רשימה יובאה! 🎉", $"הרשימה '{newList.Title}' נוספה בהצלחה. תוכל למצוא אותה בתפריט הרשימות שלך.", "מעולה");
                }
            }
            catch (Exception ex)
            {
                // Handle corrupted or invalid deep links
                await App.Current.MainPage.DisplayAlert("שגיאה בייבוא", "הקישור לא תקין או פגום.", "אישור");
            }
        }
    }
}
