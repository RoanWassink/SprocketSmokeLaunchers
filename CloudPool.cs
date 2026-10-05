using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace SprocketSmokeLaunchers;
// No ParticleSystem dependency: legacy scalar setters are throw-stubs in this game.
// Fixed pool of 48 cloud meshes, 16 camera-facing puffs / 32 triangles per cloud.
internal static class CloudPool
{
    private sealed class Slot
    {
        internal GameObject Root=null!, Grenade=null!;
        internal Mesh Mesh=null!;
        internal Il2CppStructArray<Vector3> Vertices=new(SmokeRules.PuffsPerCloud*4);
        internal Material Material=null!;
        internal bool Active, Smoking;
        internal readonly DeploymentCue Cue=new();
        internal LaunchAudio.Voice? BurstVoice;
        internal Vector3 Origin, Velocity;
        internal float Age;
    }
    private static readonly Slot?[] Slots=new Slot[SmokeRules.MaxClouds];
    private static Texture2D? texture;
    private static Material? grenadeMaterial;
    private static Camera? camera;
    private static int warmed, colorProperty;
    private static bool failed, stopped;
    internal static bool Ready => !failed && !stopped && warmed==Slots.Length;
    internal static void SetCamera(Camera? value) => camera=value;
    internal static int Free { get { int free=0; foreach(var s in Slots) if(s!=null && !s.Active) free++; return free; } }
    internal static void Warm()
    {
        if(failed || stopped || warmed==Slots.Length) return;
        try
        {
            if(texture==null) { colorProperty=Shader.PropertyToID("_UnlitColor"); CreateTexture(); }
            for(int count=0;count<2 && warmed<Slots.Length;count++) { Slots[warmed]=CreateSlot(warmed); warmed++; }
            if(warmed==Slots.Length) Plugin.Instance.Log.LogInfo("[Smoke] Mesh VFX pool ready: 48 slots / 768 puffs; no ParticleSystem setters, damage or LOS colliders.");
        }
        catch(Exception ex) { failed=true; Clear(); Runtime.Warn("VFX initialization; firing disabled",ex); }
    }
    private static void CreateTexture()
    {
        texture=new Texture2D(64,64,TextureFormat.RGBA32,false) {name="Procedural smoke puff",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color[64*64];
        for(int y=0;y<64;y++) for(int x=0;x<64;x++)
        {
            float u=(x-31.5f)/31.5f,v=(y-31.5f)/31.5f,r=u*u+v*v;
            float noise=.8f+.2f*MathF.Sin(x*.42f+y*.27f)*MathF.Sin(y*.33f-x*.21f);
            float alpha=Math.Clamp((1-r)*noise,0,1); alpha*=alpha;
            pixels[y*64+x]=new Color(.68f,.70f,.69f,alpha);
        }
        texture.SetPixels(pixels); texture.Apply(false,true);
    }
    private static Material SmokeMaterial()
    {
        var shader=Shader.Find("HDRP/Unlit") ?? throw new NotSupportedException("HDRP/Unlit unavailable");
        var mat=new Material(shader) {name="Smoke puff material",hideFlags=HideFlags.HideAndDontSave};
        mat.SetTexture("_UnlitColorMap",texture); mat.SetColor("_UnlitColor",Color.white); mat.SetColor("_EmissiveColor",Color.black);
        mat.SetFloat("_BlendMode",0); mat.SetFloat("_TransparentCullMode",0); mat.SetFloat("_CullMode",0);
        HDMaterial.SetSurfaceType(mat,true);
        if(!HDMaterial.ValidateMaterial(mat)) { UnityEngine.Object.Destroy(mat); throw new NotSupportedException("HDRP smoke material validation failed"); }
        mat.SetShaderPassEnabled("DepthForwardOnly",false); mat.SetShaderPassEnabled("MotionVectors",false);
        mat.SetShaderPassEnabled("TransparentDepthPrepass",false); mat.SetShaderPassEnabled("TransparentDepthPostpass",false);
        return mat;
    }
    private static Slot CreateSlot(int index)
    {
        var s=new Slot();
        try
        {
            s.Root=new GameObject("Smoke cloud "+index); s.Root.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(s.Root);
            s.Material=SmokeMaterial();
            s.Mesh=new Mesh {name="Smoke billboard cluster "+index,hideFlags=HideFlags.HideAndDontSave};
            s.Mesh.MarkDynamic();
            var uv=new Vector2[SmokeRules.PuffsPerCloud*4]; var triangles=new int[SmokeRules.PuffsPerCloud*6];
            for(int i=0;i<SmokeRules.PuffsPerCloud;i++)
            {
                int v=i*4,t=i*6;
                uv[v]=new(0,0); uv[v+1]=new(1,0); uv[v+2]=new(1,1); uv[v+3]=new(0,1);
                triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+2; triangles[t+3]=v; triangles[t+4]=v+2; triangles[t+5]=v+3;
            }
            UpdateMesh(s,Vector3.right,Vector3.up);
            s.Mesh.uv=uv; s.Mesh.triangles=triangles;
            // Covers the entire 25s layout, including drifting centres and corner size.
            s.Mesh.bounds=new Bounds(new Vector3(0,1,0),new Vector3(24,24,24));
            s.Root.AddComponent<MeshFilter>().sharedMesh=s.Mesh;
            var renderer=s.Root.AddComponent<MeshRenderer>(); renderer.sharedMaterial=s.Material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            s.Grenade=GameObject.CreatePrimitive(PrimitiveType.Sphere); s.Grenade.name="Smoke grenade";
            s.Grenade.transform.SetParent(s.Root.transform,false); s.Grenade.transform.localScale=Vector3.one*.075f;
            UnityEngine.Object.Destroy(s.Grenade.GetComponent<Collider>());
            if(grenadeMaterial==null)
            {
                grenadeMaterial=new Material(Shader.Find("HDRP/Unlit")) {hideFlags=HideFlags.HideAndDontSave};
                grenadeMaterial.SetColor("_UnlitColor",new Color(.17f,.20f,.14f,1)); HDMaterial.ValidateMaterial(grenadeMaterial);
            }
            s.Grenade.GetComponent<Renderer>().sharedMaterial=grenadeMaterial;
            s.Grenade.SetActive(false); s.BurstVoice=LaunchAudio.Create(4,75); return s;
        }
        catch { if(s.Root!=null) UnityEngine.Object.Destroy(s.Root); if(s.Material!=null) UnityEngine.Object.Destroy(s.Material); if(s.Mesh!=null) UnityEngine.Object.Destroy(s.Mesh); throw; }
    }
    private static void UpdateMesh(Slot s,Vector3 right,Vector3 up)
    {
        var r=new System.Numerics.Vector3(right.x,right.y,right.z); var u=new System.Numerics.Vector3(up.x,up.y,up.z);
        for(int i=0;i<SmokeRules.PuffsPerCloud;i++) for(int j=0;j<4;j++)
        { var p=PuffLayout.Corner(i,j,s.Age,r,u); s.Vertices[i*4+j]=new Vector3(p.X,p.Y,p.Z); }
        // Persistent native buffer: no managed/native arrays allocated during a frame.
        s.Mesh.vertices=s.Vertices;
    }
    internal static void Launch(Vector3 origin,Vector3 velocity)
    {
        foreach(var s in Slots)
        {
            if(s==null || s.Active) continue;
            s.Cue.Reset(); s.Active=true; s.Smoking=false; s.Age=0; s.Origin=origin; s.Velocity=velocity;
            s.Material.SetColor(colorProperty,new Color(1,1,1,0));
            s.Root.transform.position=origin; s.Root.SetActive(true); s.Grenade.SetActive(true); s.Grenade.transform.position=origin;
            return;
        }
        throw new InvalidOperationException("Volley reservation violated");
    }
    internal static void Tick(float dt)
    {
        if(dt<=0 || !Ready) return;
        // One player-camera lookup per frame; no Camera.main or scene enumeration.
        Vector3 right=camera!=null ? camera.transform.right : Vector3.right;
        Vector3 up=camera!=null ? camera.transform.up : Vector3.up;
        foreach(var s in Slots)
        {
            if(s==null || !s.Active) continue;
            s.Age+=dt;
            if(!s.Smoking)
            {
                var position=s.Origin+s.Velocity*s.Age+Vector3.down*(.5f*9.81f*s.Age*s.Age);
                var previous=s.Grenade.transform.position; var step=position-previous;
                if(step.sqrMagnitude>.000001f && Physics.Raycast(previous,step.normalized,out RaycastHit hit,step.magnitude,~0,QueryTriggerInteraction.Ignore)) BeginSmoke(s,hit.point);
                else if(s.Age>=SmokeRules.MaxFlight) BeginSmoke(s,position);
                else s.Grenade.transform.position=position;
            }
            else
            {
                if(s.Age>=SmokeRules.CloudSeconds) { Retire(s); continue; }
                s.Material.SetColor(colorProperty,new Color(1,1,1,SmokeRules.Opacity(s.Age)));
                UpdateMesh(s,right,up);
            }
        }
    }
    private static void BeginSmoke(Slot s,Vector3 position)
    {
        if(s.Smoking) return;
        s.Smoking=true; s.Age=0; s.Grenade.SetActive(false); s.Root.transform.position=position+Vector3.up*2.2f;
        if(s.Cue.TryDeploy()) LaunchAudio.PlayBurst(s.BurstVoice,position);
        s.Material.SetColor(colorProperty,new Color(1,1,1,0));
        UpdateMesh(s,camera!=null ? camera.transform.right : Vector3.right,camera!=null ? camera.transform.up : Vector3.up);
    }
    private static void Retire(Slot s) { s.Active=false; s.Root.SetActive(false); }
    internal static void Clear() { foreach(var s in Slots) if(s!=null && s.Active) Retire(s); }
    internal static void Shutdown()
    {
        stopped=true; Clear(); camera=null;
        for(int i=0;i<Slots.Length;i++) { var s=Slots[i]; if(s==null) continue; LaunchAudio.Release(s.BurstVoice); UnityEngine.Object.Destroy(s.Root); UnityEngine.Object.Destroy(s.Material); UnityEngine.Object.Destroy(s.Mesh); Slots[i]=null; }
        if(texture!=null) UnityEngine.Object.Destroy(texture); if(grenadeMaterial!=null) UnityEngine.Object.Destroy(grenadeMaterial);
        texture=null; grenadeMaterial=null;
    }
}
