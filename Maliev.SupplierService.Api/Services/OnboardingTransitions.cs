using Maliev.SupplierService.Data.Enums;

namespace Maliev.SupplierService.Api.Services;

public static class OnboardingTransitions
{
    private static readonly Dictionary<OnboardingStage, OnboardingStage[]> ValidTransitions = new()
    {
        [OnboardingStage.PendingApproval] = [OnboardingStage.DocumentationReview],
        [OnboardingStage.DocumentationReview] = [OnboardingStage.FinalApproval, OnboardingStage.PendingApproval],
        [OnboardingStage.FinalApproval] = [OnboardingStage.Active, OnboardingStage.DocumentationReview],
        [OnboardingStage.Active] = [] // Terminal state
    };

    public static bool IsValidTransition(OnboardingStage from, OnboardingStage to)
    {
        return ValidTransitions.TryGetValue(from, out var validTargets) && validTargets.Contains(to);
    }

    public static IReadOnlyList<OnboardingStage> GetValidNextStages(OnboardingStage current)
    {
        return ValidTransitions.TryGetValue(current, out var validTargets) ? validTargets : [];
    }
}
