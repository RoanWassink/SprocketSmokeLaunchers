using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Bindings;
namespace SprocketSmokeLaunchers;
internal static class LaunchAudio
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeSpan { internal IntPtr Begin; internal int Length; }
    internal sealed class Voice
    {
        internal GameObject Root=null!;
        internal AudioSource Source=null!;
        internal bool Paused;
    }
    private static AudioClip? clip, burstClip;
    private static bool failed, stopped, paused, burstAttempted;
    private static readonly List<Voice> Voices=new(SmokeRules.MaxBanks+SmokeRules.MaxClouds);
    private static AudioClip Upload(float[] samples,string titleText)
    {
        if(Marshal.SizeOf<NativeSpan>()!=Marshal.SizeOf<ManagedSpanWrapper>() ||
           Marshal.OffsetOf<ManagedSpanWrapper>(nameof(ManagedSpanWrapper.begin)).ToInt32()!=0 ||
           Marshal.OffsetOf<ManagedSpanWrapper>(nameof(ManagedSpanWrapper.length)).ToInt32()!=IntPtr.Size)
            throw new NotSupportedException("Unity audio span layout changed");
        var title=titleText.ToCharArray(); var created=AudioClip.Construct_Internal(); created.hideFlags=HideFlags.HideAndDontSave;
        GCHandle titlePin=default,samplesPin=default;
        try
        {
            titlePin=GCHandle.Alloc(title,GCHandleType.Pinned); samplesPin=GCHandle.Alloc(samples,GCHandleType.Pinned);
            var nativeTitle=new NativeSpan {Begin=titlePin.AddrOfPinnedObject(),Length=title.Length};
            ref var label=ref Unsafe.As<NativeSpan,ManagedSpanWrapper>(ref nativeTitle);
            AudioClip.CreateUserSound_Injected(created.m_CachedPtr,ref label,samples.Length,1,LaunchSound.Frequency,false);
            var nativeSamples=new NativeSpan {Begin=samplesPin.AddrOfPinnedObject(),Length=samples.Length};
            ref var data=ref Unsafe.As<NativeSpan,ManagedSpanWrapper>(ref nativeSamples);
            if(!AudioClip.SetData_Injected(created.m_CachedPtr,ref data,0) || created.samples!=samples.Length || created.channels!=1 || created.frequency!=LaunchSound.Frequency)
                throw new InvalidOperationException("Smoke cue native upload failed");
            return created;
        }
        catch { UnityEngine.Object.Destroy(created); throw; }
        finally { if(titlePin.IsAllocated) titlePin.Free(); if(samplesPin.IsAllocated) samplesPin.Free(); }
    }
    internal static void Warm()
    {
        if(stopped || failed) return;
        if(clip==null) try { clip=Upload(LaunchSound.CreateSamples(),"Smoke launch discharge"); Plugin.Instance.Log.LogInfo("[Smoke AUDIO] Stronger authored three-pulse launch ready; saved volume/mute retained."); }
        catch(Exception ex) { failed=true; Runtime.Warn("Optional launch audio disabled",ex); }
        if(!burstAttempted && !failed)
        {
            burstAttempted=true;
            try { burstClip=Upload(DeploymentSound.CreateSamples(),"Smoke grenade activation"); Plugin.Instance.Log.LogInfo("[Smoke AUDIO] Authored grenade burst ready: once per grenade at smoke deployment position."); }
            catch(Exception ex) { Runtime.Warn("Optional burst audio disabled; launch retained",ex); }
        }
    }
    internal static Voice? Create(float minDistance=6,float maxDistance=65)
    {
        if(stopped || failed || Voices.Count>=SmokeRules.MaxBanks+SmokeRules.MaxClouds) return null;
        Voice? voice=null;
        try
        {
            voice=new Voice {Root=new GameObject("Smoke launch sound")};
            voice.Root.hideFlags=HideFlags.HideAndDontSave; UnityEngine.Object.DontDestroyOnLoad(voice.Root);
            voice.Source=voice.Root.AddComponent<AudioSource>();
            voice.Source.playOnAwake=false; voice.Source.loop=false; voice.Source.spatialBlend=1;
            voice.Source.minDistance=minDistance; voice.Source.maxDistance=maxDistance; voice.Source.dopplerLevel=0;
            voice.Source.pitch=1; Voices.Add(voice); return voice;
        }
        catch(Exception ex) { if(voice?.Root!=null) UnityEngine.Object.Destroy(voice.Root); failed=true; Runtime.Warn("Optional launch voice disabled",ex); return null; }
    }
    internal static void Play(Voice? voice,Vector3 position) => PlayClip(voice,position,clip);
    internal static void PlayBurst(Voice? voice,Vector3 position) => PlayClip(voice,position,burstClip);
    private static void PlayClip(Voice? voice,Vector3 position,AudioClip? selected)
    {
        if(failed || stopped || voice?.Source==null || selected==null || Plugin.SoundVolume.Value<=0) return;
        try
        {
            voice.Root.transform.position=position;
            voice.Source.clip=selected; voice.Source.volume=Plugin.SoundVolume.Value;
            voice.Paused=false; voice.Source.Play();
        }
        catch(Exception ex) { failed=true; Runtime.Warn("Optional launch playback disabled",ex); }
    }
    internal static void SetPaused(bool value)
    {
        if(paused==value) return; paused=value;
        try
        {
            foreach(var voice in Voices)
            {
                if(voice.Source==null) continue;
                if(value && voice.Source.isPlaying) { voice.Source.Pause(); voice.Paused=true; }
                else if(!value && voice.Paused) { voice.Source.UnPause(); voice.Paused=false; }
            }
        }
        catch(Exception ex) { Runtime.Warn("Optional sound pause",ex); }
    }
    internal static void Clear()
    {
        try { foreach(var voice in Voices) if(voice.Source!=null) { voice.Source.Stop(); voice.Paused=false; } }
        catch(Exception ex) { Runtime.Warn("Optional sound clear",ex); }
    }
    internal static void Release(Voice? voice)
    {
        if(voice==null) return; Voices.Remove(voice); if(voice.Root!=null) UnityEngine.Object.Destroy(voice.Root);
    }
    internal static void Shutdown()
    {
        stopped=true;
        foreach(var voice in Voices) if(voice.Root!=null) UnityEngine.Object.Destroy(voice.Root);
        Voices.Clear(); if(clip!=null) UnityEngine.Object.Destroy(clip); if(burstClip!=null) UnityEngine.Object.Destroy(burstClip); clip=null; burstClip=null;
    }
}

