using Certio.Domain.Documents;
using Certio.Domain.Matters;

namespace Certio.Web.ViewModels
{
    public class DocumentsViewModel
    {
        public List<Document> Documents { get; set; } = new List<Document>();
        public List<FolderInfo> Folders { get; set; } = new List<FolderInfo>();
        public StorageInfo Storage { get; set; } = new StorageInfo();
        public List<Matter> Matters { get; set; } = new List<Matter>();
    }
}
