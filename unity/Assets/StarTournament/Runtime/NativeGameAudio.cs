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
        readonly Dictionary<string,AudioClip[]> banks=new Dictionary<string,AudioClip[]>();
        readonly System.Random soundRandom=new System.Random();
        readonly Dictionary<string,int> lastBank=new Dictionary<string,int>();
        readonly Dictionary<string,AudioClip[]> movementClips=new Dictionary<string,AudioClip[]>();
        // Content cardinality, not a tunable mix value: all five approved recordings ship together.
        const int MovementVariantCount=5;
        readonly int[,] lastMovement=new int[NativeMatchRoster.MaximumParticipants,3];
        readonly System.Random movementRandom=new System.Random();
        AudioSource[] voices;
        float[] voiceGain,voiceBaseGain;
        Vector3[] voicePosition;
        int[] voiceParticipant;
        bool[] voiceFeedback,voiceCombat;
        float combatMusicGain=1;
        AudioSource[] rocketSources;
        uint[] rocketIds;
        float[] rocketGain;
        readonly float[] beamEnvelope=new float[NativeMatchRoster.MaximumParticipants];
        readonly AudioSpatialMix[] beamMix=new AudioSpatialMix[NativeMatchRoster.MaximumParticipants];
        readonly AudioSource[] beams;
        readonly float[] beamGain;
        readonly AudioSource uiSource;
        readonly AudioSource damageBonusSource;
        readonly AudioSource botReactionSource;
        readonly Dictionary<string,AudioClip> botReactionClips=new Dictionary<string,AudioClip>(StringComparer.Ordinal);
        const float BotReactionGain=1;
        int botReactionSpeaker=-1;
        public int BotReactionPlayCount { get; private set; }
        readonly AudioSource killSeriesSource;
        const float KillSeriesGain=.85f;
        float damageBonusGain;
        float uiGain;
        readonly Transform root;
        readonly ProvingProfile profile;
        readonly NativeRoundMusic music;
        public NativeRoundMusic Music=>music;
        readonly NativeMusicCoordinator musicScene;
        public NativeMusicCoordinator MusicScene=>musicScene;
        ProvingProfile musicProfile;
        NativeCombatSession session;
        NativeMatchComposition composition;
        ParticipantState[] lastPose;
        CombatLifeState[] lastWeaponState;
        bool[] weaponReadyPending;
        float[] stepDistance;
        double[] lastHit;
        int nextVoice;
        bool muted;

        public NativeGameAudio(Transform parent,ProvingProfile profile,bool mute)
        {
            this.profile=profile;muted=mute;
            var holder=new GameObject("native-game-audio");holder.transform.SetParent(parent,false);root=holder.transform;
            foreach(var name in new[]{"menu_move","menu_confirm","menu_back","rifle","shotgun","rocket","cutter-start","cutter-loop","hit","death","pickup","shield-hit","explosion","rocket-flight","weapon-pickup","weapon-ready","shield-pickup","damage-pickup","speed-pickup","heal-pickup","jump","land","damage-bonus-spawn","damage-bonus-pickup","kill-triple","kill-quadruple"})
            {
                var clip=Resources.Load<AudioClip>("Audio/"+name);
                if(clip==null)Debug.LogError("Missing native audio clip: "+name);
                clips[name]=clip;
            }
            foreach(var entry in new[]{("rifle",4),("shotgun",2),("body-hit",2),("death",3)})
            {
                var bank=new AudioClip[entry.Item2];
                for(int i=0;i<bank.Length;i++){bank[i]=Resources.Load<AudioClip>("Audio/"+entry.Item1+"-"+i);if(bank[i]==null)Debug.LogError("Missing approved audio bank: "+entry.Item1+"-"+i);}
                banks[entry.Item1]=bank;
            }
            foreach(var name in new[]{"step"})
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
            musicScene=new NativeMusicCoordinator(root,profile);music=musicScene.Round;musicProfile=profile;
            uiSource=Source("menu-ui");damageBonusSource=Source("damage-bonus-alert");
            botReactionSource=Source("bot-reaction");
            foreach(var entry in NativeBotBanterVoice.Clips)
            {
                var clip=Resources.Load<AudioClip>(entry.Value);
                if(clip==null)Debug.LogError("Missing bot reaction voice clip: "+entry.Value);
                else botReactionClips.Add(entry.Key,clip);
            }
            killSeriesSource=Source("kill-series-voice");
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
            if(rocketSources!=null)foreach(var rocket in rocketSources)if(rocket)UnityEngine.Object.Destroy(rocket.gameObject);
            voices=new AudioSource[count];voiceGain=new float[count];voiceBaseGain=new float[count];voicePosition=new Vector3[count];voiceParticipant=new int[count];voiceFeedback=new bool[count];voiceCombat=new bool[count];nextVoice=0;
            rocketSources=new AudioSource[count];rocketIds=new uint[count];rocketGain=new float[count];
            for(int i=0;i<count;i++){rocketSources[i]=Source("rocket-flight-"+i);rocketSources[i].clip=clips["rocket-flight"];rocketSources[i].loop=true;}
            for(int i=0;i<count;i++)voices[i]=Source("voice-"+i);
        }
        public void Bind(NativeCombatSession next,NativeMatchComposition roster,ProvingProfile musicTuning=null)
        {
            Unbind();BuildVoicePool();session=next;composition=roster;
            musicProfile=musicTuning??profile;
            if(session==null)return;
            lastPose=new ParticipantState[session.ParticipantCount];lastWeaponState=new CombatLifeState[session.ParticipantCount];weaponReadyPending=new bool[session.ParticipantCount];stepDistance=new float[session.ParticipantCount];lastHit=new double[session.ParticipantCount];
            for(int i=0;i<lastPose.Length;i++){lastPose[i]=session.Pose(i);lastWeaponState[i]=session.Life(i);lastHit[i]=double.NegativeInfinity;}
            session.DamageBonusAppeared+=DamageBonusSpawn;session.ShotResolved+=Shot;session.Damaged+=Damage;session.Died+=Death;session.PickupCollected+=Pickup;session.StateRestored+=ResetMotion;session.RocketExploded+=Explosion;session.WeaponStateResolved+=WeaponState;
        }
        void Unbind()
        {
            if(session!=null){session.DamageBonusAppeared-=DamageBonusSpawn;session.ShotResolved-=Shot;session.Damaged-=Damage;session.Died-=Death;session.PickupCollected-=Pickup;session.StateRestored-=ResetMotion;session.RocketExploded-=Explosion;session.WeaponStateResolved-=WeaponState;}
            session=null;composition=null;
            StopEffects();
        }
        void ResetMotion()
        {
            if(session==null)return;
            musicScene.Restore(session.Match);
            StopEffects();
            for(int i=0;i<lastPose.Length;i++){lastPose[i]=session.Pose(i);lastWeaponState[i]=session.Life(i);weaponReadyPending[i]=false;stepDistance[i]=0;lastHit[i]=double.NegativeInfinity;}
            for(int i=0;i<beams.Length;i++){beams[i].Stop();beamGain[i]=0;}
        }
        public void SetDiagnosticMute(bool value){muted=value;if(value)StopEffects();UpdateVolume();}
        public void TickMusic(bool inMenu,bool suspended,float dt)
        {
            musicScene.Tick(inMenu,session?.Match,musicProfile,suspended,dt);
            if(!suspended)
            {
                float floor=ProvingProfile.CombatMusicDuckGain(musicProfile);
                combatMusicGain=Mathf.MoveTowards(Mathf.Max(floor,combatMusicGain),1,Mathf.Max(0,dt)*(1-floor)/ProvingProfile.CombatMusicDuckReleaseSeconds(musicProfile));
            }
            UpdateVolume();
        }
        public void SuspendMusic(){musicScene.SetSuspended(true);UpdateVolume();}
        public void StopEffects(){StopBotReaction();combatMusicGain=1;damageBonusSource.Stop();killSeriesSource.Stop();foreach(var voice in voices)voice.Stop();for(int i=0;i<beams.Length;i++){beams[i].Stop();beamGain[i]=0;beamEnvelope[i]=0;}for(int i=0;i<rocketSources.Length;i++){rocketSources[i].Stop();rocketIds[i]=0;rocketGain[i]=0;}}
        public void StopBotReaction(){botReactionSource.Stop();botReactionSpeaker=-1;}
        public bool PlayBotReaction(string text,int speaker,bool speakerMayBeDead=false)
        {
            if(!NativeBotReactionPreferences.VoiceEnabled||text==null||!botReactionClips.TryGetValue(text,out var clip)||EffectsVolume<=0||
                session==null||speaker<0||speaker>=session.ParticipantCount||!speakerMayBeDead&&session.Life(speaker).Dead)return false;
            StopBotReaction();botReactionSpeaker=speakerMayBeDead?-1:speaker;botReactionSource.clip=clip;
            botReactionSource.Play();BotReactionPlayCount++;UpdateVolume();return true;
        }
        public void UpdateVolume()
        {
            if(!NativeBotReactionPreferences.VoiceEnabled)StopBotReaction();
            float volume=EffectsVolume;
            if(volume==0){StopEffects();uiSource.Stop();musicScene.UpdateVolume(muted?0:NativeAudioPreferences.Music(profile)/100f,1);return;}
            float sum=0,combatStrength=0;
            for(int i=0;i<voices.Length;i++)if(voices[i].isPlaying&&voiceParticipant[i]>=0)
            {var mix=Spatial(voicePosition[i],voiceParticipant[i]);voiceGain[i]=voiceBaseGain[i]*mix.Gain;voices[i].panStereo=mix.Pan;}
            if(uiSource.isPlaying)sum+=uiGain;
            if(killSeriesSource.isPlaying)sum+=KillSeriesGain;
            if(damageBonusSource.isPlaying)sum+=damageBonusGain;
            if(botReactionSource.isPlaying)sum+=BotReactionGain;
            for(int i=0;i<voices.Length;i++)if(voices[i].isPlaying)
            {sum+=voiceGain[i];if(voiceCombat[i])combatStrength=Mathf.Max(combatStrength,voiceGain[i]*volume);}
            for(int i=0;i<beams.Length;i++)if(beams[i].isPlaying)
            {sum+=beamGain[i];combatStrength=Mathf.Max(combatStrength,beamGain[i]*volume);}
            for(int i=0;i<rocketSources.Length;i++)if(rocketSources[i].isPlaying)sum+=rocketGain[i];
            // Preserve the previous SFX-only mix, then normalize its budget together with music.
            // Foreground effects stay stronger under load without completely erasing the theme.
            float scale=volume*Mathf.Min(1,1/Mathf.Max(1,sum));
            float musicVolume=muted?0:NativeAudioPreferences.Music(profile)/100f;
            // Audible foreground weapons lower music, weighted by listener distance and user SFX level.
            // Release uses the music clock; volume refreshes never invent elapsed time.
            float duckFloor=ProvingProfile.CombatMusicDuckGain(musicProfile);
            combatMusicGain=Mathf.Clamp(combatMusicGain,duckFloor,1);
            combatMusicGain=Mathf.Min(combatMusicGain,Mathf.Lerp(1,duckFloor,Mathf.Clamp01(combatStrength/ProvingProfile.CombatMusicDuckReferenceGain(musicProfile))));
            float musicBudget=musicScene.RequestedVolume(musicVolume)*combatMusicGain;
            float attenuation=musicBudget>0?1/Mathf.Max(1,sum*scale+musicBudget):1;
            scale*=attenuation;
            for(int i=0;i<voices.Length;i++)voices[i].volume=voiceGain[i]*scale;
            for(int i=0;i<beams.Length;i++)beams[i].volume=beamGain[i]*scale;
            for(int i=0;i<rocketSources.Length;i++)rocketSources[i].volume=rocketGain[i]*scale;
            uiSource.volume=uiGain*scale;damageBonusSource.volume=damageBonusGain*scale;
            botReactionSource.volume=BotReactionGain*scale;
            killSeriesSource.volume=KillSeriesGain*scale;
            musicScene.UpdateVolume(musicVolume,musicBudget*attenuation);
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
            PlayAt(clip,gain,participant,session!=null&&participant>=0?session.Pose(participant).Position:Vector3.zero,pitch);
        }
        void PlayAt(AudioClip clip,float baseGain,int participant,Vector3 point,float pitch=1,bool feedback=false,bool combat=false)
        {
            if(clip==null||EffectsVolume<=0)return;
            var mix=participant>=0?Spatial(point,participant):new AudioSpatialMix(1,0);
            float gain=baseGain*mix.Gain;
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
                int weakest=-1;
                // Short death/weapon-ready feedback must finish even when subsequent shots fill the pool.
                for(int i=0;i<voices.Length;i++)
                    if(!voiceFeedback[i]&&(weakest<0||voiceGain[i]<voiceGain[weakest]))weakest=i;
                if(weakest<0)return;
                source=voices[weakest];
                // Keep a louder event audible when 1–4 viewports all produce events at once.
                if(!feedback&&voiceGain[weakest]>gain)return;
            }
            int slot=Array.IndexOf(voices,source);
            source.Stop();source.clip=clip;source.pitch=pitch;source.panStereo=mix.Pan;
            voiceFeedback[slot]=feedback;voiceCombat[slot]=combat;voiceGain[slot]=gain;voiceBaseGain[slot]=baseGain;voiceParticipant[slot]=participant;voicePosition[slot]=point;source.volume=level;source.Play();UpdateVolume();
        }
        void PlayMovement(string name,float gain,int participant)
        {
            if(name!="step"){Play(name,gain,participant);return;}
            var bank=movementClips[name];int kind=0;
            int prior=lastMovement[participant,kind]-1;
            int selected=movementRandom.Next(prior<0?bank.Length:bank.Length-1);
            if(prior>=0&&selected>=prior)selected++;
            lastMovement[participant,kind]=selected+1;
            float pitch=1+((float)movementRandom.NextDouble()*2-1)*profile.Get("audio.movementPitchVariation");
            float gainScale=1+((float)movementRandom.NextDouble()*2-1)*profile.Get("audio.movementGainVariation");
            PlayClip(bank[selected],gain*gainScale,participant,pitch);
        }
        AudioSpatialMix Spatial(Vector3 point,int owner,Vector3? end=null)
        {
            if(session==null||composition==null)return new AudioSpatialMix(0,0);
            var best=new AudioSpatialMix(0,0);
            for(int seat=0;seat<composition.LocalCount;seat++)
            {
                int local=composition.ParticipantAt(seat);var listener=session.Pose(local);
                var source=end.HasValue?NativeAudioSpatial.ClosestPoint(point,end.Value,listener.Position):point;
                var mix=NativeAudioSpatial.ForListener(source,listener.Position,listener.Yaw,profile.Get("audio.maxDistanceMeters"),profile.Get(local==owner?"audio.localGain":"audio.remoteGain"));
                // One physical output: strongest listener wins; ties use stable seat order.
                if(mix.Gain>best.Gain)best=mix;
            }
            return best;
        }
        AudioClip Bank(string name)
        {
            var bank=banks[name];int prior=lastBank.TryGetValue(name,out int last)?last:-1;
            // Death weights are unspecified: independent equal probability for all three.
            int selected=soundRandom.Next(name=="death"||prior<0?bank.Length:bank.Length-1);
            if(name!="death"&&prior>=0&&selected>=prior)selected++;
            lastBank[name]=selected;return bank[selected];
        }
        void Shot(ShotNotice shot)
        {
            float gain=ProvingProfile.WeaponAudioGain(profile);
            switch(shot.Weapon)
            {
                case WeaponId.Rifle:PlayAt(Bank("rifle"),gain,shot.Shooter,shot.Origin,combat:true);break;
                case WeaponId.Shotgun:PlayAt(Bank("shotgun"),gain,shot.Shooter,shot.Origin,combat:true);break;
                case WeaponId.RocketLauncher:PlayAt(clips["rocket"],gain,shot.Shooter,shot.Origin,combat:true);break;
                // Cutter attack is the ramp of the accepted loop, not an old one-shot.
            }
        }
        void Damage(DamageNotice damage)
        {
            if(session==null)return;
            if(damage.Time-lastHit[damage.Participant]<profile.Get("audio.hitIntervalSeconds"))return;
            lastHit[damage.Participant]=damage.Time;
            var clip=damage.ArmorLost>0?clips["shield-hit"]:Bank("body-hit");
            PlayAt(clip,.85f,damage.Participant,damage.Impact.Valid?damage.Impact.Point:session.Pose(damage.Participant).Position);
        }
        void Death(DeathNotice death)
        {
            if(death.Seat==botReactionSpeaker)StopBotReaction();
            string cue=NativeKillNotice.Voice(death.Score);
            if(cue!=null&&composition!=null&&composition.SeatOf(death.Score.Source)>=0&&EffectsVolume>0)
            {killSeriesSource.clip=clips[cue];killSeriesSource.volume=KillSeriesGain*EffectsVolume;killSeriesSource.Play();}
            beams[death.Seat].Stop();beamGain[death.Seat]=0;beamEnvelope[death.Seat]=0;
            PlayAt(Bank("death"),ProvingProfile.DeathAudioGain(profile),death.Seat,death.Pose.Position,feedback:true);
        }
        void WeaponState(int participant)
        {
            var now=session.Life(participant);var old=lastWeaponState[participant];lastWeaponState[participant]=now;
            if(now.Dead||now.Life!=old.Life||old.Dead)
            {weaponReadyPending[participant]=false;return;}
            if(now.SwitchRemaining>0){weaponReadyPending[participant]=false;return;}
            if(now.SelectedWeapon!=old.SelectedWeapon)weaponReadyPending[participant]=true;
            if(!weaponReadyPending[participant]||now.CooldownRemaining>0)return;
            bool hasAmmo=now.SelectedWeapon==WeaponId.Cutter?now.CutterEnergy>0:now.Ammo>0;
            weaponReadyPending[participant]=false;
            if(!hasAmmo)return;
            // Private readiness feedback; the single shared output does not broadcast bot/opponent switches.
            for(int seat=0;composition!=null&&seat<composition.LocalCount;seat++)
                if(composition.ParticipantAt(seat)==participant)
                {PlayAt(clips["weapon-ready"],ProvingProfile.WeaponReadyAudioGain(profile),participant,session.Pose(participant).Position,feedback:true);break;}
        }
        void Explosion(RocketExplosion explosion)=>PlayAt(clips["explosion"],1,explosion.Rocket.Owner,explosion.Position,combat:true);
        void DamageBonusSpawn()=>PlayDamageBonus("damage-bonus-spawn",profile.Get("audio.damageBonusSpawnGain"));
        void PlayDamageBonus(string name,float gain)
        {
            if(!clips.TryGetValue(name,out var clip)||clip==null||EffectsVolume<=0||gain<=0)return;
            damageBonusSource.Stop();damageBonusSource.clip=clip;damageBonusGain=gain;damageBonusSource.Play();UpdateVolume();
        }
        void Pickup(int participant,string id,NativeBotPickupKind kind)
        {
            bool collectorLocal=false,otherLocal=false;
            for(int seat=0;composition!=null&&seat<composition.LocalCount;seat++)
                if(composition.ParticipantAt(seat)==participant)collectorLocal=true;else otherLocal=true;
            if(kind==NativeBotPickupKind.Damage)
            {
                if(collectorLocal)Play("damage-pickup",.9f,participant);
                if(otherLocal)PlayDamageBonus("damage-bonus-pickup",profile.Get("audio.damageBonusPickupGain"));
                return;
            }
            string name=kind==NativeBotPickupKind.Weapon?"weapon-pickup":kind==NativeBotPickupKind.Armor?"shield-pickup":kind==NativeBotPickupKind.Speed?"speed-pickup":"heal-pickup";
            Play(name,.9f,participant);
        }
        public void Tick(bool running)
        {
            if(session==null)return;
            if(!running)StopEffects();
            TickRockets(running);
            for(int i=0;i<session.ParticipantCount;i++)
            {
                var now=session.Pose(i);var old=lastPose[i];lastPose[i]=now;
                if(!running||session.Life(i).Dead){stepDistance[i]=0;beams[i].Stop();beamGain[i]=0;beamEnvelope[i]=0;continue;}
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
                var beam=beams[i];var state=session.Beam(i);bool active=state.Active&&EffectsVolume>0;
                if(active)beamMix[i]=Spatial(state.Origin,i,state.Endpoint);
                var mix=beamMix[i];
                beamEnvelope[i]=Mathf.MoveTowards(beamEnvelope[i],active?1:0,Time.fixedDeltaTime/(active?.055f:.14f));
                beamGain[i]=beamEnvelope[i]*mix.Gain*.55f*ProvingProfile.WeaponAudioGain(profile);beam.panStereo=mix.Pan;
                if(active&&!beam.isPlaying&&beam.clip!=null)beam.Play();
                if(!active&&beamEnvelope[i]<=0&&beam.isPlaying)beam.Stop();
            }
            UpdateVolume();
        }
        void TickRockets(bool running)
        {
            if(!running||EffectsVolume<=0)return;
            var current=session.Rockets;
            for(int slot=0;slot<rocketSources.Length;slot++)
            {
                bool exists=false;foreach(var rocket in current)if(rocket.Id==rocketIds[slot]){exists=true;break;}
                if(!exists){rocketSources[slot].Stop();rocketIds[slot]=0;rocketGain[slot]=0;}
            }
            foreach(var rocket in current)
            {
                var mix=Spatial(rocket.Position,rocket.Owner);
                int slot=Array.IndexOf(rocketIds,rocket.Id);
                if(slot<0)
                {
                    if(mix.Gain<=.005f)continue;
                    slot=Array.IndexOf(rocketIds,0u);if(slot<0)continue;
                    rocketIds[slot]=rocket.Id;
                }
                rocketGain[slot]=mix.Gain*.55f;var source=rocketSources[slot];source.transform.position=rocket.Position;source.panStereo=mix.Pan;
                if(!source.isPlaying&&source.clip!=null)source.Play();
            }
        }
        public void Dispose(){Unbind();musicScene.Dispose();if(root)UnityEngine.Object.Destroy(root.gameObject);}
    }
}
