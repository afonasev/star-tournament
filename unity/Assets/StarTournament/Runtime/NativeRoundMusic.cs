using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class RoundMusicManifest { public RoundMusicTrack[] tracks; }
    [Serializable] public sealed class RoundMusicTrack { public string id, title; public RoundMusicPhrase[] phases; }
    [Serializable] public sealed class RoundMusicPhrase { public string resource; public float bpm, loopStart, loopEnd; }

    // Presentation only: no simulation RNG, mutations or replay fields. Two streaming voices,
    // even with four viewports. All phases of a track derive from its one continuous arrangement.
    public sealed class NativeRoundMusic : IDisposable
    {
        readonly AudioSource[] sources=new AudioSource[2];
        readonly System.Random random=new System.Random();
        readonly RoundMusicManifest manifest;
        AudioClip[] clips;
        RoundMusicTrack track;
        NativeMatchState match;
        int previous=-1, phase, active, incoming=-1, requested;
        float transition, transitionLength, finishElapsed;
        float gain, transitionBeats, finishSeconds, firstThreshold, finalThreshold;
        bool paused, finishing;
        readonly float[] weights=new float[2];
        public string TrackId=>track?.id;
        public int Phase=>phase;
        public bool Paused=>paused;
        public float GainSum=>weights[0]+weights[1];
        public bool HasOutput=>match!=null && GainSum>0 && (!finishing || finishElapsed<finishSeconds);
        public static int Intensity(double remaining, double duration, NativeMatchPhase state,float first,float last)
        {
            if(state==NativeMatchPhase.Overtime)return 2;
            double progress=duration<=0?0:1-Math.Max(0,remaining)/duration;
            return progress>=last?2:progress>=Math.Min(first,last)?1:0;
        }
        public NativeRoundMusic(Transform parent)
        {
            var data=Resources.Load<TextAsset>("Audio/Music/manifest");
            if(data==null){Debug.LogError("Missing round music manifest");return;}
            manifest=JsonUtility.FromJson<RoundMusicManifest>(data.text);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("round-music-"+i);go.transform.SetParent(parent,false);
                sources[i]=go.AddComponent<AudioSource>();sources[i].playOnAwake=false;
                sources[i].spatialBlend=0;sources[i].priority=64;sources[i].pitch=1;sources[i].volume=0;
            }
        }
        public void Bind(NativeMatchState next,ProvingProfile profile)
        {
            Stop();match=next;if(match==null||manifest?.tracks==null||manifest.tracks.Length==0)return;
            float Get(string key,float fallback)=>profile.Descriptor("audio.music."+key)==null?fallback:profile.Get("audio.music."+key);
            gain=Get("gain",.55f);transitionBeats=Get("transitionBeats",4);finishSeconds=Get("finishSeconds",2);
            firstThreshold=Get("developAt",.33f);finalThreshold=Get("climaxAt",.67f);
            // Draw from the other tracks without consuming the gameplay random stream.
            int count=manifest.tracks.Length,index=count==1?0:random.Next(previous<0?count:count-1);
            if(previous>=0&&count>1&&index>=previous)index++;
            previous=index;LoadTrack(index);
            Restore();
        }
        void LoadTrack(int index)
        {
            foreach(var source in sources){source.Stop();source.clip=null;}
            if(clips!=null)foreach(var clip in clips)if(clip!=null)Resources.UnloadAsset(clip);
            track=manifest.tracks[index];clips=new AudioClip[track.phases.Length];
            for(int i=0;i<clips.Length;i++)
            {
                clips[i]=Resources.Load<AudioClip>(track.phases[i].resource);
                if(clips[i]==null)Debug.LogError("Missing round music phrase: "+track.phases[i].resource);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnostic-only deterministic asset coverage. Ordinary matches always use the RNG above.
        public void ReviewTrack(int index)
        {
            if(match==null||manifest?.tracks==null||index<0||index>=manifest.tracks.Length)throw new ArgumentException("Invalid bound review track");
            previous=index;LoadTrack(index);Restore();
        }
#endif
        public void Restore()
        {
            if(match==null||clips==null)return;
            foreach(var source in sources)source.Stop();
            phase=requested=Intensity(match.RemainingSeconds,match.Configuration.DurationMinutes*60d,match.Phase,firstThreshold,finalThreshold);
            active=0;incoming=-1;transition=finishElapsed=0;finishing=false;paused=true;
            weights[0]=1;weights[1]=0;
            sources[0].clip=clips[phase];sources[0].time=0;
            // Bind/restore can happen in setup or a paused review. Tick starts playback explicitly.
        }
        public void Tick(bool running,float dt)
        {
            if(match==null||track==null||sources[active].clip==null)return;
            if(match.Phase==NativeMatchPhase.Finished)
            {
                if(!finishing){finishing=true;finishElapsed=0;SetPaused(false);}
                finishElapsed+=dt;
                if(finishElapsed>=finishSeconds){foreach(var source in sources)source.Stop();weights[0]=weights[1]=0;return;}
            }
            else
            {
                SetPaused(!running);if(paused)return;
                requested=Math.Max(requested,Intensity(match.RemainingSeconds,match.Configuration.DurationMinutes*60d,match.Phase,firstThreshold,finalThreshold));
            }
            if(incoming>=0)
            {
                transition+=dt;
                float t=Mathf.Clamp01(transition/transitionLength);
                weights[active]=Mathf.Cos(t*Mathf.PI*.5f);weights[incoming]=Mathf.Sin(t*Mathf.PI*.5f);
                if(t>=1){sources[active].Stop();weights[active]=0;active=incoming;incoming=-1;weights[active]=1;}
                return;
            }
            if(finishing)return;
            var phrase=track.phases[phase];float beat=60/phrase.bpm;
            float fade=Mathf.Min(transitionBeats*beat,(phrase.loopEnd-phrase.loopStart)*.25f);
            float time=sources[active].time;
            bool loop=time>=phrase.loopEnd-fade;
            // Only advance at a beat boundary; an overtime request cannot reset the intro.
            bool change=requested>phase && (time/beat)%1<Mathf.Max(dt/beat,.035f);
            if(!loop&&!change)return;
            int nextPhase=change?requested:phase;
            incoming=1-active;sources[incoming].clip=clips[nextPhase];
            sources[incoming].time=change?0:Mathf.Max(0,track.phases[nextPhase].loopStart-fade);
            sources[incoming].volume=0;sources[incoming].Play();
            transition=0;transitionLength=fade;weights[incoming]=0;phase=nextPhase;
        }
        void SetPaused(bool value)
        {
            if(paused==value)
            {
                if(!value&&!sources[active].isPlaying&&!finishing)sources[active].Play();
                return;
            }
            paused=value;
            foreach(var source in sources)
            {
                if(value)source.Pause();else source.UnPause();
            }
            if(!value&&!sources[active].isPlaying)sources[active].Play();
        }
        // The coordinator freezes finished tails as well as running music on focus/pause.
        public void SetSuspended(bool value){if(sources[0]!=null)SetPaused(value);}
        public float RequestedVolume(float userVolume)
        {
            if(paused||!HasOutput)return 0;
            float envelope=finishing?Mathf.Clamp01(1-finishElapsed/finishSeconds):1;
            return Mathf.Clamp01(userVolume)*gain*envelope;
        }
        public void UpdateVolume(float userVolume,float available)
        {
            if(sources[0]==null)return;
            float desired=Mathf.Min(RequestedVolume(userVolume),Mathf.Max(0,available));
            // Sum of individual peak bounds, including equal-power overlap, never exceeds budget.
            float scale=desired/Mathf.Max(1,GainSum);
            for(int i=0;i<2;i++)sources[i].volume=paused?0:weights[i]*scale;
        }
        public void Stop()
        {
            foreach(var source in sources)if(source!=null){source.Stop();source.clip=null;}
            if(clips!=null)foreach(var clip in clips)if(clip!=null)Resources.UnloadAsset(clip);
            clips=null;match=null;track=null;incoming=-1;weights[0]=weights[1]=0;finishing=false;paused=true;
        }
        public void Dispose(){Stop();foreach(var source in sources)if(source)UnityEngine.Object.Destroy(source.gameObject);}
    }
}
