using Plugin.Firebase.Firestore;
using Recipe_book.Models.Recipes;
using Recipe_book.Models.Cloud;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using System.IO;

namespace Recipe_book.Services;

public class FirestoreService
{
    public async Task SaveRecipeToCloudAsync(Recipe recipe)
    {
        try
        {
            if (!string.IsNullOrEmpty(recipe.LocalImagePath) && string.IsNullOrEmpty(recipe.CloudImagePath))
            {
                recipe.CloudImagePath = await UploadImageAsync(recipe.LocalImagePath);
            }

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

    public async Task<bool> DeleteRecipeFromCloudAsync(string cloudId)
    {
        if (string.IsNullOrEmpty(cloudId)) return true; // Nothing to delete

        try
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("Recipes").GetDocument(cloudId).DeleteDocumentAsync();

            System.Diagnostics.Debug.WriteLine($"Recipe with CloudId {cloudId} deleted from cloud.");
            return true; // Success!
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting from cloud: {ex.Message}");
            return false; // Failed (likely no internet)
        }
    }

    public async Task<bool> DeleteImageFromCloudAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return true;

        try
        {
            int startIndex = imageUrl.IndexOf("recipe_images/");
            if (startIndex == -1) return true;

            string publicIdWithExt = imageUrl.Substring(startIndex);
            int lastDot = publicIdWithExt.LastIndexOf('.');
            string publicId = lastDot != -1 ? publicIdWithExt.Substring(0, lastDot) : publicIdWithExt;

            var account = new Account(
                Secrets.CloudinaryCloudName,
                Secrets.CloudinaryApiKey,
                Secrets.CloudinaryApiSecret
            );

            var cloudinary = new Cloudinary(account);
            var deletionParams = new DeletionParams(publicId);

            var result = await Task.Run(() => cloudinary.Destroy(deletionParams));

            if (result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                System.Diagnostics.Debug.WriteLine($"Image {publicId} successfully deleted.");
                return true; // Success!
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting image: {ex.Message}");
            return false; // Failed (likely no internet)
        }
    }

    public async Task<bool> DeleteSharedFolderFromCloudAsync(string cloudId)
    {
        if (string.IsNullOrEmpty(cloudId)) return true;

        try
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("SharedFolders").GetDocument(cloudId).DeleteDocumentAsync();

            System.Diagnostics.Debug.WriteLine($"Shared folder {cloudId} automatically deleted by Garbage Collector.");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting shared folder: {ex.Message}");
            return false;
        }
    }

    public async Task<Recipe> GetRecipeFromCloudAsync(string cloudId)
    {
        if (string.IsNullOrEmpty(cloudId)) return null;

        try
        {
            var firestore = CrossFirebaseFirestore.Current;
            var snapshot = await firestore.GetCollection("Recipes").GetDocument(cloudId).GetDocumentSnapshotAsync<Recipe>();

            if (snapshot != null)
            {
                var recipe = snapshot.Data;
                recipe.CloudId = cloudId; 
                return recipe;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching recipe from cloud: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Fetches a shared folder document from Firestore using its CloudId.
    /// </summary>
    public async Task<SharedFolderCloudModel> GetSharedFolderFromCloudAsync(string cloudId)
    {
        if (string.IsNullOrEmpty(cloudId)) return null;

        try
        {
            var firestore = CrossFirebaseFirestore.Current;
            var snapshot = await firestore.GetCollection("SharedFolders").GetDocument(cloudId).GetDocumentSnapshotAsync<SharedFolderCloudModel>();

            if (snapshot != null)
            {
                var folder = snapshot.Data;
                folder.CloudId = cloudId;
                return folder;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching shared folder from cloud: {ex.Message}");
        }

        return null;
    }


    private async Task<string> UploadImageAsync(string localFilePath)
    {
        try
        {
            var account = new Account(
                   Secrets.CloudinaryCloudName,
                   Secrets.CloudinaryApiKey,
                   Secrets.CloudinaryApiSecret
               );

            var cloudinary = new Cloudinary(account);

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(localFilePath),
                Folder = "recipe_images",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false,
                // Server-side transformation: fixes EXIF orientation, limits width to 1080px, and optimizes size/format
                Transformation = new Transformation().Width(1080).Crop("limit").Quality("auto").FetchFormat("auto")
            };

            var uploadResult = await Task.Run(() => cloudinary.Upload(uploadParams));

            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return uploadResult.SecureUrl.ToString();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Cloudinary Error: {uploadResult.Error.Message}");
                return localFilePath; // Fallback to local path on failure
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Upload Exception: {ex.Message}");
            return localFilePath; // Fallback to local path on failure
        }
    }


    /// <summary>
    /// Uploads a flat packaged folder to Firestore.
    /// The complex tree is safely hidden inside the RootFolderJson string.
    /// </summary>
    public async Task<string> UploadSharedFolderAsync(SharedFolderCloudModel sharedFolder)
    {
        try
        {
            var firestore = CrossFirebaseFirestore.Current;
            var collection = firestore.GetCollection("SharedFolders");

            var doc = collection.CreateDocument();
            sharedFolder.CloudId = doc.Id;

            // This will now work perfectly because the model is completely flat!
            await doc.SetDataAsync(sharedFolder);

            System.Diagnostics.Debug.WriteLine($"Shared folder uploaded successfully! ID: {doc.Id}");
            return doc.Id;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error uploading shared folder: {ex.Message}");
            return null;
        }
    }

}