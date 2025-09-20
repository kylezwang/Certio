using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Projects;
using Certio.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProjectController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Project
        public async Task<IActionResult> Index()
        {
            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var projects = await _context.Projects
                .Where(p => p.OrganizationId == primaryOrg.OrganizationId)
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.User)
                .ToListAsync();

            // If no projects exist, add some sample data for demonstration
            if (!projects.Any())
            {
                var sampleProjects = new List<Project>
                {
                    new Project
                    {
                        Title = "Contract Review Automation",
                        Description = "AI-powered contract analysis and risk assessment system",
                        Status = "In Progress",
                        Priority = "High",
                        Category = "AI/ML",
                        ProjectType = "Contract Management",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 2, 15),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    },
                    new Project
                    {
                        Title = "Client Portal Dashboard",
                        Description = "Secure client access portal with document sharing capabilities",
                        Status = "Review",
                        Priority = "Medium",
                        Category = "Frontend",
                        ProjectType = "Client Portal",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 2, 28),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    },
                    new Project
                    {
                        Title = "Compliance Tracking System",
                        Description = "Automated regulatory compliance monitoring and reporting",
                        Status = "Planning",
                        Priority = "High",
                        Category = "Backend",
                        ProjectType = "Compliance Tracking",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 3, 10),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    },
                    new Project
                    {
                        Title = "Legal Research Assistant",
                        Description = "Natural language processing for legal document search",
                        Status = "Completed",
                        Priority = "Medium",
                        Category = "AI/ML",
                        ProjectType = "Legal Research",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 1, 30),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    }
                };

                _context.Projects.AddRange(sampleProjects);
                await _context.SaveChangesAsync();
                
                // Reload projects from database
                projects = await _context.Projects
                    .Where(p => p.OrganizationId == primaryOrg.OrganizationId)
                    .Include(p => p.Assignments)
                        .ThenInclude(a => a.User)
                    .ToListAsync();
            }

            var viewModel = new ProjectsViewModel
            {
                Projects = projects,
                ActiveProjectsCount = projects.Count(p => p.Status == "In Progress"),
                CompletedProjectsCount = projects.Count(p => p.Status == "Completed"),
                InReviewProjectsCount = projects.Count(p => p.Status == "Review"),
                TeamMembersCount = await _context.UserOrganizations
                    .Where(uo => uo.OrganizationId == primaryOrg.OrganizationId && uo.IsActive)
                    .CountAsync()
            };

            return View(viewModel);
        }

        // GET: Project/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var project = await _context.Projects
                .Where(p => p.Id == id && p.OrganizationId == primaryOrg.OrganizationId)
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound();
            }

            return View(project);
        }

        // GET: Project/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new ProjectFormViewModel();
            
            // Populate clients and teams data
            await PopulateClientAndTeamData(viewModel);
            
            return View(viewModel);
        }

        // POST: Project/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectFormViewModel model, string action)
        {
            // Always populate client and team data
            await PopulateClientAndTeamData(model);
            
            
            if (action == "next")
            {
                // Clear previous validation errors for fields not in current step FIRST
                ClearNonCurrentStepErrors(model);
                
                // Clear ModelState completely before validating current step
                ModelState.Clear();
                
                // Validate current step before moving to next
                if (ValidateCurrentStep(model))
                {
                    model.Step++;
                    return View(model);
                }
                // If validation fails, return to current step with only current step errors
                return View(model);
            }
            else if (action == "previous")
            {
                // Move to previous step - data is already cleared by JavaScript
                if (model.Step > 1)
                {
                    model.Step--;
                }
                // Clear any validation errors since we're going back
                ModelState.Clear();
                return View(model);
            }
            else if (action == "create")
            {
                // Validate all required fields before creating
                if (ValidateAllSteps(model))
                {
                    var project = new Project
                    {
                        Title = model.Title,
                        Description = model.Description,
                        Category = model.Category,
                        ProjectType = model.ProjectType,
                        Status = model.Status,
                        Priority = model.Priority,
                        StartDate = model.StartDate,
                        DueDate = model.DueDate,
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    };

                    _context.Projects.Add(project);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Project created successfully!";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(model);
        }

        private bool ValidateCurrentStep(ProjectFormViewModel model)
        {
            bool isValid = true;

            if (model.Step == 1)
            {
                // Validate Step 1 fields
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    ModelState.AddModelError("Title", "Project title is required");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(model.Category))
                {
                    ModelState.AddModelError("Category", "Please select a category");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(model.ProjectType))
                {
                    ModelState.AddModelError("ProjectType", "Please select a project type");
                    isValid = false;
                }
            }
            else if (model.Step == 2)
            {
                // Validate Step 2 fields
                if (string.IsNullOrWhiteSpace(model.Status))
                {
                    ModelState.AddModelError("Status", "Please select a status");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(model.Priority))
                {
                    ModelState.AddModelError("Priority", "Please select a priority");
                    isValid = false;
                }
            }
            else if (model.Step == 3)
            {
                // Validate Step 3 fields
                if (!model.ClientId.HasValue)
                {
                    ModelState.AddModelError("ClientId", "Please select a client");
                    isValid = false;
                }
                // Team is optional, so no validation needed
            }

            return isValid;
        }

        private bool ValidateAllSteps(ProjectFormViewModel model)
        {
            bool isValid = true;

            // Validate Step 1 fields
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "Project title is required");
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(model.Category))
            {
                ModelState.AddModelError("Category", "Please select a category");
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(model.ProjectType))
            {
                ModelState.AddModelError("ProjectType", "Please select a project type");
                isValid = false;
            }

            // Validate Step 2 fields
            if (string.IsNullOrWhiteSpace(model.Status))
            {
                ModelState.AddModelError("Status", "Please select a status");
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(model.Priority))
            {
                ModelState.AddModelError("Priority", "Please select a priority");
                isValid = false;
            }

            // Validate Step 3 fields
            if (!model.ClientId.HasValue)
            {
                ModelState.AddModelError("ClientId", "Please select a client");
                isValid = false;
            }

            return isValid;
        }

        private void ClearNonCurrentStepErrors(ProjectFormViewModel model)
        {
            // This method is now simplified since we clear ModelState completely
            // before validating the current step
        }

        private void ClearCurrentStepData(ProjectFormViewModel model)
        {
            if (model.Step == 2)
            {
                // Clear Step 2 data when going back from Step 2 to Step 1
                model.Status = "";
                model.Priority = "";
                model.StartDate = null;
                model.DueDate = null;
            }
            else if (model.Step == 3)
            {
                // Clear Step 3 data when going back from Step 3 to Step 2
                model.ClientId = null;
                model.TeamId = null;
            }
        }

        private async Task PopulateClientAndTeamData(ProjectFormViewModel model)
        {
            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                // If no user context, return empty lists
                model.Clients = new List<ClientOption>();
                model.Teams = new List<TeamOption>();
                return;
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                // If no organization, return empty lists
                model.Clients = new List<ClientOption>();
                model.Teams = new List<TeamOption>();
                return;
            }

            // Get clients (users with Client role in the same organization)
            var clients = await _context.UserOrganizations
                .Where(uo => uo.OrganizationId == primaryOrg.OrganizationId && 
                            uo.UserType == Certio.Domain.Users.UserType.Client && 
                            uo.IsActive)
                .Include(uo => uo.User)
                .Select(uo => new ClientOption
                {
                    Id = uo.User.Id,
                    Name = $"{uo.User.FirstName} {uo.User.LastName}",
                    Company = uo.User.Company ?? "No Company",
                    Email = uo.User.Email
                })
                .ToListAsync();

            // Get teams in the same organization
            var teams = await _context.Teams
                .Where(t => t.OrganizationId == primaryOrg.OrganizationId && t.IsActive)
                .Select(t => new TeamOption
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description ?? "No Description",
                    TeamType = t.TeamType.ToString()
                })
                .ToListAsync();

            // If no clients exist, add some sample data
            if (!clients.Any())
            {
                clients = new List<ClientOption>
                {
                    new ClientOption { Id = 1, Name = "John Smith", Company = "Acme Corp", Email = "john@acme.com" },
                    new ClientOption { Id = 2, Name = "Jane Doe", Company = "Tech Solutions", Email = "jane@techsolutions.com" },
                    new ClientOption { Id = 3, Name = "Bob Johnson", Company = "Legal Partners", Email = "bob@legalpartners.com" }
                };
            }

            // If no teams exist, add some sample data
            if (!teams.Any())
            {
                teams = new List<TeamOption>
                {
                    new TeamOption { Id = 1, Name = "Legal Team Alpha", Description = "Senior legal professionals", TeamType = "Legal" },
                    new TeamOption { Id = 2, Name = "Contract Review Team", Description = "Specialized in contract analysis", TeamType = "Legal" },
                    new TeamOption { Id = 3, Name = "Client Relations", Description = "Client communication and support", TeamType = "Client" }
                };
            }

            model.Clients = clients;
            model.Teams = teams;
        }

        // GET: Project/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var viewModel = new ProjectFormViewModel
            {
                Id = project.Id,
                Title = project.Title,
                Description = project.Description,
                Category = project.Category,
                ProjectType = project.ProjectType,
                Status = project.Status,
                Priority = project.Priority,
                StartDate = project.StartDate,
                DueDate = project.DueDate
            };

            return View(viewModel);
        }

        // POST: Project/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProjectFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var project = await _context.Projects.FindAsync(id);
                    if (project == null)
                    {
                        return NotFound();
                    }

                    project.Title = model.Title;
                    project.Description = model.Description;
                    project.Category = model.Category;
                    project.ProjectType = model.ProjectType;
                    project.Status = model.Status;
                    project.Priority = model.Priority;
                    project.StartDate = model.StartDate;
                    project.DueDate = model.DueDate;
                    project.LastModifiedDate = DateTime.UtcNow;

                    _context.Update(project);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Project updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProjectExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Project/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            return View(project);
        }

        // POST: Project/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project != null)
            {
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Project deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.Id == id);
        }
    }
}
