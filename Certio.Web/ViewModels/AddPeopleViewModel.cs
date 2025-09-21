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
        public string UserType { get; set; } = UserTypes.Client;

        [Required]
        public string Role { get; set; } = OrganizationRoles.Member;
    }
}


