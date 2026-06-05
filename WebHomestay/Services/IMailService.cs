namespace WebHomestay.Services
{
    public record EmailAttachment(string FilePath, string FileName, string ContentType);

    public interface IMailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendEmailAsync(string toEmail, string subject, string body, IReadOnlyCollection<EmailAttachment> attachments);
    }
}
