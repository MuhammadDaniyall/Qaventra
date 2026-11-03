using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace HRMS.Api.Services;

/// <summary>What can be read from a CV file without AI: its text, then the email, phone, years of experience and a name.</summary>
public static partial class CvReader
{
    public record CvFacts(string? FirstName, string? LastName, string? Email, string? Phone, decimal? ExperienceYears);

    public static string Text(string fileName, byte[] data)
    {
        try
        {
            switch (Path.GetExtension(fileName).ToLowerInvariant())
            {
                case ".pdf":
                {
                    using var pdf = PdfDocument.Open(data);
                    var text = new StringBuilder();
                    foreach (var page in pdf.GetPages().Take(10)) text.AppendLine(page.Text);
                    return text.ToString();
                }
                case ".docx":
                {
                    using var stream = new MemoryStream(data);
                    using var doc = WordprocessingDocument.Open(stream, false);
                    return doc.MainDocumentPart?.Document?.Body?.InnerText ?? "";
                }
                default:
                    return "";
            }
        }
        catch (Exception)
        {
            // A damaged or protected file: the CV is still attached, only nothing is read from it.
            return "";
        }
    }

    public static CvFacts Read(string fileName, byte[] data)
    {
        var text = Text(fileName, data);
        var email = Email().Match(text) is { Success: true } e ? e.Value.ToLowerInvariant() : null;
        var phone = Phone().Match(text) is { Success: true } p ? p.Value.Trim() : null;
        decimal? years = Years().Match(text) is { Success: true } y && decimal.TryParse(y.Groups[1].Value, out var n) && n is > 0 and < 50 ? n : null;
        // The name: the file name without "CV"/"resume" and separators, e.g. "Ali_Raza_CV.pdf" → Ali Raza.
        var words = Separators().Split(Path.GetFileNameWithoutExtension(fileName))
            .Where(w => w.Length > 1 && !NoiseWords.Contains(w.ToLowerInvariant()) && !w.Any(char.IsDigit))
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()).ToList();
        return new CvFacts(words.FirstOrDefault(), words.Count > 1 ? string.Join(' ', words.Skip(1).Take(2)) : null, email, phone, years);
    }

    static readonly HashSet<string> NoiseWords = ["cv", "resume", "resum", "curriculum", "vitae", "updated", "latest", "new", "final", "copy", "profile"];

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"(\+92|0092|0)\s?3\d{2}[\s-]?\d{7}")]
    private static partial Regex Phone();

    [GeneratedRegex(@"(\d{1,2})\+?\s*(?:years|yrs)", RegexOptions.IgnoreCase)]
    private static partial Regex Years();

    [GeneratedRegex(@"[\s_\-.()\[\]]+")]
    private static partial Regex Separators();
}
