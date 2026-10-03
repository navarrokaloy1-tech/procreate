namespace ProCreateApi.Services.Search;

/// <summary>
/// Shared pieces of the list-screen search boxes.
///
/// Every search goes through LIKE rather than Contains: Contains compiles to
/// SQLite's instr, which is case-sensitive, so "dela cruz" would miss "Dela
/// Cruz". Queries are also split into words that must each match something,
/// which is what makes a complete name match when no single column holds it —
/// "Juan Dela Cruz" spans FirstName and LastName, and the pickers display
/// "LastName, FirstName".
/// </summary>
public static class SearchTerms
{
    /// <summary>Passed to EF.Functions.Like so the escapes below are honoured.</summary>
    public const string Escape = "\\";

    public static string[] Tokenize(string search) =>
        search.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// A contains-pattern. Wildcards typed by the user are escaped so a
    /// literal "%" matches itself instead of everything.
    /// </summary>
    public static string Pattern(string token) =>
        "%" + token.Replace(Escape, Escape + Escape).Replace("%", Escape + "%").Replace("_", Escape + "_") + "%";
}
