using System.Net.Mail;
using System.Text;

namespace VaultHistory.Notification.Infrastructure.Gmail;

public static class GmailMimeMessageBuilder
{
    public static string CreateRaw(string senderAddress, string senderName, string recipient, string subject, string htmlBody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senderAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(htmlBody);

        var sender = new MailAddress(senderAddress, senderName, Encoding.UTF8).ToString();
        var receiver = new MailAddress(recipient).Address;
        var encodedSubject = EncodeHeader(subject);
        var encodedBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlBody), Base64FormattingOptions.InsertLineBreaks);
        var mime = $"From: {sender}\r\nTo: {receiver}\r\nSubject: {encodedSubject}\r\nMIME-Version: 1.0\r\nContent-Type: text/html; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n{encodedBody}";

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(mime))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string EncodeHeader(string value) => $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";
}
