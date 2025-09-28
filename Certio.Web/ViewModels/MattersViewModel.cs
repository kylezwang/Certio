using Certio.Domain.Matters;

namespace Certio.Web.ViewModels
{
    public class MattersViewModel
    {
        public List<Matter> Matters { get; set; } = new List<Matter>();
        public int ActiveMattersCount { get; set; }
        public int CompletedMattersCount { get; set; }
        public int InReviewMattersCount { get; set; }
        public int TeamMembersCount { get; set; }
    }
}
