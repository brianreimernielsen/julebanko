namespace Julebanko.Models;

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AdminRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
