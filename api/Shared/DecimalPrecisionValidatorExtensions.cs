using FluentValidation;

namespace EnergyTracker.Api.Shared;

public static class DecimalPrecisionValidatorExtensions
{
    public static IRuleBuilderOptions<T, decimal> DecimalPrecision<T>(
        this IRuleBuilderOptions<T, decimal> ruleBuilder, int scale, int precision = 18) =>
        ruleBuilder.PrecisionScale(precision, scale, true);

    public static IRuleBuilderOptions<T, decimal?> DecimalPrecision<T>(
        this IRuleBuilderOptions<T, decimal?> ruleBuilder, int scale, int precision = 18) =>
        ruleBuilder.PrecisionScale(precision, scale, true);
}
