using Certio.Domain.Projects;

namespace Certio.Web.ViewModels
{
    public class ProjectsViewModel
    {
        public List<Project> Projects { get; set; } = new List<Project>();
        public int ActiveProjectsCount { get; set; }
        public int CompletedProjectsCount { get; set; }
        public int InReviewProjectsCount { get; set; }
        public int TeamMembersCount { get; set; }
    }
}
