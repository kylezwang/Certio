using Certio.Domain.Documents;

namespace Certio.Web.ViewModels
{
    public class DocumentViewViewModel
    {
        public Document Document { get; set; } = null!;
        public string EmbedUrl { get; set; } = string.Empty;
        public string OpenUrl { get; set; } = string.Empty;
    }
}

