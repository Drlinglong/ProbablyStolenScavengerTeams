using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ScavengerTeams;

internal static partial class RegressionTests
{
    private static void LoadLocalization()
    {
        var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "content", "Localization"));
        foreach (var path in Directory.GetFiles(root, "*.csv"))
        {
            var table = new Dictionary<string, string>();
            // Match the native loader: first comma, then trim/unquote/decode escaped newline.
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                var comma = line.IndexOf(',');
                if (comma <= 0) throw new Exception("Malformed localization line: " + path);
                var key = line.Substring(0,comma).Trim();
                var value = line.Substring(comma+1).Trim();
                if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1,value.Length-2).Replace("\"\"", "\"");
                if (table.ContainsKey(key)) throw new Exception("Duplicate localization key: " + key);
                table.Add(key,value.Replace("\\n","\n"));
            }
            ModHelper.Locales[Path.GetFileNameWithoutExtension(path)] = table;
        }
    }
    private static void RunLocalizationTests()
    {
        Check("native-style CSV parsing covers all route fields and format arguments in both languages", () =>
        {
            foreach (var locale in new[] { "zh", "en" })
            {
                ModHelper.Locale=locale;
                Require(ModHelper.Locales[locale].Count == 46, "missing localization rows");
                foreach (var team in TeamDefinitions.All)
                {
                    foreach (var field in new[] { "material","visitor_name","crate_name","crate_flavor" })
                        Require(Text.For(team,field) != "team."+team.Key+"."+field, "unresolved localization key");
                    for (var level=1; level<=3; level++) Require(!team.Title(level).Contains("{0}"), "unexpanded level");
                    var crate=CrateItems.Create(team);
                    Require(crate.shortDescription.Contains("6") && !crate.shortDescription.Contains("{0}"), "unexpanded crate count");
                }
                NetworkRegistration.Register();
            }
            Require(ModHelper.Locales["zh"].Keys.OrderBy(k=>k).SequenceEqual(ModHelper.Locales["en"].Keys.OrderBy(k=>k)), "locale key drift");
            ModHelper.Locale="zh"; NetworkRegistration.Register();
        });
        Check("CSV-backed text changes affect new and saved crates without changing state or identities", () =>
        {
            var key="team.metal.crate_name"; var before=ModHelper.Locales["zh"][key];
            try
            {
                BuyUpgrade(1,TeamDefinitions.Metal);
                var state=TeamStateRepository.Read(TeamDefinitions.Metal);
                var crate=CrateItems.Create(TeamDefinitions.Metal);
                ModHelper.Locales["zh"][key]="编辑后的物品箱名称";
                Require(CrateItems.Create(TeamDefinitions.Metal).name=="编辑后的物品箱名称", "new crate ignored localized name");
                crate.onLoaded(crate);
                Require(crate.name=="编辑后的物品箱名称" && crate.identifier==TeamDefinitions.Metal.CrateId, "saved crate ignored text or changed ID");
                Require(TeamStateRepository.Read(TeamDefinitions.Metal).nextDay==state.nextDay, "text changed schedule");
            }
            finally { ModHelper.Locales["zh"][key]=before; }
        });
        Check("unavailable locale uses complete English fallback rather than raw keys", () =>
        {
            ModHelper.Locale="missing-locale";
            Require(TeamDefinitions.Ore.VisitorName=="Asteroid Miner", "fallback failed");
            var crate=CrateItems.Create(TeamDefinitions.Ore);
            Require(crate.shortDescription.StartsWith("Double-click"), "fallback description failed");
        });
        Check("resource titles use Roman levels and crate descriptions retain only the opening warning", () =>
        {
            var names = new[] { "3D打印耗材", "废金属", "电子元件", "矿石" };
            var roman = new[] { "I", "II", "III" };
            for (var i=0; i<TeamDefinitions.All.Count; i++)
            {
                var team=TeamDefinitions.All[i];
                for (var level=1; level<=3; level++)
                {
                    Require(team.Title(level)==names[i]+roman[level-1], "incorrect title");
                    var key="team."+team.Key+".service_description."+level;
                    Require(Text.Get(key)!=key && !Text.Get(key).Contains("{0}"), "missing level description");
                }
                var description=CrateItems.Create(team).shortDescription;
                Require(!description.Contains("3×3") && description.EndsWith("打开前请确保有足够的仓库空间。"), "crate description changed incorrectly");
            }
        });
        foreach (var route in TeamDefinitions.All)
        {
            var team=route;
            Check(team.Key+" purchase and upgrades greet once per level without changing delivery or discounts", () =>
            {
                var prefix=team==TeamDefinitions.Ore ? "team.ore." : "team.";
                BuyUpgrade(1,team);
                var first=Visit(team,11);
                Require(first.mainDialogue.Text==Text.Get(prefix+"intro.1"), "missing first introduction");
                Reload(); first=Visit(team,11);
                Require(first.mainDialogue.Text==Text.Get(prefix+"intro.1") && first.wholesaleDiscount==-50, "reload changed introduction or special");
                first.OnDismiss(); Reload();
                var normal=Visit(team,14);
                Require(normal.mainDialogue.Text==Text.Get(prefix+"greeting") && normal.wholesaleDiscount==-15, "introduction repeated");
                normal.OnDismiss();
                for (var level=2; level<=3; level++)
                {
                    var due=TeamStateRepository.Read(team).nextDay;
                    BuyUpgrade(level,team);
                    Require(TeamStateRepository.Read(team).nextDay==due, "upgrade reset schedule");
                    Reload(); var upgraded=Visit(team,due);
                    Require(upgraded.mainDialogue.Text==Text.Get(prefix+"intro."+level), "missing upgrade introduction");
                    Require(upgraded.wholesaleDiscount==-15, "upgrade reset discount");
                    upgraded.OnDismiss(); Reload();
                    normal=Visit(team,due+3);
                    Require(normal.mainDialogue.Text==Text.Get(prefix+"greeting"), "upgrade introduction repeated");
                    normal.OnDismiss();
                }
            });
        }
        Check("legacy delivery state seeds greeting progress without resetting saved offer data", () =>
        {
            var state=UnityEngine.JsonUtility.FromJson<DeliveryState>(
                "{\"version\":1,\"nextDay\":14,\"offerDay\":11,\"offerLevel\":2,\"soldMask\":1,\"closed\":false,\"firstOffer\":true}");
            Require(DeliveryRules.Valid(state), "legacy state rejected");
            DeliveryRules.Arrive(state,11,3);
            Require(state.offerGreetingLevel==0 && state.offerLevel==2 && state.soldMask==1 && state.nextDay==14 && state.firstOffer, "restore changed legacy batch");
            DeliveryRules.Arrive(state,14,3);
            Require(state.offerGreetingLevel==3 && state.greetedLevel==3 && !state.firstOffer, "legacy upgrade greeting failed");
            DeliveryRules.Arrive(state,17,3);
            Require(state.offerGreetingLevel==0, "legacy upgrade repeated");
        });
        Check("upgrading before the first delivery uses only the current level introduction", () =>
        {
            BuyUpgrade(1); BuyUpgrade(2); BuyUpgrade(3);
            var client=Arrive(11);
            Require(client.mainDialogue.Text==Text.Get("team.intro.3") && client.wholesaleDiscount==-50, "wrong introduction or first discount");
        });
    }
}
