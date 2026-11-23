using MegaCrit.Sts2.Core.Localization;

namespace AaaCode.RelicForge;

internal static class ForgeText
{
    internal static bool Chinese => LocManager.Instance?.Language is "zhs" or "zht";
    internal static string Pick(string chinese, string english) => Chinese ? chinese : english;

    internal static void AddRestSiteLabels()
    {
        LocManager.Instance.GetTable("rest_site_ui").MergeWith(new()
        {
            ["OPTION_AAACODE_RELIC_FORGE.name"] = Pick("升级遗物", "Upgrade Relic"),
            ["OPTION_AAACODE_RELIC_FORGE.description"] = Pick(
                "花费4个碎片和本次火堆行动，将一件遗物替换为升级形态。当前碎片：{Fragments}。",
                "Spend 4 fragments and this rest-site action to upgrade a relic. Fragments: {Fragments}."),
            ["OPTION_AAACODE_RELIC_FORGE.descriptionDisabled"] = Pick(
                "需要4个碎片和一件可升级的遗物。当前碎片：{Fragments}。",
                "Requires 4 fragments and an upgradable relic. Fragments: {Fragments}."),
        });
    }
}
