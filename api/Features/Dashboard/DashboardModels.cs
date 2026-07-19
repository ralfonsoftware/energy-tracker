namespace EnergyTracker.Api.Features.Dashboard;

public record CostSummary(
    decimal DailyAvgCost,
    decimal WeeklyAvgCost,
    decimal ProjectedMonthlyCost,
    bool HasCostGap,
    int CoveredDays,
    int TotalDays,
    bool CostDetailAvailable
);

public record DashboardSummary(
    decimal DailyAvgKwh,
    decimal WeeklyAvgKwh,
    decimal TodayKwh,
    decimal DailyBudgetKwh,
    DateTimeOffset? LastReadingDate,
    string[] SpikeDays,
    CostSummary? Cost,
    decimal? LastKwhValue,
    DailyConsumptionPoint[] DailyConsumption
);

public record DailyConsumptionPoint(string Date, decimal KwhValue, bool WasMeterReset);
