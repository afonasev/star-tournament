using System;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public enum NativeBotIntent { Search, Pursue, Engage, Retreat, Support }
    [Serializable] public struct NativeBotAlly { public int Participant; public Vector3 Position; }
    public sealed class NativeBotFrame
    {
        public ParticipantState Pose;
        public CombatLifeState Life;
        public NativeBotKnowledge Knowledge;
        public NativeBotAlly[] Allies = Array.Empty<NativeBotAlly>();
    }
    // Only static geometry capabilities and allowlisted input DTOs cross the planner boundary.
    public interface INativeBotTactics
    {
        Vector3[] Anchors { get; }
        bool CanMove(Vector3 feet, Vector3 direction, float distance);
        bool CanJump(ParticipantState pose, Vector3 direction);
        bool Covered(Vector3 feet, Vector3 observedEnemyFeet);
    }
    public interface INativeBotWeaponPolicy
    {
        string Identity { get; }
        LocalAction Aim(ParticipantState own, Vector3 observedFeet, double time, float seconds,
            float phase, float turnRate, float error, float period, out float residualDegrees);
        float Range { get; }
    }
    public interface INativeRocketTactics
    { bool ClearRocketShot(Vector3 origin,Vector3 direction,float distance,out Vector3 endpoint); }
    public sealed class NativeShotgunPolicy : INativeBotWeaponPolicy
    {
        readonly float eye, target, maxPitch;
        readonly ProvingProfile combat;
        public float EyeHeight=>eye; public float TargetHeight=>target;
        public float RocketMinimumDistance=>combat.Get("rocket.botMinimumDistance");
        public float RocketSafety=>combat.Get("rocket.radius")+combat.Get("rocket.botSafetyMargin");
        public float RangeFor(WeaponId id)=>combat.Get(id==WeaponId.RocketLauncher?"rocket.range":id==WeaponId.Rifle?"rifle.range":"shot.range");
        public float Range { get; }
        public string Identity { get; }
        public NativeShotgunPolicy(ProvingProfile movement, ProvingProfile combat)
        {
            this.combat=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(combat));this.combat.EnsureNativeCombatDescriptors();
            Identity="native-loadout-pulse-v1|"+JsonUtility.ToJson(movement)+"|"+JsonUtility.ToJson(combat);
            maxPitch=movement.Get("camera.maximumPitchDegrees");eye=movement.Get("camera.eyeHeight");target=movement.Get("player.capsule.height")*combat.Get("zone.torsoY");
            Range=combat.Get("shot.range");
        }
        public LocalAction Aim(ParticipantState own,Vector3 feet,double time,float seconds,float phase,
            float rate,float error,float period,out float residual)
        {
            var delta=feet+Vector3.up*target-(own.Position+Vector3.up*eye);
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
            // Full-cycle sine/cosine are a technical smooth-error waveform, not hidden tuning.
            float wave=(float)(time/period*Math.PI*2)+phase;
            float nextYaw=own.Yaw+Mathf.Clamp(Mathf.DeltaAngle(own.Yaw,yaw+Mathf.Sin(wave)*error),-rate*seconds,rate*seconds);
            float nextPitch=Mathf.Clamp(Mathf.MoveTowards(own.Pitch,pitch+Mathf.Cos(wave)*error,rate*seconds),-maxPitch,maxPitch);
            residual=Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(nextYaw,yaw)),Mathf.Abs(nextPitch-pitch));
            return new LocalAction{LookDegrees=new Vector2(Mathf.DeltaAngle(own.Yaw,nextYaw),own.Pitch-nextPitch)};
        }
    }
    [Serializable] public sealed class NativeBotPlannerState
    {
        public int Version=1, OwnLife, Target=-1, TargetLife, Anchor=-1, StrafeSign=1;
        public string Configuration;
        public uint Random;
        public NativeBotIntent Intent;
        public double Time=-1, NextDecision, Noticed, NextStrafe, NextJump, RetreatUntil, RetreatReady, SupportUntil, SupportReady, RouteRetryAt;
        public Vector3 Goal, JumpDirection;
        public bool HasGoal, Pressed, JumpActive;
        public float Personality, AimPhase;
        public NativeNavigationState Navigation;
    }
    public sealed class NativeBotPlanner
    {
        readonly ProvingProfile profile;
        readonly string prefix,configuration;
        readonly INativeNavigation routes;
        readonly ProvingProfile navigationProfile;
        readonly INativeBotTactics tactics;
        readonly INativeBotWeaponPolicy weapon;
        readonly float maxHealth;
        NativeBotNavigation navigation;
        NativeBotPlannerState state;
        public NativeBotIntent Intent=>state.Intent;
        public NativeNavigationStatus NavigationStatus=>navigation.Status;
        public int Jumps {get;private set;}
        public int FireAttempts {get;private set;}
        float P(string field)=>profile.Get(prefix+field);
        float C(string field)=>profile.Get("bots.cooperation."+field);
        float T(string field)=>profile.Get("bots.tactics."+field);
        public NativeBotPlanner(NativeBotDifficulty difficulty,uint seed,ProvingProfile behavior,
            ProvingProfile navigationProfile,INativeNavigation routes,INativeBotTactics tactics,INativeBotWeaponPolicy weapon,float maximumHealth)
        {
            var canonical=ProvingProfile.CreateBotBehaviorDefault();
            if(seed==0||!Enum.IsDefined(typeof(NativeBotDifficulty),difficulty)||behavior==null||behavior.Id!=canonical.Id||
                behavior.Version!=canonical.Version||behavior.Validate().Count!=0||!Finite(maximumHealth)||maximumHealth<=0)
                throw new ArgumentException("Invalid bot planner configuration");
            foreach(var d in canonical.Descriptors)if(!d.Contains(behavior.Get(d.Path)))throw new ArgumentException("Invalid behavior value: "+d.Path);
            if(behavior.Get("bots.tactics.jumpSamples")%1!=0)throw new ArgumentException("Integral jump samples required");
            this.routes=routes??throw new ArgumentNullException(nameof(routes));this.tactics=tactics??throw new ArgumentNullException(nameof(tactics));
            this.weapon=weapon??throw new ArgumentNullException(nameof(weapon));maxHealth=maximumHealth;
            profile=CopyProfile(behavior);this.navigationProfile=CopyProfile(navigationProfile);prefix="bots."+difficulty.ToString().ToLowerInvariant()+".";
            navigation=new NativeBotNavigation(routes,this.navigationProfile);
            configuration=difficulty+"|"+seed+"|"+routes.Identity+"|"+JsonUtility.ToJson(profile)+"|"+JsonUtility.ToJson(this.navigationProfile)+"|"+maximumHealth+"|"+weapon.Identity;
            state=new NativeBotPlannerState{Configuration=configuration,Random=seed};
            state.Anchor=tactics.Anchors.Length==0?-1:(int)(Random()*tactics.Anchors.Length);
            state.Personality=1+(Random()*2-1)*C("personalitySpread");state.AimPhase=Random()*Mathf.PI*2;
        }
        static ProvingProfile CopyProfile(ProvingProfile p)=>JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(p));
        float Random(){uint x=state.Random;x^=x<<13;x^=x>>17;x^=x<<5;state.Random=x;return (x>>8)*(1f/16777216f);}
        public void Release(){state.Pressed=false;}
        public NativeBotPlannerState Capture()
        {var copy=Copy(state);copy.Navigation=navigation.Capture();return copy;}
        static NativeBotPlannerState Copy(NativeBotPlannerState s)=>JsonUtility.FromJson<NativeBotPlannerState>(JsonUtility.ToJson(s));
        public void Restore(NativeBotPlannerState s)
        {
            if(s==null||s.Version!=1||s.Configuration!=configuration||s.Random==0||s.OwnLife<0||s.Target< -1||s.TargetLife<0||s.Anchor< -1||
                s.Anchor>=tactics.Anchors.Length||(s.StrafeSign!=1&&s.StrafeSign!= -1)||!Enum.IsDefined(typeof(NativeBotIntent),s.Intent)||
                !Finite(s.Goal)||!Finite(s.Personality)||Mathf.Abs(s.Personality-1)>C("personalitySpread")+.000001f||!Finite(s.AimPhase)||
                !Finite(s.Time)||s.Time< -1||!Finite(s.NextDecision)||!Finite(s.Noticed)||!Finite(s.NextStrafe)||!Finite(s.NextJump)||
                !Finite(s.RetreatUntil)||!Finite(s.RetreatReady)||!Finite(s.SupportUntil)||!Finite(s.SupportReady)||!Finite(s.RouteRetryAt)||!Finite(s.JumpDirection)||s.Navigation==null)
                throw new ArgumentException("Invalid planner snapshot");
            double now=Math.Max(0,s.Time);float spread=1+C("personalitySpread");
            bool Deadline(double value,double max)=>value>=0&&value<=now+max+.00001; // Floating-point tolerance, not tuning.
            if(s.Noticed<0||s.Noticed>now||(s.Target==-1)!=(s.TargetLife==0)||s.Navigation.Time>s.Time||
                !Deadline(s.NextDecision,P("decisionSeconds"))||!Deadline(s.NextStrafe,P("strafeSeconds")*spread)||
                !Deadline(s.NextJump,P("jumpCooldownSeconds")*spread)||!Deadline(s.RetreatUntil,P("retreatSeconds"))||
                !Deadline(s.RetreatReady,P("retreatSeconds")+T("retreatCooldownSeconds"))||!Deadline(s.SupportUntil,C("supportSeconds"))||
                !Deadline(s.SupportReady,C("supportSeconds")+T("supportCooldownSeconds"))||
                !Deadline(s.RouteRetryAt,navigationProfile.Get("bots.navigation.stuckSeconds")+navigationProfile.Get("bots.navigation.recoverySeconds"))||
                (s.JumpActive&&(s.OwnLife==0||Mathf.Abs(s.JumpDirection.magnitude-1)>.00001f||Mathf.Abs(s.JumpDirection.y)>.00001f)))
                throw new ArgumentException("Inconsistent planner time or intent");
            var candidate=new NativeBotNavigation(routes,navigationProfile);candidate.Restore(s.Navigation);
            state=Copy(s);state.Navigation=null;navigation=candidate;
        }
        public LocalAction Tick(double time,float seconds,NativeBotFrame f)
        {
            if(!Finite(time)||time<0||time<=state.Time||!Finite(seconds)||seconds<=0||f==null||f.Knowledge==null||f.Knowledge.Enemies==null||
                f.Allies==null||!Finite(f.Pose.Position)||!Finite(f.Pose.Yaw)||!Finite(f.Pose.Pitch)||f.Life.Life<1||f.Knowledge.OwnLife!=f.Life.Life||f.Knowledge.Alive==f.Life.Dead)
                throw new ArgumentException("Invalid bot frame");
            state.Time=time;
            if(state.OwnLife!=f.Life.Life||f.Life.Dead)
            {
                navigation.Clear();state.OwnLife=f.Life.Life;state.Target=-1;state.TargetLife=0;state.HasGoal=false;state.Pressed=false;
                state.NextDecision=time;state.RetreatUntil=state.SupportUntil=0;state.RetreatReady=state.SupportReady=time;
                state.JumpActive=false;state.RouteRetryAt=0;state.NextStrafe=time;state.NextJump=time+P("jumpCooldownSeconds");state.Intent=NativeBotIntent.Search;
            }
            if(f.Life.Dead)return default;
            var known=f.Knowledge.Enemies;
            bool decision=time>=state.NextDecision||(state.Target>=0&&!known.Any(k=>k.Sighting.Participant==state.Target&&k.Sighting.Life==state.TargetLife));
            if(decision)
            {
                state.NextDecision=time+P("decisionSeconds");
                NativeBotMemoryEntry? best=null;float distance=float.PositiveInfinity;
                foreach(var k in known)
                {
                    // Visibility wins over memory; distance only compares equally available observations.
                    float d=Vector3.Distance(f.Pose.Position,k.Sighting.Position);
                    if(!best.HasValue||(k.Visible&&!best.Value.Visible)||(k.Visible==best.Value.Visible&&d<distance)){best=k;distance=d;}
                }
                int target=best?.Sighting.Participant??-1,life=best?.Sighting.Life??0;
                if(target!=state.Target||life!=state.TargetLife)
                {
                    state.Target=target;state.TargetLife=life;state.Noticed=time;
                    state.SupportUntil=0;state.RetreatUntil=0;
                    if(target>=0)navigation.FollowEnemy(target);else {navigation.ForgetEnemy();state.HasGoal=navigation.InTransition;}
                }
            }
            NativeBotMemoryEntry? memory=null;
            foreach(var k in known)if(k.Sighting.Participant==state.Target&&k.Sighting.Life==state.TargetLife){memory=k;break;}
            if(!memory.HasValue&&state.Target>=0){state.Target=-1;state.TargetLife=0;navigation.ForgetEnemy();state.HasGoal=navigation.InTransition;}
            bool visible=memory.HasValue&&memory.Value.Visible;
            if(state.JumpActive&&f.Pose.Grounded)state.JumpActive=false;
            if(decision&&(state.Target>=0||navigation.InTransition)&&navigation.Status==NativeNavigationStatus.Blocked&&time>=state.RouteRetryAt)
            {
                navigation.RetryBlockedRoute();
                state.RouteRetryAt=time+navigationProfile.Get("bots.navigation.stuckSeconds")+navigationProfile.Get("bots.navigation.recoverySeconds");
            }
            if(memory.HasValue)
            {
                state.Intent=visible?NativeBotIntent.Engage:NativeBotIntent.Pursue;
                state.Goal=memory.Value.Sighting.Position;state.HasGoal=true;
                if(time<state.RetreatUntil){state.Intent=NativeBotIntent.Retreat;}
                else if(time<state.SupportUntil){state.Intent=NativeBotIntent.Support;}
                else if(decision&&visible)
                {
                    if(time>=state.RetreatReady&&f.Life.Health/maxHealth<P("retreatHealthRatio"))TryRetreat(time,f,memory.Value.Sighting.Position);
                    if(state.Intent==NativeBotIntent.Engage&&time>=state.SupportReady&&Random()<P("supportChance"))TrySupport(time,f,memory.Value.Sighting.Position);
                }
            }
            else
            {
                state.Intent=NativeBotIntent.Search;
                if(decision&&!navigation.InTransition&&(!state.HasGoal||navigation.Status==NativeNavigationStatus.Idle||navigation.Status==NativeNavigationStatus.Arrived||navigation.Status==NativeNavigationStatus.Blocked))
                {
                    var anchors=tactics.Anchors;
                    if(anchors.Length>0){state.Anchor=(state.Anchor+1)%anchors.Length;state.Goal=anchors[state.Anchor];state.HasGoal=true;navigation.SetStaticGoal(state.Goal);}
                }
            }
            // Stored tactical goals live in navigation, separate from currently observed target position.
            if(memory.HasValue&&state.Intent!=NativeBotIntent.Retreat&&state.Intent!=NativeBotIntent.Support&&navigation.FollowedEnemy!=state.Target)
                navigation.FollowEnemy(state.Target);
            LocalAction aim=default;float residual=float.PositiveInfinity;
            if(visible)aim=weapon.Aim(f.Pose,memory.Value.Sighting.Position,time,seconds,state.AimPhase,P("aimDegreesPerSecond"),P("aimErrorDegrees"),P("aimPeriodSeconds"),out residual);
            float nextYaw=f.Pose.Yaw+aim.LookDegrees.x;
            bool close=visible&&Vector3.Distance(f.Pose.Position,memory.Value.Sighting.Position)<=P("preferredDistanceMeters")*state.Personality+T("probeDistance");
            bool localFight=close&&routes.TryLocate(f.Pose.Position,out var ownSupport)&&routes.TryLocate(memory.Value.Sighting.Position,out var enemySupport)&&
                ownSupport.Support==enemySupport.Support&&!ownSupport.Support.StartsWith("transition:")&&
                tactics.CanMove(f.Pose.Position,(memory.Value.Sighting.Position-f.Pose.Position).normalized,Vector3.Distance(f.Pose.Position,memory.Value.Sighting.Position));
            bool tactical=(localFight&&state.Intent==NativeBotIntent.Engage)||state.JumpActive;
            var action=navigation.Tick(time,f.Pose,f.Knowledge,visible?nextYaw:(float?)null,tactical);
            if(visible)action.LookDegrees=aim.LookDegrees;
            bool navigationOwns=navigation.InTransition||navigation.Status==NativeNavigationStatus.Recovering;
            bool maneuver=false;
            if(time>=state.NextStrafe){state.NextStrafe=time+P("strafeSeconds")*state.Personality;state.StrafeSign=Random()<.5f?-1:1;maneuver=true;}
            if(state.JumpActive)
            {
                var locked=Quaternion.Euler(0,-nextYaw,0)*state.JumpDirection;action.Move=new Vector2(locked.x,locked.z);
            }
            else if(tactical&&!navigationOwns)
            {
                var delta=memory.Value.Sighting.Position-f.Pose.Position;delta.y=0;
                var forward=delta.normalized;var side=Vector3.Cross(Vector3.up,forward)*state.StrafeSign;
                float approach=Mathf.Clamp((delta.magnitude-P("preferredDistanceMeters")*state.Personality)/T("probeDistance"),-1,1);
                var direction=forward*approach+side*P("strafeWeight");
                foreach(var ally in f.Allies){var away=f.Pose.Position-ally.Position;away.y=0;if(away.magnitude<C("separationMeters"))direction+=away.normalized*(1-away.magnitude/C("separationMeters"));}
                direction=direction.normalized;
                if(!tactics.CanMove(f.Pose.Position,direction,T("probeDistance"))){direction=-side;if(!tactics.CanMove(f.Pose.Position,direction,T("probeDistance")))direction=Vector3.zero;}
                var local=Quaternion.Euler(0,-nextYaw,0)*direction;action.Move=new Vector2(local.x,local.z);
                if(maneuver&&f.Pose.Grounded&&time>=state.NextJump&&Random()<P("jumpChance")&&tactics.CanJump(f.Pose,direction))
                {action.Jump=true;state.JumpActive=true;state.JumpDirection=direction;state.NextJump=time+P("jumpCooldownSeconds")*state.Personality;Jumps++;}
            }
            // Use only observed target information and allowlisted allied poses. No hidden enemy query.
            float targetDistance=memory.HasValue?Vector3.Distance(f.Pose.Position,memory.Value.Sighting.Position):float.PositiveInfinity;
            bool closeShotgun=targetDistance<=P("preferredDistanceMeters")*state.Personality;
            var desired=f.Life.SelectedWeapon;
            bool rocketSafe=false;
            if(visible&&memory.HasValue&&weapon is NativeShotgunPolicy loadout&&tactics is INativeRocketTactics rocketTactics&&f.Life.RocketAmmo>0&&targetDistance>=Mathf.Max(loadout.RocketMinimumDistance,loadout.RocketSafety)&&targetDistance<=loadout.RangeFor(WeaponId.RocketLauncher))
            {
                var origin=f.Pose.Position+Vector3.up*loadout.EyeHeight;
                var direction=Quaternion.Euler(f.Pose.Pitch-action.LookDegrees.y,f.Pose.Yaw+action.LookDegrees.x,0)*Vector3.forward;
                rocketSafe=rocketTactics.ClearRocketShot(origin,direction,targetDistance,out var endpoint)&&Vector3.Distance(endpoint,f.Pose.Position)>loadout.RocketSafety;
                foreach(var ally in f.Allies)if(Vector3.Distance(endpoint,ally.Position)<=loadout.RocketSafety+loadout.TargetHeight)rocketSafe=false;
            }
            if(rocketSafe)desired=WeaponId.RocketLauncher;
            else if(f.Life.ShotgunAmmo>0 && (f.Life.RifleAmmo==0||closeShotgun))desired=WeaponId.Shotgun;
            else if(f.Life.RifleAmmo>0)desired=WeaponId.Rifle;
            else if(desired==WeaponId.RocketLauncher)desired=WeaponId.Rifle; // Never use unsafe rockets as an empty fallback.
            if(desired!=f.Life.SelectedWeapon)action.SelectWeapon=desired==WeaponId.Rifle?WeaponSelection.Rifle:desired==WeaponId.Shotgun?WeaponSelection.Shotgun:WeaponSelection.RocketLauncher;
            int readyAmmo=CombatLife.AmmoFor(f.Life,desired);
            double readyCooldown=CombatLife.CooldownFor(f.Life,desired);
            float weaponRange=weapon is NativeShotgunPolicy policy?policy.RangeFor(desired):weapon.Range;
            // Older isolated planner fixtures have no loadout; keep their supplied ammo/cooldown.
            if(f.Life.SelectedWeapon==0){readyAmmo=f.Life.Ammo;readyCooldown=f.Life.CooldownRemaining;action.SelectWeapon=WeaponSelection.None;}
            // A shotgun press must be followed by a complete false tick.
            bool release=state.Pressed;state.Pressed=false;
            if(!release&&visible&&time-state.Noticed>=P("reactionSeconds")&&residual<=P("fireToleranceDegrees")&&
                readyAmmo>0&&readyCooldown<=0&&targetDistance<=weaponRange&&(desired!=WeaponId.RocketLauncher||rocketSafe)&&!AllyInLine(f,memory.Value.Sighting.Position))
            {action.Fire=true;state.Pressed=true;FireAttempts++;}
            return action;
        }
        bool AllyInLine(NativeBotFrame f,Vector3 target)
        {
            var direction=target-f.Pose.Position;float length=direction.magnitude;if(length==0)return false;direction/=length;
            foreach(var ally in f.Allies){var delta=ally.Position-f.Pose.Position;float along=Vector3.Dot(delta,direction);
                if(along>0&&along<length&&(delta-direction*along).magnitude<C("separationMeters"))return true;}
            return false;
        }
        void TryRetreat(double time,NativeBotFrame f,Vector3 target)
        {
            state.RetreatReady=time+P("retreatSeconds")+T("retreatCooldownSeconds");
            var away=f.Pose.Position-target;away.y=0;away=away.normalized;var side=Vector3.Cross(Vector3.up,away);
            foreach(var direction in new[]{away,side,-side})
            {
                var goal=f.Pose.Position+direction*T("coverDistance");
                if(tactics.CanMove(f.Pose.Position,direction,T("coverDistance"))&&tactics.Covered(goal,target))
                {navigation.SetStaticGoal(goal);state.RetreatUntil=time+P("retreatSeconds");state.Intent=NativeBotIntent.Retreat;return;}
            }
        }
        void TrySupport(double time,NativeBotFrame f,Vector3 target)
        {
            state.SupportReady=time+C("supportSeconds")+T("supportCooldownSeconds");
            foreach(var ally in f.Allies)
            {
                if(Vector3.Distance(ally.Position,f.Pose.Position)>C("supportDistanceMeters")||Vector3.Distance(ally.Position,target)>C("supportDistanceMeters"))continue;
                var radial=ally.Position-target;radial.y=0;var side=Vector3.Cross(Vector3.up,radial.normalized)*state.StrafeSign;
                var goal=ally.Position+side*C("separationMeters");var delta=goal-f.Pose.Position;
                if(delta.magnitude<C("separationMeters")||!tactics.CanMove(f.Pose.Position,delta.normalized,delta.magnitude))continue;
                navigation.SetStaticGoal(goal);state.SupportUntil=time+C("supportSeconds");state.Intent=NativeBotIntent.Support;return;
            }
        }
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
    }
}
