using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    // User volume belongs to the installation. Lab descriptors supply only initial values and mix limits.
    public static class NativeAudioPreferences
    {
        const string MusicKey="StarTournament.Audio.MusicPercent";
        const string EffectsKey="StarTournament.Audio.EffectsPercent";
        public static int Music(ProvingProfile profile)=>Read(MusicKey,profile.Get("audio.musicDefaultPercent"));
        public static int Effects(ProvingProfile profile)=>Read(EffectsKey,profile.Get("audio.effectsDefaultPercent"));
        static int Read(string key,float fallback)=>Mathf.Clamp(PlayerPrefs.GetInt(key,Mathf.RoundToInt(fallback)),0,100);
        public static void SetMusic(int value){PlayerPrefs.SetInt(MusicKey,Mathf.Clamp(value,0,100));PlayerPrefs.Save();}
        public static void SetEffects(int value){PlayerPrefs.SetInt(EffectsKey,Mathf.Clamp(value,0,100));PlayerPrefs.Save();}
    }

    // One shared output for the arena, regardless of the number of local viewports.
    public sealed class NativeGameAudio : IDisposable
    {
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,AudioClip[]> movementClips=new Dictionary<string,AudioClip[]>();
        // Content cardinality, not a tunable mix value: all five approved recordings ship together.
        const int MovementVariantCount=5;
        readonly int[,] lastMovement=new int[NativeMatchRoster.MaximumParticipants,3];
        readonly System.Random movementRandom=new System.Random();
        AudioSource[] voices;
        float[] voiceGain;
        readonly AudioSource[] beams;
        readonly float[] beamGain;
        readonly AudioSource uiSource;
        readonly AudioSource damageBonusSource;
        float damageBonusGain;
        float uiGain;
        readonly Transform root;
        readonly ProvingProfile profile;
        readonly NativeRoundMusic music;
        public NativeRoundMusic Music=>music;
        NativeCombatSession session;
        NativeMatchComposition composition;
        ParticipantState[] lastPose;
        float[] stepDistance;
        double[] lastHit;
        int nextVoice;
        bool muted;

        public NativeGameAudio(Transform parent,ProvingProfile profile,bool mute)
        {
            this.profile=profile;muted=mute;
            var holder=new GameObject("native-game-audio");holder.transform.SetParent(parent,false);root=holder.transform;
            foreach(var name in new[]{"menu_move","menu_confirm","menu_back","rifle","shotgun","rocket","cutter-start","cutter-loop","hit","death","pickup","damage-bonus-spawn","damage-bonus-pickup"})
            {
                var clip=Resources.Load<AudioClip>("Audio/"+name);
                if(clip==null)Debug.LogError("Missing native audio clip: "+name);
                clips[name]=clip;
            }
            foreach(var name in new[]{"step","jump","land"})
            {
                var bank=new AudioClip[MovementVariantCount];
                for(int i=0;i<bank.Length;i++)
                {
                    bank[i]=Resources.Load<AudioClip>("Audio/Movement/"+name+"-"+i);
                    if(bank[i]==null)Debug.LogError("Missing movement audio clip: "+name+"-"+i);
                }
                movementClips[name]=bank;
            }
            BuildVoicePool();
            music=new NativeRoundMusic(root);
            uiSource=Source("menu-ui");damageBonusSource=Source("damage-bonus-alert");
            beams=new AudioSource[NativeMatchRoster.MaximumParticipants];
            beamGain=new float[beams.Length];
            for(int i=0;i<beams.Length;i++){beams[i]=Source("cutter-"+i);beams[i].clip=clips["cutter-loop"];beams[i].loop=true;}
        }
        AudioSource Source(string name)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;return source;
        }
        void BuildVoicePool()
        {
            int count=Mathf.RoundToInt(profile.Get("audio.maxVoices"));
            if(voices!=null&&voices.Length==count)return;
            if(voices!=null)foreach(var voice in voices)if(voice)UnityEngine.Object.Destroy(voice.gameObject);
            voices=new AudioSource[count];voiceGain=new float[count];nextVoice=0;
            for(int i=0;i<count;i++)voices[i]=Source("voice-"+i);
        }
        public void Bind(NativeCombatSession next,NativeMatchComposition roster,ProvingProfile musicTuning=null)
        {
            Unbind();BuildVoicePool();session=next;composition=roster;
            music.Bind(session?.Match,musicTuning??profile);
            if(session==null)return;
            lastPose=new ParticipantState[session.ParticipantCount];stepDistance=new float[session.ParticipantCount];lastHit=new double[session.ParticipantCount];
            for(int i=0;i<lastPose.Length;i++){lastPose[i]=session.Pose(i);lastHit[i]=double.NegativeInfinity;}
            session.DamageBonusAppeared+=DamageBonusSpawn;session.ShotResolved+=Shot;session.Damaged+=Damage;session.Died+=Death;session.PickupCollected+=Pickup;session.StateRestored+=ResetMotion;
        }
        void Unbind()
        {
            if(session!=null){session.DamageBonusAppeared-=DamageBonusSpawn;session.ShotResolved-=Shot;session.Damaged-=Damage;session.Died-=Death;session.PickupCollected-=Pickup;session.StateRestored-=ResetMotion;}
            session=null;composition=null;music.Stop();
            StopEffects();
        }
        void ResetMotion()
        {
            if(session==null)return;
            music.Restore();
            damageBonusSource.Stop();
            for(int i=0;i<lastPose.Length;i++){lastPose[i]=session.Pose(i);stepDistance[i]=0;}
            for(int i=0;i<beams.Length;i++){beams[i].Stop();beamGain[i]=0;}
        }
        public void SetDiagnosticMute(bool value){muted=value;if(value)StopEffects();UpdateVolume();}
        public void TickMusic(bool running,float dt){music.Tick(running,dt);UpdateVolume();}
        public void StopEffects(){damageBonusSource.Stop();foreach(var voice in voices)voice.Stop();for(int i=0;i<beams.Length;i++){beams[i].Stop();beamGain[i]=0;}}
        public void UpdateVolume()
        {
            float volume=EffectsVolume;
            if(volume==0){StopEffects();uiSource.Stop();music.UpdateVolume(muted?0:NativeAudioPreferences.Music(profile)/100f,1);return;}
            float sum=0;
            if(uiSource.isPlaying)sum+=uiGain;
            if(damageBonusSource.isPlaying)sum+=damageBonusGain;
            for(int i=0;i<voices.Length;i++)if(voices[i].isPlaying)sum+=voiceGain[i];
            for(int i=0;i<beams.Length;i++)if(beams[i].isPlaying)sum+=beamGain[i];
            // Preserve the previous SFX-only mix, then normalize its budget together with music.
            // Foreground effects stay stronger under load without completely erasing the theme.
            float scale=volume*Mathf.Min(1,1/Mathf.Max(1,sum));
            float musicVolume=muted?0:NativeAudioPreferences.Music(profile)/100f;
            float musicBudget=music.RequestedVolume(musicVolume);
            float attenuation=musicBudget>0?1/Mathf.Max(1,sum*scale+musicBudget):1;
            scale*=attenuation;
            for(int i=0;i<voices.Length;i++)voices[i].volume=voiceGain[i]*scale;
            for(int i=0;i<beams.Length;i++)beams[i].volume=beamGain[i]*scale;
            uiSource.volume=uiGain*scale;damageBonusSource.volume=damageBonusGain*scale;
            music.UpdateVolume(musicVolume,musicBudget*attenuation);
        }
        public void MenuMove()=>PlayMenu("menu_move",.40f);
        public void MenuConfirm()=>PlayMenu("menu_confirm",.65f);
        public void MenuBack()=>PlayMenu("menu_back",.55f);
        float EffectsVolume=>muted?0:NativeAudioPreferences.Effects(profile)/100f;
        void PlayMenu(string name,float gain)
        {
            if(!clips.TryGetValue(name,out var clip)||clip==null||EffectsVolume<=0)return;
            uiSource.Stop();uiSource.clip=clip;uiGain=gain;uiSource.Play();UpdateVolume();
        }
        void Play(string name,float gain,int participant=-1)
        {
            if(clips.TryGetValue(name,out var clip))PlayClip(clip,gain,participant,1);
        }
        void PlayClip(AudioClip clip,float gain,int participant,float pitch)
        {
            if(clip==null||EffectsVolume<=0)return;
            if(participant>=0)gain*=ParticipantGain(participant);
            if(gain<=.005f)return;
            float level=Mathf.Clamp01(gain*EffectsVolume);
            AudioSource source=null;
            for(int i=0;i<voices.Length;i++)
            {
                var candidate=voices[(nextVoice+i)%voices.Length];
                if(!candidate.isPlaying){source=candidate;nextVoice=(nextVoice+i+1)%voices.Length;break;}
            }
            if(source==null)
            {
                int weakest=0;
                for(int i=1;i<voices.Length;i++)if(voiceGain[i]<voiceGain[weakest])weakest=i;
                source=voices[weakest];
                // Keep a louder event audible when 1–4 viewports all produce events at once.
                if(voiceGain[weakest]>gain)return;
            }
            source.Stop();source.clip=clip;source.pitch=pitch;voiceGain[Array.IndexOf(voices,source)]=gain;source.volume=level;source.Play();UpdateVolume();
        }
        void PlayMovement(string name,float gain,int participant)
        {
            var bank=movementClips[name];int kind=name=="step"?0:name=="jump"?1:2;
            int prior=lastMovement[participant,kind]-1;
            int selected=movementRandom.Next(prior<0?bank.Length:bank.Length-1);
            if(prior>=0&&selected>=prior)selected++;
            lastMovement[participant,kind]=selected+1;
            float pitch=1+((float)movementRandom.NextDouble()*2-1)*profile.Get("audio.movementPitchVariation");
            float gainScale=1+((float)movementRandom.NextDouble()*2-1)*profile.Get("audio.movementGainVariation");
            PlayClip(bank[selected],gain*gainScale,participant,pitch);
        }
        float ParticipantGain(int participant)
        {
            if(session==null||composition==null)return 0;
            float distance=float.MaxValue;
            for(int seat=0;seat<composition.LocalCount;seat++)
            {
                int local=composition.ParticipantAt(seat);
                if(local==participant)return profile.Get("audio.localGain");
                distance=Mathf.Min(distance,Vector3.Distance(session.Pose(local).Position,session.Pose(participant).Position));
            }
            float range=profile.Get("audio.maxDistanceMeters");
            return profile.Get("audio.remoteGain")*Mathf.Clamp01(1-distance/range);
        }
        void Shot(ShotNotice shot)
        {
            switch(shot.Weapon)
            {
                case WeaponId.Rifle:Play("rifle",1,shot.Shooter);break;
                case WeaponId.Shotgun:Play("shotgun",1,shot.Shooter);break;
                case WeaponId.RocketLauncher:Play("rocket",1,shot.Shooter);break;
                case WeaponId.Cutter:Play("cutter-start",.7f,shot.Shooter);break;
            }
        }
        void Damage(DamageNotice damage)
        {
            if(session==null||session.Life(damage.Participant).Dead)return;
            if(damage.Time-lastHit[damage.Participant]<profile.Get("audio.hitIntervalSeconds"))return;
            lastHit[damage.Participant]=damage.Time;Play("hit",.85f,damage.Participant);
        }
        void Death(DeathNotice death){Play("death",1,death.Seat);beams[death.Seat].Stop();beamGain[death.Seat]=0;}
        void DamageBonusSpawn()=>PlayDamageBonus("damage-bonus-spawn",profile.Get("audio.damageBonusSpawnGain"));
        void PlayDamageBonus(string name,float gain)
        {
            if(!clips.TryGetValue(name,out var clip)||clip==null||EffectsVolume<=0||gain<=0)return;
            damageBonusSource.Stop();damageBonusSource.clip=clip;damageBonusGain=gain;damageBonusSource.Play();UpdateVolume();
        }
        void Pickup(int participant,string id,NativeBotPickupKind kind)
        {
            if(kind==NativeBotPickupKind.Damage)PlayDamageBonus("damage-bonus-pickup",profile.Get("audio.damageBonusPickupGain"));
            else Play("pickup",.9f,participant);
        }
        public void Tick(bool running)
        {
            if(session==null)return;
            for(int i=0;i<session.ParticipantCount;i++)
            {
                var now=session.Pose(i);var old=lastPose[i];lastPose[i]=now;
                if(!running||session.Life(i).Dead){stepDistance[i]=0;beams[i].Stop();beamGain[i]=0;continue;}
                if(old.Grounded&&!now.Grounded&&now.Velocity.y>0.1f)PlayMovement("jump",profile.Get("audio.jumpGain"),i);
                if(!old.Grounded&&now.Grounded&&old.Velocity.y<-.1f)PlayMovement("land",profile.Get("audio.landGain"),i);
                if(now.Grounded)
                {
                    var delta=now.Position-old.Position;delta.y=0;
                    if(delta.sqrMagnitude<.25f)stepDistance[i]+=delta.magnitude;else stepDistance[i]=0;
                    if(stepDistance[i]>=profile.Get("audio.footstepDistanceMeters"))
                    {stepDistance[i]=0;PlayMovement("step",profile.Get("audio.footstepGain"),i);}
                }
                else stepDistance[i]=0;
                var beam=beams[i];bool active=session.Beam(i).Active&&EffectsVolume>0;
                float target=active?ParticipantGain(i)*.55f:0;
                // Short ramps remove click edges at fire/release; these are audio envelope invariants.
                beamGain[i]=Mathf.MoveTowards(beamGain[i],target,Time.fixedDeltaTime*3f);
                if(active&&!beam.isPlaying&&beam.clip!=null)beam.Play();
                if(!active&&beamGain[i]<=0&&beam.isPlaying)beam.Stop();
            }
            UpdateVolume();
        }
        public void Dispose(){Unbind();music.Dispose();if(root)UnityEngine.Object.Destroy(root.gameObject);}
    }
}
