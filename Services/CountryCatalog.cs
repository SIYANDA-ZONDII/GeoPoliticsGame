
using GeopoliticsGame.Models;

namespace GeopoliticsGame.Services;

public static class CountryCatalog
{
    // Population source: Worldometer, 2026 estimates.
    // Verify all figures against the cited source before final submission.
    public static List<Country> GetAllCountries()
    {
        return new List<Country>
        {
            new() { Name = "South Africa", Population = 65_453_084 },
            new() { Name = "India", Population = 1_476_625_576 },
            new() { Name = "Brazil", Population = 213_562_666 },
            new() { Name = "Japan", Population = 122_427_731 },
            new() { Name = "Germany", Population = 83_644_258 },
            new() { Name = "Canada", Population = 40_467_728 },
            new() { Name = "Australia", Population = 27_227_096 },
            new() { Name = "Egypt", Population = 120_101_175 },
            new() { Name = "France", Population = 66_746_401 },
            new() { Name = "United States", Population = 349_035_494 },
            new() { Name = "China", Population = 1_412_914_089 },
            new() { Name = "Nigeria", Population = 242_431_832 },
            new() { Name = "United Kingdom", Population = 69_931_528 },
            new() { Name = "Italy", Population = 58_926_166 },
            new() { Name = "Mexico", Population = 132_997_658 },
            new() { Name = "Argentina", Population = 46_003_734 },
            new() { Name = "Spain", Population = 47_850_793 },
            new() { Name = "South Korea", Population = 51_600_388 },
            new() { Name = "Indonesia", Population = 287_886_782 },
            new() { Name = "Saudi Arabia", Population = 35_165_787 }
        };
    }
}
