namespace IPAbuyer.Core.Services.AppCatalog
{
    public sealed record DeveloperFilterOption(string DisplayName, int Count);

    public static class DeveloperFilter
    {
        public static IReadOnlyList<DeveloperFilterOption> BuildOptions(IEnumerable<string?> developerNames)
        {
            ArgumentNullException.ThrowIfNull(developerNames);

            var developers = new Dictionary<string, (string DisplayName, int Count)>(StringComparer.OrdinalIgnoreCase);
            foreach (string? name in developerNames)
            {
                string developer = name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(developer))
                {
                    continue;
                }

                if (developers.TryGetValue(developer, out var existing))
                {
                    developers[developer] = (existing.DisplayName, existing.Count + 1);
                }
                else
                {
                    developers.Add(developer, (developer, 1));
                }
            }

            return developers.Values
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new DeveloperFilterOption(item.DisplayName, item.Count))
                .ToArray();
        }

        public static bool Matches(string? developerName, string? selectedDeveloper)
        {
            return string.IsNullOrWhiteSpace(selectedDeveloper)
                || string.Equals(developerName?.Trim(), selectedDeveloper, StringComparison.OrdinalIgnoreCase);
        }
    }
}
