using System;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class MenuMusicManifest
    {
        public string id,title,resource;
        public float bpm,loopStart,loopEnd,loopFadeSeconds;
    }

    // One persistent menu transport. Navigation never resets its motif or playback position.
    public sealed class NativeMenuMusic : IDisposable
    {
        readonly AudioSource[] sources=new AudioSource[2];
        readonly float[] weights={1,0};
        readonly MenuMusicManifest manifest;
        readonly AudioClip clip;
        int active,incoming=-1;
        float elapsed,gain;
        bool paused=true;
        public bool Paused=>paused;
        public float GainSum=>weights[0]+weights[1];
        public void Configure(ProvingProfile profile){gain=profile.Get("audio.music.gain");}
        public NativeMenuMusic(Transform parent,ProvingProfile profile)
        {
            var text=Resources.Load<TextAsset>("Audio/Music/menu-manifest");
            if(text==null){Debug.LogError("Missing menu music manifest");return;}
            manifest=JsonUtility.FromJson<MenuMusicManifest>(text.text);
            clip=Resources.Load<AudioClip>(manifest.resource);
            if(clip==null||manifest.loopStart<manifest.loopFadeSeconds||manifest.loopEnd>=clip.length||manifest.loopFadeSeconds<=0)
            {Debug.LogError("Invalid menu music loop");return;}
            gain=profile.Get("audio.music.gain");
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("menu-music-"+i);go.transform.SetParent(parent,false);
                sources[i]=go.AddComponent<AudioSource>();sources[i].playOnAwake=false;
                sources[i].spatialBlend=0;sources[i].priority=64;sources[i].pitch=1;sources[i].volume=0;sources[i].clip=clip;
            }
        }
        public void SetSuspended(bool value)
        {
            if(sources[0]==null)return;
            if(value==paused)return;
            paused=value;
            foreach(var source in sources){if(value)source.Pause();else source.UnPause();}
            if(!value&&!sources[active].isPlaying)sources[active].Play();
        }
        public void Tick(bool running,float dt)
        {
            if(sources[0]==null)return;
            SetSuspended(!running);if(paused)return;
            if(incoming>=0)
            {
                elapsed+=dt;float t=Mathf.Clamp01(elapsed/manifest.loopFadeSeconds);
                weights[active]=Mathf.Cos(t*Mathf.PI*.5f);weights[incoming]=Mathf.Sin(t*Mathf.PI*.5f);
                if(t>=1){sources[active].Stop();weights[active]=0;active=incoming;incoming=-1;weights[active]=1;}
            }
            else if(sources[active].time>=manifest.loopEnd-manifest.loopFadeSeconds)
            {
                incoming=1-active;sources[incoming].time=manifest.loopStart-manifest.loopFadeSeconds;
                sources[incoming].volume=0;sources[incoming].Play();elapsed=0;weights[incoming]=0;
            }
        }
        public float RequestedVolume(float user)=>paused||sources[0]==null?0:Mathf.Clamp01(user)*gain;
        public void UpdateVolume(float user,float available)
        {
            if(sources[0]==null)return;
            float budget=Mathf.Min(RequestedVolume(user),Mathf.Max(0,available));
            for(int i=0;i<2;i++)sources[i].volume=weights[i]*budget/Mathf.Max(1,GainSum);
        }
        public void Dispose()
        {
            foreach(var source in sources)if(source!=null){source.Stop();source.clip=null;UnityEngine.Object.Destroy(source.gameObject);}
            // Resources owns the single shared menu asset. Explicitly unloading it here could
            // invalidate another transport/review using that same cached AudioClip.
        }
    }
}
