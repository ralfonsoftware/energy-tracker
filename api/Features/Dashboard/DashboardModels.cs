namespace EnergyTracker.Api.Features.Dashboard;

public record DashboardSummary(
    decimal DailyAvgKwh,
    decimal WeeklyAvgKwh,
    decimal DailyAvgCost,
    decimal WeeklyAvgCost,
    decimal ProjectedMonthlyCost,
    DateTimeOffset? LastReadingDate,
    decimal TodayKwh,
    decimal DailyBudgetKwh,
    string[] SpikeDays
);
