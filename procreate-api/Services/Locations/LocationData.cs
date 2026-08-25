namespace ProCreateApi.Services.Locations;

public record Country(string Code, string Name);
public record Region(string Code, string Name);
public record Province(string Name);
public record City(string Name);

/// <summary>
/// Static address reference data for the registration form's cascading
/// Country -> Region -> Province -> City/Municipality dropdowns.
///
/// Held in memory rather than in the database: read-only lookup data that
/// changes on the order of years, so a table plus seeding would add migration
/// cost for no benefit.
///
/// COVERAGE, precisely:
///   - Regions:   all 17, using the common short names ("Davao Region")
///                rather than the numerals, which users do not recognise.
///   - Provinces: all 82, complete. NCR has no provinces, so it is modelled
///                with a single "Metro Manila" entry holding its 17 LGUs.
///   - Cities:    each province's CHARTERED CITIES plus its CAPITAL. This is
///                deliberately NOT the full ~1,490 municipality list — that
///                cannot be transcribed reliably by hand, and a 1,600-entry
///                <select> is unusable anyway. The UI therefore renders the
///                city field as a combobox: these values appear as
///                suggestions, but any municipality can be typed. To get
///                exhaustive coverage, import the PSGC dataset published by
///                the Philippine Statistics Authority and back RegionsFor /
///                ProvincesFor / CitiesFor with it — the shape already fits.
///
/// Region/province/city data is provided for PH only; other countries return
/// empty lists and the UI falls back to free-text entry.
/// </summary>
public static class LocationData
{
    public static readonly IReadOnlyList<Country> Countries = new List<Country>
    {
        new("PH", "Philippines"),
        new("AU", "Australia"),
        new("CA", "Canada"),
        new("CN", "China"),
        new("HK", "Hong Kong"),
        new("ID", "Indonesia"),
        new("JP", "Japan"),
        new("KR", "South Korea"),
        new("MY", "Malaysia"),
        new("NZ", "New Zealand"),
        new("QA", "Qatar"),
        new("SA", "Saudi Arabia"),
        new("SG", "Singapore"),
        new("TW", "Taiwan"),
        new("TH", "Thailand"),
        new("AE", "United Arab Emirates"),
        new("GB", "United Kingdom"),
        new("US", "United States"),
        new("VN", "Vietnam"),
    };

    /// <summary>
    /// Short, recognisable region names. Codes stay stable so stored records
    /// keep resolving if a display name is ever reworded.
    /// </summary>
    private static readonly IReadOnlyList<Region> PhRegions = new List<Region>
    {
        new("NCR", "Metro Manila"),
        new("CAR", "Cordillera Administrative Region"),
        new("R1", "Ilocos Region"),
        new("R2", "Cagayan Valley"),
        new("R3", "Central Luzon"),
        new("R4A", "CALABARZON"),
        new("MIM", "MIMAROPA"),
        new("R5", "Bicol Region"),
        new("R6", "Western Visayas"),
        new("R7", "Central Visayas"),
        new("R8", "Eastern Visayas"),
        new("R9", "Zamboanga Peninsula"),
        new("R10", "Northern Mindanao"),
        new("R11", "Davao Region"),
        new("R12", "SOCCSKSARGEN"),
        new("R13", "Caraga"),
        new("BARMM", "Bangsamoro"),
    };

    /// <summary>All 82 provinces, plus Metro Manila standing in for NCR.</summary>
    private static readonly Dictionary<string, string[]> PhProvinces = new()
    {
        ["NCR"] = new[] { "Metro Manila" },
        ["CAR"] = new[] { "Abra", "Apayao", "Benguet", "Ifugao", "Kalinga", "Mountain Province" },
        ["R1"] = new[] { "Ilocos Norte", "Ilocos Sur", "La Union", "Pangasinan" },
        ["R2"] = new[] { "Batanes", "Cagayan", "Isabela", "Nueva Vizcaya", "Quirino" },
        ["R3"] = new[] { "Aurora", "Bataan", "Bulacan", "Nueva Ecija", "Pampanga", "Tarlac", "Zambales" },
        ["R4A"] = new[] { "Batangas", "Cavite", "Laguna", "Quezon", "Rizal" },
        ["MIM"] = new[] { "Marinduque", "Occidental Mindoro", "Oriental Mindoro", "Palawan", "Romblon" },
        ["R5"] = new[] { "Albay", "Camarines Norte", "Camarines Sur", "Catanduanes", "Masbate", "Sorsogon" },
        ["R6"] = new[] { "Aklan", "Antique", "Capiz", "Guimaras", "Iloilo", "Negros Occidental" },
        ["R7"] = new[] { "Bohol", "Cebu", "Negros Oriental", "Siquijor" },
        ["R8"] = new[] { "Biliran", "Eastern Samar", "Leyte", "Northern Samar", "Samar", "Southern Leyte" },
        ["R9"] = new[] { "Zamboanga del Norte", "Zamboanga del Sur", "Zamboanga Sibugay" },
        ["R10"] = new[] { "Bukidnon", "Camiguin", "Lanao del Norte", "Misamis Occidental", "Misamis Oriental" },
        ["R11"] = new[] { "Davao de Oro", "Davao del Norte", "Davao del Sur", "Davao Occidental", "Davao Oriental" },
        ["R12"] = new[] { "Cotabato", "Sarangani", "South Cotabato", "Sultan Kudarat" },
        ["R13"] = new[] { "Agusan del Norte", "Agusan del Sur", "Dinagat Islands", "Surigao del Norte", "Surigao del Sur" },
        ["BARMM"] = new[] { "Basilan", "Lanao del Sur", "Maguindanao del Norte", "Maguindanao del Sur", "Sulu", "Tawi-Tawi" },
    };

    /// <summary>
    /// Cities and provincial capitals, keyed by province. Suggestions, not an
    /// exhaustive municipality list — see the class remarks.
    /// </summary>
    private static readonly Dictionary<string, string[]> PhCities = new()
    {
        // NCR
        ["Metro Manila"] = new[]
        {
            "Caloocan", "Las Piñas", "Makati", "Malabon", "Mandaluyong", "Manila",
            "Marikina", "Muntinlupa", "Navotas", "Parañaque", "Pasay", "Pasig",
            "Pateros", "Quezon City", "San Juan", "Taguig", "Valenzuela",
        },

        // CAR
        ["Abra"] = new[] { "Bangued" },
        ["Apayao"] = new[] { "Kabugao" },
        ["Benguet"] = new[] { "Baguio", "La Trinidad" },
        ["Ifugao"] = new[] { "Lagawe" },
        ["Kalinga"] = new[] { "Tabuk" },
        ["Mountain Province"] = new[] { "Bontoc" },

        // Ilocos Region
        ["Ilocos Norte"] = new[] { "Batac", "Laoag" },
        ["Ilocos Sur"] = new[] { "Candon", "Vigan" },
        ["La Union"] = new[] { "San Fernando" },
        ["Pangasinan"] = new[] { "Alaminos", "Dagupan", "Lingayen", "San Carlos", "Urdaneta" },

        // Cagayan Valley
        ["Batanes"] = new[] { "Basco" },
        ["Cagayan"] = new[] { "Tuguegarao" },
        ["Isabela"] = new[] { "Cauayan", "Ilagan", "Santiago" },
        ["Nueva Vizcaya"] = new[] { "Bayombong" },
        ["Quirino"] = new[] { "Cabarroguis" },

        // Central Luzon
        ["Aurora"] = new[] { "Baler" },
        ["Bataan"] = new[] { "Balanga" },
        ["Bulacan"] = new[] { "Malolos", "Meycauayan", "San Jose del Monte" },
        ["Nueva Ecija"] = new[] { "Cabanatuan", "Gapan", "Muñoz", "Palayan", "San Jose" },
        ["Pampanga"] = new[] { "Angeles", "Mabalacat", "San Fernando" },
        ["Tarlac"] = new[] { "Tarlac City" },
        ["Zambales"] = new[] { "Iba", "Olongapo" },

        // CALABARZON
        ["Batangas"] = new[] { "Batangas City", "Calaca", "Lipa", "Santo Tomas", "Tanauan" },
        ["Cavite"] = new[]
        {
            "Bacoor", "Carmona", "Cavite City", "Dasmariñas", "General Trias",
            "Imus", "Tagaytay", "Trece Martires", "Trece Martires City",
        },
        ["Laguna"] = new[] { "Biñan", "Cabuyao", "Calamba", "San Pablo", "San Pedro", "Santa Rosa", "Santa Cruz" },
        ["Quezon"] = new[] { "Lucena", "Tayabas" },
        ["Rizal"] = new[] { "Antipolo" },

        // MIMAROPA
        ["Marinduque"] = new[] { "Boac" },
        ["Occidental Mindoro"] = new[] { "Mamburao" },
        ["Oriental Mindoro"] = new[] { "Calapan" },
        ["Palawan"] = new[] { "Puerto Princesa" },
        ["Romblon"] = new[] { "Romblon" },

        // Bicol Region
        ["Albay"] = new[] { "Legazpi", "Ligao", "Tabaco" },
        ["Camarines Norte"] = new[] { "Daet" },
        ["Camarines Sur"] = new[] { "Iriga", "Naga", "Pili" },
        ["Catanduanes"] = new[] { "Virac" },
        ["Masbate"] = new[] { "Masbate City" },
        ["Sorsogon"] = new[] { "Sorsogon City" },

        // Western Visayas
        ["Aklan"] = new[] { "Kalibo" },
        ["Antique"] = new[] { "San Jose de Buenavista" },
        ["Capiz"] = new[] { "Roxas" },
        ["Guimaras"] = new[] { "Jordan" },
        ["Iloilo"] = new[] { "Iloilo City", "Passi" },
        ["Negros Occidental"] = new[]
        {
            "Bacolod", "Bago", "Cadiz", "Escalante", "Himamaylan", "Kabankalan",
            "La Carlota", "Sagay", "San Carlos", "Silay", "Sipalay", "Talisay", "Victorias",
        },

        // Central Visayas
        ["Bohol"] = new[] { "Tagbilaran" },
        ["Cebu"] = new[]
        {
            "Bogo", "Carcar", "Cebu City", "Danao", "Lapu-Lapu", "Mandaue",
            "Naga", "Talisay", "Toledo",
        },
        ["Negros Oriental"] = new[] { "Bais", "Bayawan", "Canlaon", "Dumaguete", "Guihulngan", "Tanjay" },
        ["Siquijor"] = new[] { "Siquijor" },

        // Eastern Visayas
        ["Biliran"] = new[] { "Naval" },
        ["Eastern Samar"] = new[] { "Borongan" },
        ["Leyte"] = new[] { "Baybay", "Ormoc", "Tacloban" },
        ["Northern Samar"] = new[] { "Catarman" },
        ["Samar"] = new[] { "Calbayog", "Catbalogan" },
        ["Southern Leyte"] = new[] { "Maasin" },

        // Zamboanga Peninsula
        ["Zamboanga del Norte"] = new[] { "Dapitan", "Dipolog" },
        ["Zamboanga del Sur"] = new[] { "Pagadian", "Zamboanga City" },
        ["Zamboanga Sibugay"] = new[] { "Ipil" },

        // Northern Mindanao
        ["Bukidnon"] = new[] { "Malaybalay", "Valencia" },
        ["Camiguin"] = new[] { "Mambajao" },
        ["Lanao del Norte"] = new[] { "Iligan", "Tubod" },
        ["Misamis Occidental"] = new[] { "Oroquieta", "Ozamiz", "Tangub" },
        ["Misamis Oriental"] = new[] { "Cagayan de Oro", "El Salvador", "Gingoog" },

        // Davao Region
        ["Davao de Oro"] = new[] { "Nabunturan" },
        ["Davao del Norte"] = new[] { "Panabo", "Samal", "Tagum" },
        ["Davao del Sur"] = new[] { "Davao City", "Digos" },
        ["Davao Occidental"] = new[] { "Malita" },
        ["Davao Oriental"] = new[] { "Mati" },

        // SOCCSKSARGEN
        ["Cotabato"] = new[] { "Kidapawan" },
        ["Sarangani"] = new[] { "Alabel" },
        ["South Cotabato"] = new[] { "General Santos", "Koronadal" },
        ["Sultan Kudarat"] = new[] { "Isulan", "Tacurong" },

        // Caraga
        ["Agusan del Norte"] = new[] { "Butuan", "Cabadbaran" },
        ["Agusan del Sur"] = new[] { "Bayugan", "Prosperidad" },
        ["Dinagat Islands"] = new[] { "San Jose" },
        ["Surigao del Norte"] = new[] { "Surigao City" },
        ["Surigao del Sur"] = new[] { "Bislig", "Tandag" },

        // BARMM
        ["Basilan"] = new[] { "Isabela City", "Lamitan" },
        ["Lanao del Sur"] = new[] { "Marawi" },
        ["Maguindanao del Norte"] = new[] { "Datu Odin Sinsuat" },
        ["Maguindanao del Sur"] = new[] { "Buluan" },
        ["Sulu"] = new[] { "Jolo" },
        ["Tawi-Tawi"] = new[] { "Bongao" },
    };

    public static IReadOnlyList<Region> RegionsFor(string? countryCode) =>
        NormaliseCountry(countryCode) == "PH" ? PhRegions : Array.Empty<Region>();

    public static IReadOnlyList<Province> ProvincesFor(string? countryCode, string? region)
    {
        if (NormaliseCountry(countryCode) != "PH") return Array.Empty<Province>();

        var code = ResolveRegionCode(region);

        return code is not null && PhProvinces.TryGetValue(code, out var provinces)
            ? provinces.Select(p => new Province(p)).ToList()
            : Array.Empty<Province>();
    }

    public static IReadOnlyList<City> CitiesFor(string? countryCode, string? province)
    {
        if (NormaliseCountry(countryCode) != "PH" || string.IsNullOrWhiteSpace(province))
            return Array.Empty<City>();

        return PhCities.TryGetValue(province, out var cities)
            ? cities.Select(c => new City(c)).ToList()
            : Array.Empty<City>();
    }

    /// <summary>Accepts a region code ("R11") or its display name ("Davao Region").</summary>
    private static string? ResolveRegionCode(string? region)
    {
        if (string.IsNullOrWhiteSpace(region)) return null;
        if (PhProvinces.ContainsKey(region)) return region;

        return PhRegions.FirstOrDefault(r =>
            string.Equals(r.Name, region, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    /// <summary>Accepts "PH" or "Philippines" so either form works.</summary>
    private static string? NormaliseCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return null;

        var match = Countries.FirstOrDefault(c =>
            string.Equals(c.Code, country, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Name, country, StringComparison.OrdinalIgnoreCase));

        return match?.Code;
    }
}
