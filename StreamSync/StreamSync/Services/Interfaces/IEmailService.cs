namespace StreamSync.BusinessLogic.Interfaces
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
    }
}
