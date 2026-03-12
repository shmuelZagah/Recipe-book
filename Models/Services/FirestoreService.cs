using Plugin.Firebase.Firestore;
using Recipe_book.Models.Recipes;

namespace Recipe_book.Services;

public class FirestoreService
{
    public async Task SaveRecipeToCloudAsync(Recipe recipe)
    {
        try
        {
            var firestore = CrossFirebaseFirestore.Current;
            var collection = firestore.GetCollection("Recipes"); 


            if (string.IsNullOrEmpty(recipe.CloudId))
            {
                var doc = collection.CreateDocument();
                recipe.CloudId = doc.Id;
            }

            await collection.GetDocument(recipe.CloudId).SetDataAsync(recipe);

            System.Diagnostics.Debug.WriteLine($"Recipe {recipe.Title} saved to cloud successfully!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving to cloud: {ex.Message}");
        }
    }
}