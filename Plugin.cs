using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Sprocket;
using Sprocket.Vehicles;
using Sprocket.Gameplay.VehicleControl;
using Sprocket.Vehicles.Control;
using SprocketKeybinds;
using UnityEngine;
using UnityEngine.SceneManagement;
using SerializationInfo = Il2CppSystem.Runtime.Serialization.SerializationInfo;
namespace SprocketSmokeLaunchers;

[BepInPlugin(Guid, "Sprocket Smoke Launchers", "0.2.4")]
[BepInDependency(Keybinds.PluginGuid, ">=0.1.5 <0.2.4")]
public sealed class Plugin : BasePlugin
{
    public const string Guid="sprocket.smokelaunchers";
    internal static Plugin Instance=null!;
    internal static ModKeybind Fire=null!;
    internal static ConfigEntry<bool> Enabled=null!;
    internal static ConfigEntry<float> SoundVolume=null!;
    private Harmony? harmony, iconHarmony;
    public override void Load()
    {
        Instance=this;
        Enabled=Config.Bind("General","Enabled",true,"Enable firing. Disabling clears effects but never replenishes ammunition. Controls are in the shared Settings keybind menu.");
        SoundVolume=Config.Bind("Audio","LaunchVolume",.6f,new ConfigDescription("Authored launch and grenade-deployment sounds; zero mutes both.",new AcceptableValueRange<float>(0,1)));
        // Metadata is additive; action registration still uses the original button contract.
        var metadata=typeof(Keybinds).GetMethod("RegisterModMetadata",new[]{typeof(string),typeof(string),typeof(string)});
        metadata?.Invoke(null,new object[]{Guid,"Nero","Smoke Launchers"});
        Fire=Keybinds.RegisterButton(Guid,"Smoke Launchers","fire-salvo","Fire smoke salvo","<Keyboard>/g");
        harmony=new Harmony(Guid);
        try { harmony.PatchAll(typeof(Hooks)); AddComponent<SmokeDriver>(); }
        catch { harmony.UnpatchSelf(); Keybinds.Unregister(Guid,"fire-salvo"); throw; }
        try { iconHarmony=new Harmony(Guid+".icons"); iconHarmony.PatchAll(typeof(IconHooks)); }
        catch(Exception ex) { iconHarmony?.UnpatchSelf(); Runtime.Warn("Optional icon patch",ex); }
        Log.LogInfo("Smoke Launchers 0.2.4: three native tubes / one salvo per bank, visual smoke only; no AI or thermal occlusion claim. Configure Fire smoke salvo in Settings/keybinds.");
    }
    public override bool Unload()
    {
        Runtime.Shutdown(); iconHarmony?.UnpatchSelf(); IconHooks.Cleanup(); harmony?.UnpatchSelf(); Keybinds.Unregister(Guid,"fire-salvo"); return true;
    }
}
internal static class Runtime
{
    internal const string Tag="smokeLauncherBank", Component="smokeLauncherBankModel", SaveKey="smokeLauncherAmmoV1";
    internal const string PartGuid="ab3b7c19-7533-4dad-897f-ddb739234a16";
    private sealed class Bank
    {
        internal readonly VehicleObject Object;
        internal VehicleObjectModel Model;
        internal readonly SessionAmmo Ammo=new();
        internal bool BalanceReported, NativeLoaded, ResourceRequested;
        internal LaunchAudio.Voice? Voice;
        internal Bank(VehicleObjectModel model) { Object=model.VehicleObject; Model=model; }
    }
    private static readonly Dictionary<int,Bank> Banks=new();
    private static readonly SessionLedger Session=new();
    private static int observedMode;
    internal static void ObserveCombat(Sprocket.GameControl.GameController controller)
    {
        var mode=controller.gamemode;
        int id=mode==null ? 0 : mode.GetInstanceID();
        if(id!=observedMode) { EndCombat(); observedMode=id; }
        bool combat=false;
        if(mode!=null && mode.State==GameModeState.Running)
        {
            var scenario=mode.TryCast<ScenarioGameMode>();
            if(scenario!=null) combat=scenario.activeState!=null && scenario.mission!=null && scenario.activeState.Pointer==scenario.mission.Pointer && scenario.activeState.Active;
            else { var deathmatch=mode.TryCast<DeathmatchGameMode>(); combat=deathmatch!=null && deathmatch.missionRunning; }
        }
        if(combat) BeginCombat(); else if(Session.Active) EndCombat();
    }
    internal static void BeginCombat() { if(!Session.Active && Session.Begin(System.Guid.NewGuid().ToString("N"))) { CloudPool.Clear(); LaunchAudio.Clear(); Plugin.Instance.Log.LogInfo("[Smoke SESSION] New combat: banks prepared with one three-grenade salvo."); } }
    internal static void EndCombat() { Session.End(); CloudPool.Clear(); LaunchAudio.Clear(); }
    private static readonly List<Bank> Volley=new(SmokeRules.MaxBanks);
    private static readonly List<int> Removed=new(SmokeRules.MaxBanks);
    private static readonly HashSet<string> Warnings=new();
    private static bool effectsActive;
    private static GameTime? gameTime;
    private static int lastFireFrame=-1;
    private static float nextCleanup, nextFeedback;
    internal static bool Own(VehicleObjectModel model) => model.ComponentID==Component && model.VehicleObject?.HasTag(Tag)==true;
    internal static void Warn(string key,Exception ex) { if(Warnings.Add(key)) Plugin.Instance.Log.LogWarning($"[Smoke] {key}: {ex}"); }
    private static void Notice(string key,string message) { if(Warnings.Add(key)) Plugin.Instance.Log.LogWarning("[Smoke] "+message); }
    private static void Feedback(string message)
    { if(Time.unscaledTime<nextFeedback) return; nextFeedback=Time.unscaledTime+2; Plugin.Instance.Log.LogInfo("[Smoke] "+message); }
    private static Bank? Get(VehicleObjectModel model)
    {
        if(!Own(model)) return null;
        int id=model.VehicleObject.GetInstanceID();
        if(Banks.TryGetValue(id,out var bank)) { bank.Model=model; return bank; }
        if(Banks.Count>=SmokeRules.MaxBanks) { Notice("bank-cap","Bank registry limit reached (128); additional launchers disabled."); return null; }
        Banks.Add(id,bank=new(model)); return bank;
    }
    internal static void Prepare(VehicleObjectModel model,bool balance)
    {
        if(!Own(model)) return;
        var prepared=Get(model); if(prepared==null) return;
        if(model.defaultMeshGuid!=TriLauncherGeometry.MeshGuid)
        {
            if(!prepared.ResourceRequested)
            {
                prepared.ResourceRequested=true; prepared.NativeLoaded=false;
                model.defaultMeshGuid=TriLauncherGeometry.MeshGuid; model.defaultMaterialGuids=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(new[]{TriLauncherGeometry.MaterialGuid});
                model.RequestModelLoad(TriLauncherGeometry.MeshGuid,new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(new[]{TriLauncherGeometry.MaterialGuid}));
            }
            return;
        }
        // Match native VehicleModelFactory scaling/flipping; no custom sign or dynamic renderer override.
        model.autoApplyScale=true; model.autoFlipModel=true;
        if(model.HasModel)
        {
            if(!prepared.ResourceRequested) prepared.NativeLoaded=true;
            prepared.Voice ??= LaunchAudio.Create();
            // External equipment: own visual/selection geometry only; never alter attachments.
            model.generateDamageColliders=false;
            model.interactionColliderType &= ~Sprocket.Vehicles.Colliders.ColliderType.Physics;
            var colliders=model.Colliders;
            if(colliders!=null)
                for(int i=0;i<colliders.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<Sprocket.Vehicles.Colliders.IVehicleCollider>>().Count;i++)
                { var c=colliders[i]; if(c!=null && c.Owner?.Pointer==model.Pointer) c.Type &= ~Sprocket.Vehicles.Colliders.ColliderType.Physics; }
        }
        if(balance)
        {
            // Loaded bank stays 18kg after firing; no disappearing mass/COM impulse.
            // Verified native Model.Build assigns its info.mass to Armour and
            // material cost to 0.2429958582 * mass. Clear that adapter resource
            // before adding our loaded mechanism, avoiding an accidental 36kg bank.
            model.SetMass(0,MassType.Armour);
            model.SetCost(0,MassType.Armour,CostType.Material);
            model.SetMass(SmokeRules.BankMass,MassType.Mechanisms);
            model.cachedMass=model.GetMass(MassType.Everything); model.RecalculateCenterOfMass();
            model.SetCost(60,MassType.Mechanisms,CostType.Material);
            model.SetCost(60,MassType.Mechanisms,CostType.Assembly);
            var bank=prepared;
            if(!bank.BalanceReported)
            { bank.BalanceReported=true; Plugin.Instance.Log.LogInfo($"[Smoke BALANCE] object={model.VehicleObject.GetInstanceID()} mass={model.GetMass(MassType.Everything):F3} cached={model.CachedMass:F3} cost={model.GetCost(MassType.Everything,CostType.Everything):F3}; target 18kg / 120. Native display still requires live check."); }
        }
    }
    internal static void Save(VehicleComponent component,SerializationInfo info)
    {
        var model=component.TryCast<VehicleObjectModel>(); if(model==null) return;
        var bank=Get(model); if(bank!=null) info.AddValue(SaveKey,bank.Ammo.Save(Session));
    }
    internal static void Load(VehicleComponent component,SerializationInfo info)
    {
        var model=component.TryCast<VehicleObjectModel>(); if(model==null) return;
        var bank=Get(model); if(bank==null) return;
        string? value=null;
        // Enumerate to distinguish missing addon state from broken/unknown data.
        var entries=info.GetEnumerator();
        while(entries.MoveNext()) if(entries.Name==SaveKey) { try { value=info.GetString(SaveKey); } catch { bank.Ammo.Load(Session,"invalid"); throw; } break; }
        bank.Ammo.Load(Session,value);
    }
    internal static void Input(VehicleController controller,GameTime time)
    {
        gameTime=time; CloudPool.SetCamera(controller.ActiveCamera);
        if(!Session.Active || !Plugin.Enabled.Value || !Application.isFocused || time.PauseState!=PauseState.Unpaused || time.TimeScale<=0 || time.DeltaTime<=0 ||
           !SceneManager.GetSceneByName("VehicleControlUI").isLoaded || Keybinds.IsConfiguring || !Plugin.Fire.WasPressedThisFrame) return;
        var vehicle=controller.ControlledVehicle?.TryCast<VehicleBehaviour>();
        if(controller.ActiveCamera==null || vehicle?.Rigidbody==null || vehicle.ControlType!=ControlType.Player || (vehicle.Flags & VehicleFlags.Enabled)==0) return;
        if(lastFireFrame==Time.frameCount) return; lastFireFrame=Time.frameCount;
        int body=vehicle.Rigidbody.GetInstanceID(); Volley.Clear();
        foreach(var bank in Banks.Values)
        {
            var obj=bank.Object;
            if(obj==null || obj.Released || obj.markedForDestroy || !obj.PhysicsEnabled || bank.Ammo.IsSpent(Session) || bank.Model==null || !bank.Model.HasModel || !bank.NativeLoaded ||
                (obj.Flags & VehicleObjectFlags.Installed)==0 || bank.Model.HealthFraction<=0) continue;
            var owner=obj.Vehicle?.Behaviour?.TryCast<VehicleBehaviour>();
            if(owner?.Rigidbody==null || owner.Rigidbody.GetInstanceID()!=body) continue;
            Volley.Add(bank);
        }
        if(Volley.Count==0) { Feedback("No loaded, installed launcher banks on controlled vehicle."); return; }
        if(!CloudPool.Ready || !SmokeRules.CanVolley(Volley.Count,CloudPool.Free))
        { Feedback("Salvo postponed: effects pool warming/busy or more than eight loaded banks. No ammunition spent."); return; }
        // Reserve the entire volley before spending any bank: never partial cap consumption.
        int fired=0;
        foreach(var bank in Volley)
        {
            if(!bank.Ammo.TrySpend(Session)) continue; fired++;
            var model=bank.Model; var scale=model.GetModelScale();
            var soundPosition=Vector3.zero;
            for(int i=0;i<SmokeRules.Tubes;i++)
            {
                var mouth=TriLauncherGeometry.Muzzle(i); var axis=TriLauncherGeometry.Direction(i);
                var origin=model.ModelPosition+model.ModelRotation*Vector3.Scale(new Vector3(mouth.X,mouth.Y,mouth.Z),scale);
                var velocity=(model.ModelRotation*Vector3.Scale(new Vector3(axis.X,axis.Y,axis.Z),scale)).normalized*SmokeRules.Speed;
                soundPosition+=origin;
                // Include vehicle translation, bounded to prevent extreme physics glitches.
                velocity+=Vector3.ClampMagnitude(vehicle.Rigidbody.linearVelocity,30);
                CloudPool.Launch(origin,velocity);
            }
            LaunchAudio.Play(bank.Voice,soundPosition/SmokeRules.Tubes);
        }
        Plugin.Instance.Log.LogInfo($"[Smoke] Fired {fired} banks / {fired*SmokeRules.Tubes} grenades; each bank empty until the next combat/Play session. Visual smoke only.");
    }
    internal static void ModelLoaded(VehicleObjectModel model)
    { var bank=Get(model); if(bank!=null) { bank.NativeLoaded=model.defaultMeshGuid==TriLauncherGeometry.MeshGuid; bank.ResourceRequested=false; } }
    internal static void Remove(VehicleObject obj) { if(obj!=null && Banks.Remove(obj.GetInstanceID(),out var bank)) LaunchAudio.Release(bank.Voice); }
    internal static void Tick()
    {
        bool active=SceneManager.GetSceneByName("VehicleControlUI").isLoaded && Plugin.Enabled.Value;
        if(!active) { CloudPool.Clear(); gameTime=null; if(effectsActive) LaunchAudio.Clear(); }
        effectsActive=active; LaunchAudio.Warm();
        LaunchAudio.SetPaused(gameTime!=null && (gameTime.PauseState!=PauseState.Unpaused || gameTime.TimeScale<=0));
        CloudPool.Warm();
        if(gameTime!=null && gameTime.PauseState==PauseState.Unpaused && gameTime.TimeScale>0) CloudPool.Tick(Math.Clamp(gameTime.DeltaTime,0,.1f));
        if(Time.unscaledTime<nextCleanup) return; nextCleanup=Time.unscaledTime+1;
        Removed.Clear(); foreach(var pair in Banks) if(pair.Value.Object==null || pair.Value.Object.Released) Removed.Add(pair.Key);
        foreach(int id in Removed) { LaunchAudio.Release(Banks[id].Voice); Banks.Remove(id); }
    }
    internal static void Shutdown() { CloudPool.Shutdown(); Banks.Clear(); gameTime=null; LaunchAudio.Shutdown(); }
}
[HarmonyPatch]
internal static class Hooks
{
    [HarmonyPostfix,HarmonyPatch(typeof(Sprocket.GameControl.GameController),nameof(Sprocket.GameControl.GameController.SceneUpdate))]
    private static void ObserveGame(Sprocket.GameControl.GameController __instance)
    { try { Runtime.ObserveCombat(__instance); } catch(Exception ex) { Runtime.Warn("Combat observation",ex); } }

    [HarmonyPrefix,HarmonyPatch(typeof(VehicleObjectModel),nameof(VehicleObjectModel.Build))]
    private static void BeforeBuild(VehicleObjectModel __instance) { try { Runtime.Prepare(__instance,false); } catch(Exception ex) { Runtime.Warn("Prepare",ex); } }
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleObjectModel),nameof(VehicleObjectModel.Build))]
    private static void Build(VehicleObjectModel __instance) { try { Runtime.Prepare(__instance,true); } catch(Exception ex) { Runtime.Warn("Build",ex); } }
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleObjectModel),nameof(VehicleObjectModel.OnModelLoaded))]
    private static void Loaded(VehicleObjectModel __instance) { try { if(Runtime.Own(__instance)) { Runtime.ModelLoaded(__instance); Runtime.Prepare(__instance,false); __instance.ForceRebuild(); } } catch(Exception ex) { Runtime.Warn("Model load",ex); } }
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleController),nameof(VehicleController.UpdateControl))]
    private static void Input(VehicleController __instance,GameTime __0) { try { Runtime.Input(__instance,__0); } catch(Exception ex) { Runtime.Warn("Input",ex); } }
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleComponent),nameof(VehicleComponent.SaveData))]
    private static void Save(VehicleComponent __instance,SerializationInfo __0) { try { Runtime.Save(__instance,__0); } catch(Exception ex) { Runtime.Warn("Ammo save",ex); } }
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleComponent),nameof(VehicleComponent.LoadData))]
    private static void Load(VehicleComponent __instance,SerializationInfo __0) { try { Runtime.Load(__instance,__0); } catch(Exception ex) { Runtime.Warn("Ammo load",ex); } }
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleObject),nameof(VehicleObject.Release))]
    private static void Release(VehicleObject __instance) => Runtime.Remove(__instance);
}
public sealed class SmokeDriver : MonoBehaviour
{
    public SmokeDriver(IntPtr pointer):base(pointer){}
    public void LateUpdate() { try { Runtime.Tick(); } catch(Exception ex) { Runtime.Warn("Cloud driver",ex); } }
}









