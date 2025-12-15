namespace ITDeviceManager.API.Services
{
    public interface IAuthService
    {
        Task<string?> AuthenticateAsync(string username, string password);
        string GenerateJwtToken(string username, string role);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }
}
