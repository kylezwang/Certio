using Certio.Domain.AIAgents;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Certio.Web.Services
{
    public interface IAIUsageService
    {
        /// <summary>
        /// Records an AI API usage event
        /// </summary>
        Task RecordUsageAsync(AIUsageRecord record, CancellationToken ct = default);
        
        /// <summary>
        /// Gets usage history for a user within a date range
        /// </summary>
        Task<List<AIUsage>> GetUserUsageHistoryAsync(int userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);
        
        /// <summary>
        /// Gets daily aggregated usage for a user
        /// </summary>
        Task<List<AIUsageDaily>> GetUserDailyUsageAsync(int userId, int? organizationId = null, int days = 30, CancellationToken ct = default);
        
        /// <summary>
        /// Gets organization-wide usage statistics
        /// </summary>
        Task<OrganizationUsageStats> GetOrganizationUsageStatsAsync(int organizationId, int days = 30, CancellationToken ct = default);
        
        /// <summary>
        /// Gets usage chart data for the Settings page
        /// </summary>
        Task<UsageChartData> GetUsageChartDataAsync(int userId, int organizationId, int days = 30, CancellationToken ct = default);
        
        /// <summary>
        /// Updates or creates daily aggregate records
        /// </summary>
        Task UpdateDailyAggregatesAsync(int userId, int organizationId, DateTime date, CancellationToken ct = default);
    }

    public class AIUsageService : IAIUsageService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AIUsageService> _logger;

        public AIUsageService(ApplicationDbContext db, ILogger<AIUsageService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task RecordUsageAsync(AIUsageRecord record, CancellationToken ct = default)
        {
            try
            {
                var usage = new AIUsage
                {
                    UserId = record.UserId,
                    OrganizationId = record.OrganizationId,
                    ModelName = record.ModelName,
                    ModelTier = record.ModelTier,
                    InputTokens = record.InputTokens,
                    OutputTokens = record.OutputTokens,
                    TotalTokens = record.InputTokens + record.OutputTokens,
                    EstimatedCost = CalculateCost(record.ModelName, record.InputTokens, record.OutputTokens),
                    RequestType = record.RequestType,
                    ConversationId = record.ConversationId,
                    ComplexityScore = record.ComplexityScore,
                    ResponseTimeMs = record.ResponseTimeMs,
                    IsStreaming = record.IsStreaming,
                    IsSuccessful = record.IsSuccessful,
                    ErrorMessage = record.ErrorMessage,
                    CreatedAt = DateTime.UtcNow
                };

                _db.AIUsages.Add(usage);
                await _db.SaveChangesAsync(ct);

                // Update daily aggregate asynchronously (fire and forget with logging)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await UpdateDailyAggregatesAsync(record.UserId, record.OrganizationId, DateTime.UtcNow.Date, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating daily aggregates for user {UserId}", record.UserId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording AI usage for user {UserId}", record.UserId);
                throw;
            }
        }

        public async Task<List<AIUsage>> GetUserUsageHistoryAsync(int userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
        {
            var query = _db.AIUsages
                .AsNoTracking()
                .Where(u => u.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(u => u.CreatedAt >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(u => u.CreatedAt <= endDate.Value);

            return await query
                .OrderByDescending(u => u.CreatedAt)
                .Take(1000) // Limit for safety
                .ToListAsync(ct);
        }

        public async Task<List<AIUsageDaily>> GetUserDailyUsageAsync(int userId, int? organizationId = null, int days = 30, CancellationToken ct = default)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);
            
            var query = _db.AIUsageDailies
                .AsNoTracking()
                .Where(u => u.UserId == userId && u.Date >= startDate);

            if (organizationId.HasValue)
                query = query.Where(u => u.OrganizationId == organizationId.Value);

            return await query
                .OrderByDescending(u => u.Date)
                .ToListAsync(ct);
        }

        public async Task<OrganizationUsageStats> GetOrganizationUsageStatsAsync(int organizationId, int days = 30, CancellationToken ct = default)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);

            var dailyData = await _db.AIUsageDailies
                .AsNoTracking()
                .Where(u => u.OrganizationId == organizationId && u.Date >= startDate)
                .ToListAsync(ct);

            return new OrganizationUsageStats
            {
                TotalCost = dailyData.Sum(d => d.TotalCost),
                TotalCalls = dailyData.Sum(d => d.TotalCalls),
                TotalTokens = dailyData.Sum(d => d.TotalTokens),
                AverageDailyCost = dailyData.Any() ? dailyData.Average(d => d.TotalCost) : 0,
                UniqueUsers = dailyData.Select(d => d.UserId).Distinct().Count(),
                StartDate = startDate,
                EndDate = DateTime.UtcNow.Date
            };
        }

        public async Task<UsageChartData> GetUsageChartDataAsync(int userId, int organizationId, int days = 30, CancellationToken ct = default)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);
            
            // Get daily aggregates for the user
            var dailyData = await _db.AIUsageDailies
                .AsNoTracking()
                .Where(u => u.UserId == userId && u.OrganizationId == organizationId && u.Date >= startDate)
                .OrderBy(u => u.Date)
                .ToListAsync(ct);

            // Fill in missing days with zero values
            var allDays = Enumerable.Range(0, days + 1)
                .Select(i => startDate.AddDays(i))
                .ToList();

            var dataPoints = allDays.Select(date =>
            {
                var dailyRecord = dailyData.FirstOrDefault(d => d.Date.Date == date.Date);
                return new UsageDataPoint
                {
                    Date = date,
                    Cost = dailyRecord?.TotalCost ?? 0,
                    Calls = dailyRecord?.TotalCalls ?? 0,
                    Tokens = dailyRecord?.TotalTokens ?? 0
                };
            }).ToList();

            // Calculate summaries
            var totalCost = dailyData.Sum(d => d.TotalCost);
            var totalCalls = dailyData.Sum(d => d.TotalCalls);
            var totalTokens = dailyData.Sum(d => d.TotalTokens);

            // Get model breakdown
            var modelBreakdown = new Dictionary<string, decimal>();
            foreach (var day in dailyData.Where(d => !string.IsNullOrEmpty(d.CostByModel)))
            {
                try
                {
                    var costs = JsonSerializer.Deserialize<Dictionary<string, decimal>>(day.CostByModel!);
                    if (costs != null)
                    {
                        foreach (var kvp in costs)
                        {
                            if (!modelBreakdown.ContainsKey(kvp.Key))
                                modelBreakdown[kvp.Key] = 0;
                            modelBreakdown[kvp.Key] += kvp.Value;
                        }
                    }
                }
                catch { /* Ignore JSON errors */ }
            }

            return new UsageChartData
            {
                DataPoints = dataPoints,
                TotalCost = totalCost,
                TotalCalls = totalCalls,
                TotalTokens = totalTokens,
                CostByModel = modelBreakdown,
                StartDate = startDate,
                EndDate = DateTime.UtcNow.Date
            };
        }

        public async Task UpdateDailyAggregatesAsync(int userId, int organizationId, DateTime date, CancellationToken ct = default)
        {
            var dateOnly = date.Date;
            var nextDay = dateOnly.AddDays(1);

            // Get all usage records for this day
            var dayUsage = await _db.AIUsages
                .AsNoTracking()
                .Where(u => u.UserId == userId && u.OrganizationId == organizationId 
                    && u.CreatedAt >= dateOnly && u.CreatedAt < nextDay)
                .ToListAsync(ct);

            if (!dayUsage.Any())
                return;

            // Calculate aggregates
            var totalCalls = dayUsage.Count;
            var totalInputTokens = dayUsage.Sum(u => u.InputTokens);
            var totalOutputTokens = dayUsage.Sum(u => u.OutputTokens);
            var totalTokens = dayUsage.Sum(u => u.TotalTokens);
            var totalCost = dayUsage.Sum(u => u.EstimatedCost);

            var callsByModel = dayUsage
                .GroupBy(u => u.ModelName)
                .ToDictionary(g => g.Key, g => g.Count());

            var costByModel = dayUsage
                .GroupBy(u => u.ModelName)
                .ToDictionary(g => g.Key, g => g.Sum(u => u.EstimatedCost));

            var avgComplexity = dayUsage
                .Where(u => u.ComplexityScore.HasValue)
                .Select(u => u.ComplexityScore!.Value)
                .DefaultIfEmpty(0)
                .Average();

            var avgResponseTime = (int)dayUsage
                .Where(u => u.ResponseTimeMs.HasValue)
                .Select(u => u.ResponseTimeMs!.Value)
                .DefaultIfEmpty(0)
                .Average();

            // Find or create daily aggregate
            var dailyAggregate = await _db.AIUsageDailies
                .FirstOrDefaultAsync(d => d.UserId == userId && d.OrganizationId == organizationId && d.Date == dateOnly, ct);

            if (dailyAggregate == null)
            {
                dailyAggregate = new AIUsageDaily
                {
                    UserId = userId,
                    OrganizationId = organizationId,
                    Date = dateOnly,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AIUsageDailies.Add(dailyAggregate);
            }

            dailyAggregate.TotalCalls = totalCalls;
            dailyAggregate.TotalInputTokens = totalInputTokens;
            dailyAggregate.TotalOutputTokens = totalOutputTokens;
            dailyAggregate.TotalTokens = totalTokens;
            dailyAggregate.TotalCost = totalCost;
            dailyAggregate.CallsByModel = JsonSerializer.Serialize(callsByModel);
            dailyAggregate.CostByModel = JsonSerializer.Serialize(costByModel);
            dailyAggregate.AverageComplexityScore = avgComplexity;
            dailyAggregate.AverageResponseTimeMs = avgResponseTime;
            dailyAggregate.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        private static decimal CalculateCost(string modelName, int inputTokens, int outputTokens)
        {
            // Cost per 1K tokens based on model
            var (inputCost, outputCost) = modelName.ToLower() switch
            {
                "gpt-4o-mini" => (0.00015m, 0.0006m),   // $0.15/$0.60 per 1M tokens
                "gpt-4.1-mini" => (0.003m, 0.012m),     // $3/$12 per 1M tokens (o1-mini)
                "o1-mini" => (0.003m, 0.012m),          // $3/$12 per 1M tokens
                "gpt-4o" => (0.005m, 0.015m),           // $5/$15 per 1M tokens
                "gpt-4.1" => (0.015m, 0.06m),           // $15/$60 per 1M tokens (o1-preview)
                "o1-preview" => (0.015m, 0.06m),        // $15/$60 per 1M tokens
                "gpt-5" => (0.01m, 0.03m),              // $10/$30 per 1M tokens (estimated)
                _ => (0.005m, 0.015m)                    // Default to gpt-4o pricing
            };

            return (inputTokens / 1000m * inputCost) + (outputTokens / 1000m * outputCost);
        }
    }

    // DTOs for the service
    public class AIUsageRecord
    {
        public int UserId { get; set; }
        public int OrganizationId { get; set; }
        public string ModelName { get; set; } = "";
        public string ModelTier { get; set; } = "Auto";
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public string RequestType { get; set; } = "chat";
        public string? ConversationId { get; set; }
        public decimal? ComplexityScore { get; set; }
        public int? ResponseTimeMs { get; set; }
        public bool IsStreaming { get; set; }
        public bool IsSuccessful { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }

    public class OrganizationUsageStats
    {
        public decimal TotalCost { get; set; }
        public int TotalCalls { get; set; }
        public int TotalTokens { get; set; }
        public decimal AverageDailyCost { get; set; }
        public int UniqueUsers { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class UsageChartData
    {
        public List<UsageDataPoint> DataPoints { get; set; } = new();
        public decimal TotalCost { get; set; }
        public int TotalCalls { get; set; }
        public int TotalTokens { get; set; }
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class UsageDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Cost { get; set; }
        public int Calls { get; set; }
        public int Tokens { get; set; }
    }
}


