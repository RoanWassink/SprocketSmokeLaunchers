using HarmonyLib;
using Sprocket;
using Sprocket.PartImporting;
namespace SprocketSmokeLaunchers;
internal static class IconHooks
{
    [HarmonyPostfix,HarmonyPatch(typeof(PartDefinitionCardFactory),nameof(PartDefinitionCardFactory.CreateCard))]
    private static void Card(PartDefinition __0,PartDisplayCard __result)
    {
        if(__0?.guid!=Runtime.PartGuid || __result==null) return;
        try { var native=GameIcons.GetOrDefault("germanSmokeLauncher"); if(native!=null) __result.Icon=native; }
        catch(Exception ex) { Runtime.Warn("Optional native launcher icon",ex); }
    }
    internal static void Cleanup() {} // borrowed native sprite is never destroyed
}
