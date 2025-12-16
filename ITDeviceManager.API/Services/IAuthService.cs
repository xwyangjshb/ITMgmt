namespace ITDeviceManager.API.Services;

public interface IAuthService
{
    public Task<string?> AuthenticateAsync(string username, string password);
    public string GenerateJwtToken(string username, string role);
    public string HashPassword(string password);
    public bool VerifyPassword(string password, string hash);
}
