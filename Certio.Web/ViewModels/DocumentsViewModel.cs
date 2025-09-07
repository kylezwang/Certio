using Certio.Domain.Documents;

namespace Certio.Web.ViewModels
{
    public class DocumentsViewModel
    {
        public List<Document> Documents { get; set; } = new List<Document>();
        public List<FolderInfo> Folders { get; set; } = new List<FolderInfo>();
        public StorageInfo Storage { get; set; } = new StorageInfo();
    }
}
