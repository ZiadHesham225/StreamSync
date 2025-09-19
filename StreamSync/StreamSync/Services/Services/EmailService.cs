using MimeKit;
using MailKit.Security;
using StreamSync.BusinessLogic.Interfaces;

namespace StreamSync.BusinessLogic.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }
        public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var emailMessage = new MimeMessage();

            var senderEmail = _config["EmailSettings:SenderEmail"];
            var senderName = "StreamSync-Project";

            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));

            emailMessage.To.Add(new MailboxAddress(userName, toEmail));

            emailMessage.Subject = "Password Reset Request";

            string body = $@"
            <p>Hello {userName},</p>
            <p>You requested a password reset. Click the link below to reset your password:</p>
            <p><a href='{resetLink}' style='color: blue;'>Reset Password</a></p>
            <p>If you didn't request this, please ignore this email.</p>
            <p>Regards,<br>StreamSync Support Team</p>";

            emailMessage.Body = new TextPart("html") { Text = body };

            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                await client.ConnectAsync(
                    _config["EmailSettings:SmtpServer"],
                    int.Parse(_config["EmailSettings:SmtpPort"]),
                    SecureSocketOptions.StartTls
                );

                await client.AuthenticateAsync(
                    senderEmail,
                    _config["EmailSettings:SenderPassword"]
                );

                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            }
        }
    }
}
