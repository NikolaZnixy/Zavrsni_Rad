namespace Data.Model.Data
{
    /// <summary>
    /// A bank/ASPSP entry with the icon shown in the "choose your bank" picker.
    /// </summary>
    public class BankIcon
    {
        /// <summary>Must match the "name" Enable Banking returns from GET /aspsps exactly.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Site-relative wwwroot path, e.g. "/images/banks/n26.svg". Drop the actual file at Web/wwwroot + this path.</summary>
        public string IconPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Static lookup of known ASPSPs (currently: Enable Banking's Croatia list) to their local icon.
    /// This is hand-curated, not synced from the API at runtime - see the conversation in this session
    /// for why a DB table wasn't worth it for a list this static.
    /// </summary>
    public static class BankIcons
    {
        private const string BasePath = "/images/banks/";

        public static readonly BankIcon N26 = new() { Name = "N26", IconPath = BasePath + "n26.svg" };
        public static readonly BankIcon Revolut = new() { Name = "Revolut", IconPath = BasePath + "revolut.svg" };
        public static readonly BankIcon Bunq = new() { Name = "bunq", IconPath = BasePath + "bunq.svg" };
        public static readonly BankIcon ErsteSteiermarkischeBank = new() { Name = "Erste & Steiermärkische Bank", IconPath = BasePath + "erste-steiermarkische-bank.svg" };
        public static readonly BankIcon ZagrebackaBanka = new() { Name = "Zagrebačka banka", IconPath = BasePath + "zagrebacka-banka.svg" };
        public static readonly BankIcon PayPal = new() { Name = "PayPal", IconPath = BasePath + "paypal.svg" };
        public static readonly BankIcon AddikoBank = new() { Name = "Addiko Bank", IconPath = BasePath + "addiko-bank.svg" };
        public static readonly BankIcon Pbz = new() { Name = "PBZ", IconPath = BasePath + "pbz.svg" };
        public static readonly BankIcon BksBank = new() { Name = "BKS Bank", IconPath = BasePath + "bks-bank.svg" };
        public static readonly BankIcon DiPocket = new() { Name = "DiPocket", IconPath = BasePath + "dipocket.svg" };
        public static readonly BankIcon SumUp = new() { Name = "SumUp", IconPath = BasePath + "sumup.svg" };
        public static readonly BankIcon Hpb = new() { Name = "HPB", IconPath = BasePath + "hpb.svg" };
        public static readonly BankIcon IBanFirst = new() { Name = "iBanFirst", IconPath = BasePath + "ibanfirst.png" };
        public static readonly BankIcon Wise = new() { Name = "Wise", IconPath = BasePath + "wise.svg" };
        public static readonly BankIcon SlatinskaBanka = new() { Name = "Slatinska Banka", IconPath = BasePath + "slatinska-banka.svg" };
        public static readonly BankIcon SamoborskaBanka = new() { Name = "Samoborska Banka", IconPath = BasePath + "samoborska-banka.svg" };
        public static readonly BankIcon Ikb = new() { Name = "IKB", IconPath = BasePath + "ikb.svg" };
        public static readonly BankIcon AgramBanka = new() { Name = "Agram Banka", IconPath = BasePath + "agram-banka.svg" };
        public static readonly BankIcon PodravskaBanka = new() { Name = "Podravska Banka", IconPath = BasePath + "podravska-banka.svg" };
        public static readonly BankIcon CroatiaBanka = new() { Name = "Croatia Banka", IconPath = BasePath + "croatia-banka.svg" };
        public static readonly BankIcon OtpBanka = new() { Name = "OTP banka", IconPath = BasePath + "otp-banka.svg" };
        public static readonly BankIcon Wamo = new() { Name = "Wamo", IconPath = BasePath + "wamo.svg" };
        public static readonly BankIcon Finom = new() { Name = "Finom", IconPath = BasePath + "finom.svg" };
        public static readonly BankIcon Raiffeisen = new() { Name = "Raiffeisen", IconPath = BasePath + "raiffeisen.svg" };
        public static readonly BankIcon VivaCom = new() { Name = "Viva.com", IconPath = BasePath + "viva-com.svg" };
        public static readonly BankIcon FerratumBank = new() { Name = "Ferratum Bank", IconPath = BasePath + "ferratum-bank.svg" };
        public static readonly BankIcon Finductive = new() { Name = "Finductive", IconPath = BasePath + "finductive.svg" };
        public static readonly BankIcon Swan = new() { Name = "Swan", IconPath = BasePath + "swan.svg" };
        public static readonly BankIcon Airwallex = new() { Name = "Airwallex", IconPath = BasePath + "airwallex.svg" };

        /// <summary>Shown when an ASPSP name has no curated icon (e.g. a new one Enable Banking adds later).</summary>
        public static readonly BankIcon Generic = new() { Name = "Generic", IconPath = BasePath + "generic-bank.svg" };

        public static readonly IReadOnlyList<BankIcon> All = new[]
        {
            N26, Revolut, Bunq, ErsteSteiermarkischeBank, ZagrebackaBanka, PayPal, AddikoBank, Pbz, BksBank,
            DiPocket, SumUp, Hpb, IBanFirst, Wise, SlatinskaBanka, SamoborskaBanka, Ikb, AgramBanka,
            PodravskaBanka, CroatiaBanka, OtpBanka, Wamo, Finom, Raiffeisen, VivaCom, FerratumBank,
            Finductive, Swan, Airwallex
        };

        /// <summary>Looks up an icon by the exact ASPSP name Enable Banking returns. Falls back to <see cref="Generic"/>.</summary>
        public static BankIcon GetByName(string name) =>
            All.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Generic;
    }
}
