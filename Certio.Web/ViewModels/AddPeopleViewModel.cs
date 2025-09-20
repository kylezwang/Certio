using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Web.ViewModels
{
    public class AddPeopleViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public UserType UserType { get; set; } = UserType.Client;

        [Required]
        public OrganizationRole Role { get; set; } = OrganizationRole.Member;
    }
}


