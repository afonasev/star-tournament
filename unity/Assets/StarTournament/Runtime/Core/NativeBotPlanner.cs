using System;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public enum NativeBotIntent { Search, Pursue, Engage, Retreat, Support, Pickup }
    [Serializable] public struct NativeBotAlly { public int Participant; public Vector3 Position, Velocity; public float Health, Armor; public string Pickup; }
    public sealed class NativeBotFrame
    {
        public ParticipantState Pose;
        public CombatLifeState Life;
        public WeaponPickupState[] WeaponObjectives = Array.Empty<WeaponPickupState>();
        public NativeBotPickupEvent[] Pickups = Array.Empty<NativeBotPickupEvent>();
        public double SpeedRemaining, DamageRemaining;
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
    public interface INativeRocketSurfaceTactics
    {
        bool Surface(Vector3 observedFeet,float probe,out Vector3 point);
        bool FirstContact(Vector3 origin,Vector3 direction,float distance,out Vector3 point);
        bool SplashReach(Vector3 impact,Vector3 participantFeet);
    }
    public sealed class NativeShotgunPolicy : INativeBotWeaponPolicy
    {
        readonly float eye, target, maxPitch;
        public float CapsuleRadius {get;} public float CapsuleHeight {get;}
        readonly ProvingProfile combat,cutter,life;
        public NativeBotWeaponEstimate Estimate {get;}
        public float RifleSpeed=>combat.Get("rifle.speed");
        public float RocketSpeed=>combat.Get("rocket.speed");
        public float RocketRadius=>combat.Get("rocket.radius");
        public float Capacity(WeaponId id)=>id==WeaponId.Cutter?cutter.Get("cutter.energyCapacity"):life.Get(id==WeaponId.Rifle?"rifle.startingAmmo":id==WeaponId.Shotgun?"combat.startingAmmo":"rocket.startingAmmo");
        public float DamageMultiplier=>life.Get("damageBoost.multiplier");
        public float MaximumArmor=>life.Get("armor.maximum");
        public float CutterMinimum=>cutter.Get("cutter.botMinimumDistance");
        public float CutterPreferredRange=>cutter.Get("cutter.maxRangeMeters")*cutter.Get("cutter.botRangeFraction");
        public float EyeHeight=>eye; public float TargetHeight=>target;
        public float RocketMinimumDistance=>combat.Get("rocket.botMinimumDistance");
        public float RocketSafety=>combat.Get("rocket.radius")+combat.Get("rocket.botSafetyMargin");
        public float RangeFor(WeaponId id)=>id==WeaponId.Rifle?float.PositiveInfinity:id==WeaponId.Cutter?cutter.Get("cutter.maxRangeMeters"):combat.Get(id==WeaponId.RocketLauncher?"rocket.range":"shot.range");
        public float Range { get; }
        public string Identity { get; }
        public NativeShotgunPolicy(ProvingProfile movement, ProvingProfile combat, ProvingProfile cutter=null,ProvingProfile lifecycle=null)
        {
            life=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(lifecycle??ProvingProfile.CreateCombatDefault()));
            this.cutter=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(cutter??ProvingProfile.CreateCutterDefault()));
            this.combat=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(combat));this.combat.EnsureNativeCombatDescriptors();
            Identity="native-loadout-cutter-v1|"+JsonUtility.ToJson(movement)+"|"+JsonUtility.ToJson(combat)+"|"+JsonUtility.ToJson(this.cutter)+"|"+JsonUtility.ToJson(life);
            CapsuleRadius=movement.Get("player.capsule.radius");CapsuleHeight=movement.Get("player.capsule.height");
            maxPitch=movement.Get("camera.maximumPitchDegrees");eye=movement.Get("camera.eyeHeight");target=movement.Get("player.capsule.height")*combat.Get("zone.torsoY");
            Range=combat.Get("shot.range");
            Estimate=new NativeBotWeaponEstimate(movement,this.combat,this.life,this.cutter,this);
        }
        public LocalAction Aim(ParticipantState own,Vector3 feet,double time,float seconds,float phase,
            float rate,float error,float period,out float residual)
        {
            return AimPoint(own,feet+Vector3.up*target,time,seconds,phase,rate,error,period,out residual);
        }
        public LocalAction AimPoint(ParticipantState own,Vector3 point,double time,float seconds,float phase,float rate,float error,float period,out float residual)
        {
            var delta=point-(own.Position+Vector3.up*eye);
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
            // Fundamental and second harmonic form a smooth error waveform through zero;
            // the harmonic is a technical shape invariant, amplitude/period remain profile-owned.
            float wave=(float)(time/period*Math.PI*2)+phase;
            float nextYaw=own.Yaw+Mathf.Clamp(Mathf.DeltaAngle(own.Yaw,yaw+Mathf.Sin(wave)*error),-rate*seconds,rate*seconds);
            float nextPitch=Mathf.Clamp(Mathf.MoveTowards(own.Pitch,pitch+Mathf.Sin(wave*2)*error,rate*seconds),-maxPitch,maxPitch);
            residual=Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(nextYaw,yaw)),Mathf.Abs(nextPitch-pitch));
            return new LocalAction{LookDegrees=new Vector2(Mathf.DeltaAngle(own.Yaw,nextYaw),own.Pitch-nextPitch)};
        }
    }
    [Serializable] public sealed class NativeBotPlannerState
    {
        public int Version=2, OwnLife, Target=-1, TargetLife, Anchor=-1, StrafeSign=1;
        public string Configuration;
        public uint Random;
        public NativeBotIntent Intent;
        public double Time=-1, NextDecision, Noticed, NextStrafe, NextJump, RetreatUntil, RetreatReady, SupportUntil, SupportReady, RouteRetryAt;
        public Vector3 Goal, JumpDirection;
        public bool HasGoal, Pressed, JumpActive, DamageBoostActive, RejectedTargetChecked;
        public float Personality, AimPhase;
        public NativeNavigationState Navigation;
        public string PickupId, RejectedPickup;
        public double PickupUntil, PickupStarted, PickupRetryAt, WeaponUntil;
        public WeaponId DesiredWeapon;
        public int RejectedTarget=-1,RejectedTargetLife;
        public double TargetRetryAt,LocalRecoveryUntil;
    }
    public sealed class NativeBotPlanner
    {
        readonly ProvingProfile profile;
        readonly string prefix,configuration;
        readonly INativeNavigation routes;
        readonly ProvingProfile navigationProfile;
        readonly INativeBotTactics tactics;
        readonly INativeBotWeaponPolicy weapon;
        readonly float maxHealth, damageMultiplier;
        readonly NativeBotDifficulty difficulty;
        NativeBotNavigation navigation;
        NativeBotPlannerState state;
        public NativeBotIntent Intent=>state.Intent;
        public string PickupId=>state.PickupId;
        public NativeNavigationStatus NavigationStatus=>navigation.Status;
        public int Jumps {get;private set;}
        public int FireAttempts {get;private set;}
        public int PickupDecisions {get;private set;}
        public int UnlockDecisions {get;private set;}
        public int RefillDecisions {get;private set;}
        public int WeaponDecisions {get;private set;}
        public int RocketRejected {get;private set;}
        float S(string field)=>profile.Get("bots.strategy."+field);
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
            if(behavior.Get("bots.tactics.jumpSamples")%1!=0||behavior.Get("bots.strategy.weaponTimeSamples")%1!=0||behavior.Get("bots.strategy.weaponSpreadSamples")%1!=0)throw new ArgumentException("Integral jump samples required");
            this.routes=routes??throw new ArgumentNullException(nameof(routes));this.tactics=tactics??throw new ArgumentNullException(nameof(tactics));
            this.weapon=weapon??throw new ArgumentNullException(nameof(weapon));maxHealth=maximumHealth;
            this.difficulty=difficulty;damageMultiplier=weapon is NativeShotgunPolicy loadout?loadout.DamageMultiplier:ProvingProfile.CreateCombatDefault().Get("damageBoost.multiplier");
            profile=CopyProfile(behavior);this.navigationProfile=CopyProfile(navigationProfile);prefix="bots."+difficulty.ToString().ToLowerInvariant()+".";
            navigation=new NativeBotNavigation(routes,this.navigationProfile);
            configuration="boost-aggression-v1|"+difficulty+"|"+seed+"|"+routes.Identity+"|"+JsonUtility.ToJson(profile)+"|"+JsonUtility.ToJson(this.navigationProfile)+"|"+maximumHealth+"|"+weapon.Identity;
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
        public void ValidateSnapshot(NativeBotPlannerState s)=>ValidatedNavigation(s);
        NativeBotNavigation ValidatedNavigation(NativeBotPlannerState s)
        {
            if(s==null||s.Version!=2||s.Configuration!=configuration||s.Random==0||s.OwnLife<0||s.Target< -1||s.TargetLife<0||s.Anchor< -1||
                s.RejectedTarget< -1||s.RejectedTargetLife<0||(s.DesiredWeapon!=0&&!Enum.IsDefined(typeof(WeaponId),s.DesiredWeapon))||s.Anchor>=tactics.Anchors.Length||(s.StrafeSign!=1&&s.StrafeSign!= -1)||!Enum.IsDefined(typeof(NativeBotIntent),s.Intent)||
                !Finite(s.Goal)||!Finite(s.Personality)||Mathf.Abs(s.Personality-1)>C("personalitySpread")+.000001f||!Finite(s.AimPhase)||
                !Finite(s.Time)||s.Time< -1||!Finite(s.NextDecision)||!Finite(s.Noticed)||!Finite(s.NextStrafe)||!Finite(s.NextJump)||
                !Finite(s.RetreatUntil)||!Finite(s.RetreatReady)||!Finite(s.SupportUntil)||!Finite(s.SupportReady)||!Finite(s.RouteRetryAt)||!Finite(s.JumpDirection)||!Finite(s.PickupUntil)||!Finite(s.PickupStarted)||!Finite(s.PickupRetryAt)||!Finite(s.WeaponUntil)||!Finite(s.TargetRetryAt)||!Finite(s.LocalRecoveryUntil)||s.PickupStarted<0||s.PickupStarted>Math.Max(0,s.Time)||s.Navigation==null)
                throw new ArgumentException("Invalid planner snapshot");
            double now=Math.Max(0,s.Time);float spread=1+C("personalitySpread");
            bool Deadline(double value,double max)=>value>=0&&value<=now+max+.00001; // Floating-point tolerance, not tuning.
            if(s.Noticed<0||s.Noticed>now||(s.Target==-1)!=(s.TargetLife==0)||s.Navigation.Time>s.Time||
                !Deadline(s.PickupUntil,S("intentHoldSeconds"))||!Deadline(s.PickupRetryAt,S("pickupRetrySeconds"))||!Deadline(s.TargetRetryAt,S("pickupRetrySeconds"))||!Deadline(s.LocalRecoveryUntil,navigationProfile.Get("bots.navigation.recoverySeconds"))||!Deadline(s.WeaponUntil,S("weaponHoldSeconds"))||
                !Deadline(s.NextDecision,P("decisionSeconds"))||!Deadline(s.NextStrafe,P("strafeSeconds")*spread)||
                !Deadline(s.NextJump,P("jumpCooldownSeconds")*spread)||!Deadline(s.RetreatUntil,P("retreatSeconds"))||
                !Deadline(s.RetreatReady,P("retreatSeconds")+T("retreatCooldownSeconds"))||!Deadline(s.SupportUntil,C("supportSeconds"))||
                !Deadline(s.SupportReady,C("supportSeconds")+T("supportCooldownSeconds"))||
                !Deadline(s.RouteRetryAt,navigationProfile.Get("bots.navigation.stuckSeconds")+navigationProfile.Get("bots.navigation.recoverySeconds"))||
                (s.JumpActive&&(s.OwnLife==0||Mathf.Abs(s.JumpDirection.magnitude-1)>.00001f||Mathf.Abs(s.JumpDirection.y)>.00001f)))
                throw new ArgumentException("Inconsistent planner time or intent");
            var candidate=new NativeBotNavigation(routes,navigationProfile);candidate.Restore(s.Navigation);return candidate;
        }
        public void Restore(NativeBotPlannerState s)
        {
            var candidate=ValidatedNavigation(s);
            state=Copy(s);if(string.IsNullOrEmpty(state.PickupId))state.PickupId=null;if(string.IsNullOrEmpty(state.RejectedPickup))state.RejectedPickup=null;state.Navigation=null;navigation=candidate;
        }
        public LocalAction Tick(double time,float seconds,NativeBotFrame f)
        {
            if(!Finite(time)||time<0||time<=state.Time||!Finite(seconds)||seconds<=0||f==null||f.Knowledge==null||f.Knowledge.Enemies==null||
                f.Allies==null||f.Pickups==null||!Finite(f.Pose.Position)||!Finite(f.Pose.Yaw)||!Finite(f.Pose.Pitch)||f.Life.Life<1||f.Knowledge.OwnLife!=f.Life.Life||f.Knowledge.Alive==f.Life.Dead)
                throw new ArgumentException("Invalid bot frame");
            state.Time=time;
            if(state.OwnLife!=f.Life.Life||f.Life.Dead)
            {
                navigation.Clear();state.OwnLife=f.Life.Life;state.Target=-1;state.TargetLife=0;state.HasGoal=false;state.Pressed=false;
                state.PickupId=state.RejectedPickup=null;state.PickupUntil=state.PickupStarted=state.PickupRetryAt=state.WeaponUntil=0;state.DesiredWeapon=0;state.RejectedTarget=-1;state.RejectedTargetLife=0;state.TargetRetryAt=state.LocalRecoveryUntil=0;
                state.NextDecision=time;state.RetreatUntil=state.SupportUntil=0;state.RetreatReady=state.SupportReady=time;
                state.RejectedTargetChecked=false;state.DamageBoostActive=false;state.JumpActive=false;state.RouteRetryAt=0;state.NextStrafe=time;state.NextJump=time;state.Intent=NativeBotIntent.Search;
            }
            if(f.Life.Dead)return default;
            bool boosted=f.DamageRemaining>0;
            if(boosted!=state.DamageBoostActive)
            {
                state.DamageBoostActive=boosted;state.NextDecision=time;
                // Replan on either policy transition; otherwise a throttled old chase route
                // can keep moving forward after expiry selects retreat. Preserve floor transitions.
                navigation.ForgetEnemy();state.HasGoal=navigation.InTransition;
                if(boosted)
                {
                    state.RetreatUntil=state.SupportUntil=0;state.RetreatReady=time;
                    state.PickupId=null;state.PickupUntil=0;
                }
            }
            if(navigation.Status==NativeNavigationStatus.Blocked&&!navigation.InTransition&&state.Target>=0&&state.PickupId==null)
            {state.RejectedTargetChecked=false;state.RejectedTarget=state.Target;state.RejectedTargetLife=state.TargetLife;state.TargetRetryAt=time+S("pickupRetrySeconds");state.Target=-1;state.TargetLife=0;state.HasGoal=false;navigation.ForgetEnemy();state.NextDecision=time;}
            var known=f.Knowledge.Enemies;
            bool decision=time>=state.NextDecision||(state.PickupId!=null&&!f.Pickups.Any(p=>p.Id==state.PickupId&&p.Available))||(state.Target>=0&&!known.Any(k=>k.Sighting.Participant==state.Target&&k.Sighting.Life==state.TargetLife));
            if(decision)
            {
                state.NextDecision=time+P("decisionSeconds");
                NativeBotMemoryEntry? best=null;float distance=float.PositiveInfinity;
                foreach(var k in known)
                {
                    if((!k.Visible||!state.RejectedTargetChecked)&&k.Sighting.Participant==state.RejectedTarget&&k.Sighting.Life==state.RejectedTargetLife&&time<state.TargetRetryAt)continue;
                    // Boosted pursuit must not repeatedly pick a nearer but unreachable enemy.
                    if(boosted&&(!routes.TryRoute(f.Pose.Position,k.Sighting.Position,out var path,out _)||path.Length==0))continue;
                    // Visibility wins; ordinary vulnerability scoring is discounted as knowledge ages.
                    float d=Vector3.Distance(f.Pose.Position,k.Sighting.Position)-(boosted?0:S("vulnerableWeight"))*Mathf.Clamp01(1-(k.Sighting.HasVitals?k.Sighting.Health+k.Sighting.Armor:maxHealth)/maxHealth)/(1+(float)(time-k.ObservedAt));
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
            // Reaching a checked empty position ends this stale pursuit. Do not query
            // hidden combat truth: the same rule covers a killed or escaped opponent.
            if(memory.HasValue&&!memory.Value.Visible&&!navigation.InTransition&&f.Pose.Grounded&&
                Vector2.Distance(new Vector2(f.Pose.Position.x,f.Pose.Position.z),new Vector2(memory.Value.Sighting.Position.x,memory.Value.Sighting.Position.z))<=navigationProfile.Get("bots.navigation.waypointRadius")&&
                Mathf.Abs(f.Pose.Position.y-memory.Value.Sighting.Position.y)<=navigationProfile.Get("bots.navigation.heightTolerance"))
            {
                state.RejectedTargetChecked=true;state.RejectedTarget=state.Target;state.RejectedTargetLife=state.TargetLife;state.TargetRetryAt=time+S("pickupRetrySeconds");
                state.Target=-1;state.TargetLife=0;state.HasGoal=false;navigation.ForgetEnemy();memory=null;decision=true;state.NextDecision=time;
            }
            bool visible=memory.HasValue&&memory.Value.Visible;
            bool surprised=visible&&time-state.Noticed<T("surpriseWindowSeconds")&&
                Mathf.Abs(Mathf.DeltaAngle(f.Pose.Yaw,Mathf.Atan2(memory.Value.Sighting.Position.x-f.Pose.Position.x,memory.Value.Sighting.Position.z-f.Pose.Position.z)*Mathf.Rad2Deg))>P("fireToleranceDegrees");
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
                bool stronger=memory.Value.Sighting.HasVitals&&memory.Value.Sighting.Health+memory.Value.Sighting.Armor>OwnStrength(f)*S("strongerRatio");
                bool mayRetreat=!boosted||difficulty==NativeBotDifficulty.Hard&&visible&&stronger;
                if(boosted&&!mayRetreat)state.RetreatUntil=0;
                if(time<state.RetreatUntil){state.Intent=NativeBotIntent.Retreat;}
                else if(time<state.SupportUntil){state.Intent=NativeBotIntent.Support;}
                else if(decision&&visible)
                {
                    if(mayRetreat&&time>=state.RetreatReady&&(stronger||!boosted&&f.Life.Health/maxHealth<P("retreatHealthRatio")))TryRetreat(time,f,memory.Value.Sighting.Position);
                    if(!boosted&&state.Intent==NativeBotIntent.Engage&&time>=state.SupportReady&&Random()<P("supportChance"))TrySupport(time,f,memory.Value.Sighting.Position);
                }
            }
            else
            {
                state.Intent=NativeBotIntent.Search;
                if(decision&&!navigation.InTransition&&(!state.HasGoal||navigation.Status==NativeNavigationStatus.Idle||navigation.Status==NativeNavigationStatus.Arrived||navigation.Status==NativeNavigationStatus.Blocked))
                {
                    var anchors=tactics.Anchors;if(anchors.Length>0){state.Anchor=(state.Anchor+1)%anchors.Length;state.Goal=anchors[state.Anchor];state.HasGoal=true;navigation.SetStaticGoal(state.Goal);}
                }
            }
            ChoosePickup(time,f,decision,visible);
            // Stored tactical goals live in navigation, separate from currently observed target position.
            if(memory.HasValue&&state.Intent!=NativeBotIntent.Retreat&&state.Intent!=NativeBotIntent.Support&&state.Intent!=NativeBotIntent.Pickup&&navigation.FollowedEnemy!=state.Target)
                navigation.FollowEnemy(state.Target);
            LocalAction aim=default;float residual=float.PositiveInfinity;
            WeaponId desired=ChooseWeapon(time,f,memory,decision);
            Vector3 aimPoint=memory.HasValue?memory.Value.Sighting.Position:Vector3.zero;
            bool rocketSafe=false;
            // Facing belongs to the known threat, independently of route/intent ownership.
            // Pickup selection and retreat expiry must not turn our back during an LOS gap.
            // The perception module bounds memory lifetime; fire still requires visibility.
            bool trackThreat=memory.HasValue;
            if(trackThreat&&weapon is NativeShotgunPolicy lp)
            {
                aimPoint+=Vector3.up*lp.TargetHeight;
                if(visible&&desired==WeaponId.Rifle)
                    aimPoint+=memory.Value.Sighting.Velocity*(Vector3.Distance(f.Pose.Position,aimPoint)/lp.RifleSpeed)*P("leadQuality");
                if(visible&&desired==WeaponId.RocketLauncher)rocketSafe=RocketPlan(f,memory.Value.Sighting,out aimPoint);
                aim=lp.AimPoint(f.Pose,aimPoint,time,seconds,state.AimPhase,P("aimDegreesPerSecond"),P("aimErrorDegrees"),P("aimPeriodSeconds"),out residual);
            }
            else if(trackThreat)aim=weapon.Aim(f.Pose,memory.Value.Sighting.Position,time,seconds,state.AimPhase,P("aimDegreesPerSecond"),P("aimErrorDegrees"),P("aimPeriodSeconds"),out residual);
            float nextYaw=f.Pose.Yaw+aim.LookDegrees.x;
            float weaponRange=weapon is NativeShotgunPolicy policy?policy.RangeFor(desired):weapon.Range;
            // A protected target requires a route around cover, not in-place combat strafing.
            bool localFight=memory.HasValue&&!memory.Value.Sighting.ShotBlocked&&Vector3.Distance(f.Pose.Position,memory.Value.Sighting.Position)<=weaponRange&&
                routes.TryLocate(f.Pose.Position,out var ownSupport)&&routes.TryLocate(memory.Value.Sighting.Position,out var enemySupport)&&
                ownSupport.Support==enemySupport.Support&&!ownSupport.Support.StartsWith("transition:");
            // Probe the actual short manoeuvre below, not the entire straight approach to a
            // distant opponent: an obstruction along that approach need not block a local strafe.
            bool tactical=(localFight&&(state.Intent==NativeBotIntent.Engage||state.Intent==NativeBotIntent.Pursue)&&time>=state.LocalRecoveryUntil)||state.JumpActive;
            var action=navigation.Tick(time,f.Pose,f.Knowledge,trackThreat?nextYaw:(float?)null,tactical);
            if(trackThreat)action.LookDegrees=aim.LookDegrees;
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
                float preferred=visible?P("preferredDistanceMeters")*state.Personality:0;
                float approach=Mathf.Clamp((delta.magnitude-preferred)/T("probeDistance"),-1,1);
                if(boosted)approach=Mathf.Max(0,approach);
                // A remembered position must actually be checked, not orbited at combat range.
                float lateral=visible?P("strafeWeight"):P("strafeWeight")*Mathf.Clamp01(delta.magnitude/P("preferredDistanceMeters"));
                var direction=forward*approach+side*lateral;
                foreach(var ally in f.Allies){var away=f.Pose.Position-ally.Position;away.y=0;if(away.magnitude<C("separationMeters"))direction+=away.normalized*(1-away.magnitude/C("separationMeters"));}
                direction=direction.normalized;
                if(!tactics.CanMove(f.Pose.Position,direction,T("probeDistance")))
                {
                    direction=new[]{-side,forward,-forward,-forward+side,-forward-side}.FirstOrDefault(d=>tactics.CanMove(f.Pose.Position,d.normalized,T("probeDistance"))).normalized;
                    if(direction.sqrMagnitude==0){state.LocalRecoveryUntil=time+navigationProfile.Get("bots.navigation.recoverySeconds");}
                }
                var local=Quaternion.Euler(0,-nextYaw,0)*direction;action.Move=new Vector2(local.x,local.z);
                TryCombatJump(time,f,ref action,direction,maneuver||(surprised&&decision),surprised);
            }
            else if(localFight&&state.Intent==NativeBotIntent.Retreat&&!navigationOwns&&action.Move.sqrMagnitude>0)
            {
                var routeDirection=Quaternion.Euler(0,nextYaw,0)*new Vector3(action.Move.x,0,action.Move.y);
                routeDirection.Normalize();
                var side=Vector3.Cross(Vector3.up,routeDirection)*state.StrafeSign;
                var direction=(routeDirection+side*P("strafeWeight")).normalized;
                if(!tactics.CanMove(f.Pose.Position,direction,T("probeDistance")))
                    direction=(routeDirection-side*P("strafeWeight")).normalized;
                if(!tactics.CanMove(f.Pose.Position,direction,T("probeDistance")))direction=routeDirection;
                // A perpendicular offset keeps positive route progress towards cover; navigation
                // retains its goal and stuck/recovery timers throughout the retreat.
                var local=Quaternion.Euler(0,-nextYaw,0)*direction;action.Move=new Vector2(local.x,local.z)*action.Move.magnitude;
                TryCombatJump(time,f,ref action,direction,maneuver||(surprised&&decision),surprised);
            }
            // Use only observed target information and allowlisted allied poses. No hidden enemy query.
            float targetDistance=memory.HasValue?Vector3.Distance(f.Pose.Position,memory.Value.Sighting.Position):float.PositiveInfinity;
            if(desired==WeaponId.RocketLauncher&&visible&&weapon is NativeShotgunPolicy loadout)
            {
                // Validate the actual error-affected ray, including first world/allied/observed-body contact.
                var direction=Quaternion.Euler(f.Pose.Pitch-action.LookDegrees.y,f.Pose.Yaw+action.LookDegrees.x,0)*Vector3.forward;
                rocketSafe=rocketSafe&&RocketRaySafe(f,memory.Value.Sighting,aimPoint,direction);
                if(!rocketSafe)RocketRejected++;
            }
            if(desired!=f.Life.SelectedWeapon&&f.Life.SwitchRemaining<=0)
                action.SelectWeapon=desired==WeaponId.Cutter?WeaponSelection.Cutter:desired==WeaponId.Rifle?WeaponSelection.Rifle:desired==WeaponId.Shotgun?WeaponSelection.Shotgun:WeaponSelection.RocketLauncher;
            int readyAmmo=CombatLife.AmmoFor(f.Life,desired);
            double readyCooldown=CombatLife.CooldownFor(f.Life,desired);
            // Older isolated planner fixtures have no loadout; keep their supplied ammo/cooldown.
            if(f.Life.SelectedWeapon==0){readyAmmo=f.Life.Ammo;readyCooldown=f.Life.CooldownRemaining;action.SelectWeapon=WeaponSelection.None;}
            // A shotgun press must be followed by a complete false tick.
            bool release=state.Pressed;state.Pressed=false;
            if((!release||desired==WeaponId.Cutter)&&visible&&!memory.Value.Sighting.ShotBlocked&&time-state.Noticed>=P("reactionSeconds")&&residual<=P("fireToleranceDegrees")&&
                readyAmmo>0&&readyCooldown<=0&&f.Life.SwitchRemaining<=0&&action.SelectWeapon==WeaponSelection.None&&targetDistance<=weaponRange&&(desired!=WeaponId.RocketLauncher||rocketSafe)&&!AllyInLine(f,memory.Value.Sighting.Position))
            {if(desired==WeaponId.Cutter)action.FireHeld=true;else {action.Fire=true;state.Pressed=true;}FireAttempts++;}
            return action;
        }
        void TryCombatJump(double time,NativeBotFrame f,ref LocalAction action,Vector3 direction,bool maneuver,bool surprised)
        {
            if(difficulty!=NativeBotDifficulty.Easy&&maneuver&&direction.sqrMagnitude>0&&f.Pose.Grounded&&time>=state.NextJump&&Random()<(surprised?Mathf.Max(P("jumpChance"),P("surpriseJumpChance")):P("jumpChance"))&&tactics.CanJump(f.Pose,direction))
            {action.Jump=true;state.JumpActive=true;state.JumpDirection=direction;state.NextJump=time+P("jumpCooldownSeconds")*state.Personality;Jumps++;}
        }
        float OwnStrength(NativeBotFrame f)=>(f.Life.Health+f.Life.Armor)*(f.DamageRemaining>0?damageMultiplier:1);
        bool Armed(NativeBotFrame f)=>f.Life.RifleAmmo>0||f.Life.ShotgunOwned&&f.Life.ShotgunAmmo>0||
            f.Life.RocketOwned&&f.Life.RocketAmmo>0||f.Life.CutterOwned&&f.Life.CutterEnergy>0;
        float PickupNeed(NativeBotFrame f,NativeBotPickupEvent p)
        {
            var loadout=weapon as NativeShotgunPolicy;
            float need=0;
            switch(p.Kind)
            {
                case NativeBotPickupKind.Heal:need=Mathf.Clamp01(1-f.Life.Health/maxHealth)*S("healUrgency");break;
                case NativeBotPickupKind.Armor:need=Mathf.Clamp01(1-f.Life.Armor/(loadout?.MaximumArmor??maxHealth));break;
                case NativeBotPickupKind.Speed:need=f.SpeedRemaining>0?0:1;break;
                case NativeBotPickupKind.Damage:need=f.DamageRemaining>0?0:1;break;
                case NativeBotPickupKind.Weapon:
                    if(!CombatLife.Owned(f.Life,p.Weapon))return 1;
                    double ammo=p.Weapon==WeaponId.Cutter?f.Life.CutterEnergy:CombatLife.AmmoFor(f.Life,p.Weapon);
                    float ratio=(float)ammo/(loadout?.Capacity(p.Weapon)??maxHealth);
                    return ratio>=S("lowAmmoRatio")?0:1-ratio;
            }
            foreach(var ally in f.Allies)
            {
                if(ally.Pickup!=p.Id||Vector3.Distance(ally.Position,p.Position)>=Vector3.Distance(f.Pose.Position,p.Position))continue;
                float alliedNeed=p.Kind==NativeBotPickupKind.Heal?(1-ally.Health/maxHealth)*S("healUrgency"):p.Kind==NativeBotPickupKind.Armor?1-ally.Armor/(loadout?.MaximumArmor??maxHealth):0;
                if(alliedNeed>need)need*=1-S("allyNeedWeight");
            }
            return need;
        }
        void ChoosePickup(double time,NativeBotFrame f,bool decision,bool visible)
        {
            bool attackPriority=f.DamageRemaining>0&&state.Intent!=NativeBotIntent.Retreat;
            bool armed=Armed(f);
            if(attackPriority&&(armed||state.PickupId!=null&&f.Pickups.Any(p=>p.Id==state.PickupId&&p.Kind!=NativeBotPickupKind.Weapon)))
            {
                bool hadPickup=state.PickupId!=null;state.PickupId=null;state.PickupUntil=0;
                if(hadPickup){navigation.ForgetEnemy();state.HasGoal=navigation.InTransition;state.NextDecision=time;}
                if(armed)return;
            }
            if(state.PickupId!=null&&(time-state.PickupStarted>S("pickupTimeoutSeconds")||navigation.Status==NativeNavigationStatus.Blocked))
            {state.RejectedPickup=state.PickupId;state.PickupRetryAt=time+S("pickupRetrySeconds");state.PickupId=null;state.PickupUntil=0;}
            var held=f.Pickups.FirstOrDefault(p=>p.Id==state.PickupId&&p.Available);
            if(held.Id==null||PickupNeed(f,held)<=0){state.PickupId=null;state.PickupUntil=0;}
            if(!decision){if(state.PickupId!=null){state.Intent=NativeBotIntent.Pickup;state.Goal=held.Position;state.HasGoal=true;}return;}
            NativeBotPickupEvent? best=null;float bestScore=visible?S("combatUtility"):0;
            if(state.Intent==NativeBotIntent.Retreat||f.Life.Health/maxHealth<P("retreatHealthRatio")||!armed)bestScore=0;
            foreach(var p in f.Pickups)
            {
                if(attackPriority&&p.Kind!=NativeBotPickupKind.Weapon)continue;
                if(!p.Available||(p.Id==state.RejectedPickup&&time<state.PickupRetryAt))continue;
                float need=PickupNeed(f,p);if(need<=0)continue;
                if(!routes.TryRoute(f.Pose.Position,p.Position,out var route,out _)||route.Length==0)continue;
                float distance=0;var from=f.Pose.Position;foreach(var point in route){distance+=Vector3.Distance(from,point.Position);from=point.Position;}
                float risk=0;foreach(var k in f.Knowledge.Enemies)
                {
                    float closest=Vector3.Distance(k.Sighting.Position,p.Position);var previous=f.Pose.Position;
                    foreach(var point in route){var segment=point.Position-previous;float along=segment.sqrMagnitude==0?0:Mathf.Clamp01(Vector3.Dot(k.Sighting.Position-previous,segment)/segment.sqrMagnitude);closest=Mathf.Min(closest,Vector3.Distance(previous+segment*along,k.Sighting.Position));previous=point.Position;}
                    risk+=Mathf.Clamp01(1-closest/S("riskRadiusMeters"))/(1+(float)(time-k.ObservedAt));
                }
                float score=need*S("pickupBaseUtility")-distance*S("routeCostPerMeter")-risk*S("riskWeight");
                if(p.Id==state.PickupId&&time<state.PickupUntil)score+=S("combatUtility");
                if(score>bestScore){best=p;bestScore=score;}
            }
            if(!best.HasValue){if(state.PickupId!=null){state.PickupId=null;navigation.ForgetEnemy();state.HasGoal=false;}return;}
            if(state.PickupId!=best.Value.Id)
            {
                state.PickupId=best.Value.Id;state.PickupStarted=time;state.PickupUntil=time+S("intentHoldSeconds");PickupDecisions++;
                if(best.Value.Kind==NativeBotPickupKind.Weapon){if(CombatLife.Owned(f.Life,best.Value.Weapon))RefillDecisions++;else UnlockDecisions++;}
            }
            if(!navigation.InTransition){navigation.SetStaticGoal(best.Value.Position);state.Goal=best.Value.Position;state.HasGoal=true;}
            state.Intent=NativeBotIntent.Pickup;
        }
        WeaponId ChooseWeapon(double time,NativeBotFrame f,NativeBotMemoryEntry? memory,bool decision)
        {
            if(f.Life.SwitchRemaining>0)return f.Life.PendingWeapon;
            if(!(weapon is NativeShotgunPolicy lp)||!memory.HasValue||!memory.Value.Visible)return f.Life.SelectedWeapon;
            if(!decision&&state.DesiredWeapon!=0&&CombatLife.Owned(f.Life,state.DesiredWeapon)&&
                (state.DesiredWeapon==WeaponId.Cutter?f.Life.CutterEnergy>0:CombatLife.AmmoFor(f.Life,state.DesiredWeapon)>0))return state.DesiredWeapon;
            float distance=Vector3.Distance(f.Pose.Position,memory.Value.Sighting.Position);
            var best=f.Life.SelectedWeapon;float bestScore=-1,currentScore=-1;
            foreach(var id in new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher,WeaponId.Cutter})
            {
                if(!CombatLife.Owned(f.Life,id)||id==WeaponId.Cutter&&f.Life.CutterEnergy<=0||id!=WeaponId.Cutter&&CombatLife.AmmoFor(f.Life,id)<=0)continue;
                if(id==WeaponId.Cutter&&(distance<lp.CutterMinimum||distance>lp.CutterPreferredRange))continue;
                if(id==WeaponId.RocketLauncher&&!RocketPlan(f,memory.Value.Sighting,out _)){RocketRejected++;continue;}
                float score=lp.Estimate.Estimate(id,f,memory.Value,time,state.AimPhase,S("weaponHorizonSeconds"),
                    (int)S("weaponTimeSamples"),(int)S("weaponSpreadSamples"),P("aimDegreesPerSecond"),P("aimErrorDegrees"),P("aimPeriodSeconds"),P("leadQuality"));
                if(id==f.Life.SelectedWeapon)currentScore=score;
                if(score>bestScore){best=id;bestScore=score;}
            }
            if(currentScore>0&&(time<state.WeaponUntil||bestScore<currentScore*S("weaponHysteresis")))best=f.Life.SelectedWeapon;
            if(best!=state.DesiredWeapon){state.DesiredWeapon=best;state.WeaponUntil=time+S("weaponHoldSeconds");WeaponDecisions++;}
            return best;
        }
        bool RocketPlan(NativeBotFrame f,NativeBotSighting target,out Vector3 point)
        {
            var lp=weapon as NativeShotgunPolicy;point=target.Position;if(lp==null)return false;
            float distance=Vector3.Distance(f.Pose.Position,target.Position);
            float lead=Mathf.Min(S("rocketLeadSeconds"),distance/lp.RocketSpeed)*P("leadQuality");
            var predicted=target.Position+target.Velocity*lead;
            if(distance<=S("rocketCloseMeters"))point=predicted+Vector3.up*lp.TargetHeight;
            else if(tactics is INativeRocketSurfaceTactics surface)
            {if(!surface.Surface(predicted,S("surfaceProbeMeters"),out point))return false;}
            else point=predicted+Vector3.up*lp.TargetHeight;
            var delta=point-(f.Pose.Position+Vector3.up*lp.EyeHeight);
            return delta.magnitude<=lp.RangeFor(WeaponId.RocketLauncher)&&RocketRaySafe(f,target,point,delta.normalized);
        }
        bool RocketRaySafe(NativeBotFrame f,NativeBotSighting target,Vector3 intended,Vector3 direction)
        {
            var lp=(NativeShotgunPolicy)weapon;var origin=f.Pose.Position+Vector3.up*lp.EyeHeight;
            float length=Vector3.Distance(origin,intended)+S("impactToleranceMeters");var endpoint=intended;
            if(tactics is INativeRocketSurfaceTactics surface)
            {
                bool hit=surface.FirstContact(origin,direction,length,out endpoint);
                // Observed body and allied body intersections precede a later world contact.
                float nearest=hit?Vector3.Distance(origin,endpoint):length;
                float body=RocketResolver.Capsule(origin-target.Position,direction,lp.CapsuleRadius,lp.CapsuleHeight-lp.CapsuleRadius,lp.CapsuleRadius);
                if(body>=0&&body<nearest){nearest=body;endpoint=origin+direction*body;hit=true;}
                foreach(var ally in f.Allies)
                {float contact=RocketResolver.Capsule(origin-ally.Position,direction,lp.CapsuleRadius,lp.CapsuleHeight-lp.CapsuleRadius,lp.CapsuleRadius);if(contact>=0&&contact<nearest){nearest=contact;endpoint=origin+direction*contact;hit=true;}}
                if(!hit||Vector3.Distance(endpoint,intended)>lp.RocketRadius||!surface.SplashReach(endpoint,target.Position))return false;
            }
            else if(tactics is INativeRocketTactics fallback)
            {if(!fallback.ClearRocketShot(origin,direction,length,out endpoint))return false;}
            else return false;
            float safety=lp.RocketSafety+S("bodySafetyMargin");
            float flight=Mathf.Min(S("rocketLeadSeconds"),Vector3.Distance(origin,endpoint)/lp.RocketSpeed);
            if(Vector3.Distance(endpoint,f.Pose.Position+Vector3.up*lp.TargetHeight)<=safety||Vector3.Distance(endpoint,f.Pose.Position+f.Pose.Velocity*flight+Vector3.up*lp.TargetHeight)<=safety)return false;
            return !f.Allies.Any(a=>Vector3.Distance(endpoint,a.Position+Vector3.up*lp.TargetHeight)<=safety||Vector3.Distance(endpoint,a.Position+a.Velocity*flight+Vector3.up*lp.TargetHeight)<=safety);
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
            foreach(var goal in tactics.Anchors.OrderByDescending(p=>Vector3.Distance(p,target)-Vector3.Distance(p,f.Pose.Position)))
                if(routes.TryRoute(f.Pose.Position,goal,out _,out _))
                {navigation.SetStaticGoal(goal);state.RetreatUntil=time+P("retreatSeconds");state.Intent=NativeBotIntent.Retreat;return;}
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
