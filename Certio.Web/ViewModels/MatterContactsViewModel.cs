using Certio.Domain.Matters;
using Certio.Application.DTOs.ChangeControl;

namespace Certio.Web.ViewModels
{
    public class MatterContactsViewModel
    {
        public Matter Matter { get; set; } = null!;
        public ChangeControlSummaryDto? ChangeControlSummary { get; set; }
    }
}
