namespace Certio.Web.Configuration
{
    public class AnonymizationSettings
    {
        public string DeletedEmailDomain { get; set; } = "anonymized.local";
        public string DeletedFirstName { get; set; } = "Deleted";
        public string DeletedLastName { get; set; } = "User";
        public string DeletedContentMessage { get; set; } = "[Content deleted by user request]";
        public string DeletedCommentMessage { get; set; } = "[Comment deleted by user request]";
        public string DeletedSenderName { get; set; } = "Deleted User";
    }
}
