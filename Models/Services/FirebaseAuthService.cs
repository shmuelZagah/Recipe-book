using Plugin.Firebase.Auth;

namespace Recipe_book.Services
{
    public interface IFirebaseAuthService
    {
        Task<string> SignInAnonymouslyAsync();
        string GetCurrentUserId();
        bool IsSignedIn();
    }

    public class FirebaseAuthService : IFirebaseAuthService
    {
        public async Task<string> SignInAnonymouslyAsync()
        {
            try
            {
                if (CrossFirebaseAuth.Current.CurrentUser != null)
                {
                    return CrossFirebaseAuth.Current.CurrentUser.Uid;
                }

                var result = await CrossFirebaseAuth.Current.SignInAnonymouslyAsync();
                return result.Uid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH ERROR]: {ex.Message}");
                return null;
            }
        }

        public string GetCurrentUserId()
        {
            return CrossFirebaseAuth.Current.CurrentUser?.Uid;
        }

        public bool IsSignedIn()
        {
            return CrossFirebaseAuth.Current.CurrentUser != null;
        }
    }
}