namespace IPAbuyer.Core.Configuration
{
    public sealed record AppleStorefront(string Code, string EnglishName)
    {
        public string SearchText => $"{Code} {EnglishName}";
    }

    public static class AppleStorefrontCatalog
    {
        private static readonly AppleStorefront[] Storefronts = Parse("""
AE|United Arab Emirates
AF|Afghanistan
AG|Antigua and Barbuda
AI|Anguilla
AL|Albania
AM|Armenia
AO|Angola
AR|Argentina
AT|Austria
AU|Australia
AZ|Azerbaijan
BA|Bosnia and Herzegovina
BB|Barbados
BE|Belgium
BF|Burkina Faso
BG|Bulgaria
BH|Bahrain
BJ|Benin
BM|Bermuda
BN|Brunei
BO|Bolivia
BR|Brazil
BS|Bahamas
BT|Bhutan
BW|Botswana
BY|Belarus
BZ|Belize
CA|Canada
CD|Democratic Republic of the Congo
CG|Republic of the Congo
CH|Switzerland
CI|Côte d’Ivoire
CL|Chile
CM|Cameroon
CN|China Mainland
CO|Colombia
CR|Costa Rica
CV|Cape Verde
CY|Cyprus
CZ|Czechia
DE|Germany
DK|Denmark
DM|Dominica
DO|Dominican Republic
DZ|Algeria
EC|Ecuador
EE|Estonia
EG|Egypt
ES|Spain
FI|Finland
FJ|Fiji
FM|Micronesia
FR|France
GA|Gabon
GB|United Kingdom
GD|Grenada
GE|Georgia
GH|Ghana
GM|Gambia
GR|Greece
GT|Guatemala
GW|Guinea-Bissau
GY|Guyana
HK|Hong Kong
HN|Honduras
HR|Croatia
HU|Hungary
ID|Indonesia
IE|Ireland
IQ|Iraq
IL|Israel
IN|India
IS|Iceland
IT|Italy
JM|Jamaica
JO|Jordan
JP|Japan
KE|Kenya
KG|Kyrgyzstan
KH|Cambodia
KN|Saint Kitts and Nevis
KR|South Korea
KW|Kuwait
KY|Cayman Islands
KZ|Kazakhstan
LA|Laos
LB|Lebanon
LC|Saint Lucia
LK|Sri Lanka
LR|Liberia
LT|Lithuania
LY|Libya
LU|Luxembourg
LV|Latvia
MD|Moldova
MA|Morocco
ME|Montenegro
MG|Madagascar
MK|North Macedonia
ML|Mali
MM|Myanmar
MN|Mongolia
MO|Macau
MR|Mauritania
MS|Montserrat
MT|Malta
MU|Mauritius
MV|Maldives
MW|Malawi
MX|Mexico
MY|Malaysia
MZ|Mozambique
NA|Namibia
NE|Niger
NG|Nigeria
NI|Nicaragua
NL|Netherlands
NO|Norway
NP|Nepal
NR|Nauru
NZ|New Zealand
OM|Oman
PA|Panama
PE|Peru
PG|Papua New Guinea
PH|Philippines
PK|Pakistan
PL|Poland
PT|Portugal
PW|Palau
PY|Paraguay
QA|Qatar
RO|Romania
RS|Serbia
RU|Russia
RW|Rwanda
SA|Saudi Arabia
SB|Solomon Islands
SC|Seychelles
SE|Sweden
SG|Singapore
SI|Slovenia
SK|Slovakia
SL|Sierra Leone
SN|Senegal
SR|Suriname
ST|São Tomé and Príncipe
SV|El Salvador
SZ|Eswatini
TC|Turks and Caicos Islands
TD|Chad
TH|Thailand
TJ|Tajikistan
TM|Turkmenistan
TN|Tunisia
TO|Tonga
TR|Türkiye
TT|Trinidad and Tobago
TW|Taiwan
TZ|Tanzania
UA|Ukraine
UG|Uganda
US|United States
UY|Uruguay
UZ|Uzbekistan
VC|Saint Vincent and the Grenadines
VE|Venezuela
VG|British Virgin Islands
VN|Vietnam
VU|Vanuatu
XK|Kosovo
YE|Yemen
ZA|South Africa
ZM|Zambia
ZW|Zimbabwe
""");

        public static IReadOnlyList<AppleStorefront> All { get; } = Storefronts
            .OrderBy(storefront => storefront.Code, StringComparer.Ordinal)
            .ToArray();

        public static bool Contains(string? code)
        {
            return !string.IsNullOrWhiteSpace(code)
                && Storefronts.Any(storefront => string.Equals(storefront.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static AppleStorefront[] Parse(string data)
        {
            return data.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('|'))
                .Select(parts => new AppleStorefront(parts[0].ToLowerInvariant(), parts[1]))
                .ToArray();
        }
    }
}
