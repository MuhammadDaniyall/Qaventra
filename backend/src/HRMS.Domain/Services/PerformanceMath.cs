namespace HRMS.Domain.Services;

public record ReviewSection(string Name, decimal Weight, ReviewSectionKind Kind, List<ReviewSectionItem> Items);

public record ReviewSectionItem(string Title, string? Description);

public record ScoredItem(int SectionIndex, decimal Weight, int? Score);

public record RaiseSlab(decimal MinScore, decimal RaisePercent);

public static class PerformanceMath
{
    public static readonly string[] DefaultLabels = ["Unsatisfactory", "Needs improvement", "Meets expectations", "Exceeds expectations", "Outstanding"];
    public static readonly int[] DefaultBellCurve = [5, 15, 50, 20, 10];
    public static readonly RaiseSlab[] DefaultRaises = [new(4.5m, 15), new(4m, 10), new(3m, 7), new(2m, 3)];

    /// <summary>
    /// Overall score on the 1–5 scale: each section's weighted average of its rated items, then the sections weighted
    /// against each other. Sections with nothing rated are left out and the rest re-weighted. Null when nothing is rated.
    /// </summary>
    public static decimal? Score(IReadOnlyList<ReviewSection> sections, IEnumerable<ScoredItem> items)
    {
        var list = items.ToList();
        decimal total = 0, weights = 0;
        for (var i = 0; i < sections.Count; i++)
        {
            var rated = list.Where(x => x.SectionIndex == i && x.Score is not null).ToList();
            if (rated.Count == 0)
                continue;
            var itemWeights = rated.Sum(x => x.Weight);
            var sectionScore = itemWeights > 0
                ? rated.Sum(x => x.Score!.Value * x.Weight) / itemWeights
                : (decimal)rated.Average(x => x.Score!.Value);
            total += sectionScore * sections[i].Weight;
            weights += sections[i].Weight;
        }
        return weights == 0 ? null : Math.Round(total / weights, 2);
    }

    /// <summary>A 1–5 score as a whole rating, rounding halves up.</summary>
    public static int Rating(decimal score) => (int)Math.Clamp(Math.Round(score, 0, MidpointRounding.AwayFromZero), 1, 5);

    /// <summary>Performance axis of the 9-box: 1 (ratings 1–2), 2 (rating 3), 3 (ratings 4–5).</summary>
    public static int PerformanceBand(int rating) => rating <= 2 ? 1 : rating == 3 ? 2 : 3;

    /// <summary>The raise of the highest slab the score reaches; 0 below every slab.</summary>
    public static decimal Raise(decimal score, IEnumerable<RaiseSlab> slabs) =>
        slabs.Where(s => score >= s.MinScore).Select(s => s.RaisePercent).DefaultIfEmpty(0).Max();
}
