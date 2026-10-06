using System;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class NativeBotDriverSnapshot
    {public int Version=1,Ticks;public uint Seed;public NativeBotPerceptionSnapshot Perception;public NativeBotPickupSnapshot Pickups;public NativeBotPlannerState[] Planners;}
    /// <summary>Allowlisted world adapter; all decisions precede the single session tick.</summary>
    public sealed class NativeBotMatchDriver
    {
        readonly NativeCombatSession session;
        readonly NativeMatchComposition composition;
        readonly NativeBotObservationProvider observations;
        readonly NativeBotPlanner[] planners;
        readonly NativeBotTactics[] tactics;
        public NativeBotPerception Perception {get;}
        public NativeBotPickupKnowledge Pickups {get;}
        public uint Seed {get;}
        public int Ticks {get;private set;}
        public double LastMilliseconds {get;private set;}
        public int LastQueries {get;private set;}
        public NativeBotPlanner Planner(int participant)=>planners[participant];
        public NativeBotMatchDriver(NativeCombatSession session,NativeMatchComposition composition,ProvingArena arena,PhysicsScene physics,
            ProvingProfile movement,ProvingProfile life,ProvingProfile combat,ProvingProfile perception,ProvingProfile navigation,ProvingProfile behavior,uint seed)
        {
            this.session=session;this.composition=composition;Seed=seed;
            if(seed==0||session.ParticipantCount!=composition.ParticipantCount)throw new ArgumentException("Invalid driver roster/seed");
            composition.RequireSupportedSources(true,false);
            var difficulties=new NativeBotDifficulty[composition.ParticipantCount];
            for(int p=0;p<difficulties.Length;p++)if(composition.Participant(p).Kind==NativeParticipantKind.Bot)difficulties[p]=(NativeBotDifficulty)composition.Participant(p).Difficulty;
            Pickups=new NativeBotPickupKnowledge(difficulties,perception);
            Perception=new NativeBotPerception(composition.Roster,difficulties,perception);
            observations=new NativeBotObservationProvider(session,composition.Roster,physics,movement,combat);
            planners=new NativeBotPlanner[difficulties.Length];tactics=new NativeBotTactics[difficulties.Length];
            for(int p=0;p<planners.Length;p++)if(composition.Participant(p).Kind==NativeParticipantKind.Bot)
            {
                var routes=new NativeNavigationProvider(arena,movement,navigation);
                tactics[p]=new NativeBotTactics(arena,physics,routes,movement,combat,behavior);
                // Stable integer seed mixing is an identity protocol, never a balance multiplier.
                uint participantSeed=seed^((uint)(p+1)*0x9e3779b9u);if(participantSeed==0)participantSeed=1;
                planners[p]=new NativeBotPlanner(difficulties[p],participantSeed,behavior,navigation,routes,tactics[p],new NativeShotgunPolicy(movement,combat,session.CutterProfile,life),life.Get("combat.maximumHealth"));
            }
        }
        NativeBotPickupEvent[] ReadPickupCatalogue()
        {
            var values=session.WeaponPickups.Select(x=>new NativeBotPickupEvent{Id=x.InstanceId,Kind=NativeBotPickupKind.Weapon,Weapon=x.Weapon,Position=x.Anchor,Available=x.Available})
                .Concat(session.HealPickups.Select(x=>new NativeBotPickupEvent{Id=x.InstanceId,Kind=NativeBotPickupKind.Heal,Position=x.Anchor,Available=x.Available}))
                .Concat(session.ArmorPickups.Select(x=>new NativeBotPickupEvent{Id=x.InstanceId,Kind=NativeBotPickupKind.Armor,Position=x.Anchor,Available=x.Available}));
            if(session.HasSpeedPickup)values=values.Concat(session.SpeedPickups.Select(x=>new NativeBotPickupEvent{Id=x.InstanceId,Kind=NativeBotPickupKind.Speed,Position=x.Anchor,Available=x.Available}));
            if(session.HasDamagePickup){var x=session.DamagePickup;values=values.Append(new NativeBotPickupEvent{Id=x.InstanceId,Kind=NativeBotPickupKind.Damage,Position=x.Anchor,Available=x.Available});}
            return values.ToArray();
        }
        public NativeBotDriverSnapshot Capture()=>new NativeBotDriverSnapshot{Seed=Seed,Ticks=Ticks,Perception=Perception.Capture(),Pickups=Pickups.Capture(),Planners=planners.Select(p=>p?.Capture()).ToArray()};
        public void Restore(NativeBotDriverSnapshot s)
        {
            if(s==null||s.Version!=1||s.Seed!=Seed||s.Ticks<0||s.Perception==null||s.Pickups==null||s.Perception.Time!=s.Pickups.Time||s.Perception.Time>session.Time||s.Planners==null||s.Planners.Length!=planners.Length)throw new ArgumentException("Invalid bot driver snapshot");
            for(int i=0;i<planners.Length;i++)if(planners[i]!=null&&(s.Planners[i]==null||s.Planners[i].Time!=s.Perception.Time))throw new ArgumentException("Invalid planner snapshot clock");
            // Validate every owner before any write. Invalid input cannot revalidate/mutate native routes.
            Perception.ValidateSnapshot(s.Perception);Pickups.ValidateSnapshot(s.Pickups);
            for(int i=0;i<planners.Length;i++)planners[i]?.ValidateSnapshot(s.Planners[i]);
            Perception.Restore(s.Perception);Pickups.Restore(s.Pickups);for(int i=0;i<planners.Length;i++)planners[i]?.Restore(s.Planners[i]);Ticks=s.Ticks;
        }
        public void Release(){foreach(var p in planners)p?.Release();}
        public void ProduceActions(LocalAction[] actions,float seconds)
        {
            if(actions==null||actions.Length!=planners.Length)throw new ArgumentException("Invalid action roster");
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            var frames=observations.Observe(Perception);
            // Humans do not become artificial bot-FOV sensors for team reports.
            for(int i=0;i<frames.Length;i++)if(planners[i]==null)frames[i].Direct=Array.Empty<NativeBotSighting>();
            Perception.Sample(session.Time,frames);Pickups.Sample(session.Time,frames,ReadPickupCatalogue());LastQueries=0;
            var pickupIntents=planners.Select(p=>p?.PickupId).ToArray();
            for(int p=0;p<planners.Length;p++)if(planners[p]!=null)
            {
                var allies=Enumerable.Range(0,planners.Length).Where(i=>i!=p&&composition.Roster.AreAllies(p,i)&&!session.Life(i).Dead)
                    .Select(i=>new NativeBotAlly{Participant=i,Position=session.Pose(i).Position,Velocity=session.Pose(i).Velocity,Health=session.Life(i).Health,Armor=session.Life(i).Armor,Pickup=pickupIntents[i]}).ToArray();
                tactics[p].ResetQueries();
                actions[p]=planners[p].Tick(session.Time,seconds,new NativeBotFrame{Pose=session.Pose(p),Life=session.Life(p),Knowledge=Perception.Read(p),Allies=allies,Pickups=Pickups.Read(p),SpeedRemaining=session.SpeedBoostRemaining(p),DamageRemaining=session.DamageBoostRemaining(p)});
                LastQueries+=tactics[p].Queries;
            }
            Ticks++;LastMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
    }
    public sealed class NativeBotTactics : INativeBotTactics, INativeRocketTactics, INativeRocketSurfaceTactics
    {
        readonly PhysicsScene physics;
        readonly INativeNavigation navigation;
        readonly float radius,height,skin,speed,gravity,jump,eye,target,probe,groundAcceleration,airAcceleration,fixedSeconds;
        readonly int jumpSamples;
        readonly Vector3[] anchors;
        // Fixed scratch capacity bounds query storage; overflow is conservatively rejected.
        readonly Collider[] overlaps=new Collider[32];
        const int Mask=(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer);
        public int Queries {get;private set;}
        public Vector3[] Anchors=>(Vector3[])anchors.Clone();
        public void ResetQueries(){Queries=0;}
        public NativeBotTactics(ProvingArena arena,PhysicsScene physics,INativeNavigation navigation,ProvingProfile movement,ProvingProfile combat,ProvingProfile behavior)
        {
            this.physics=physics;this.navigation=navigation;
            radius=movement.Get("player.capsule.radius");height=movement.Get("player.capsule.height");skin=movement.Get("player.capsule.skinWidth");
            speed=movement.Get("player.movement.maximumGroundSpeed");gravity=movement.Get("player.movement.gravity");jump=movement.Get("player.movement.jumpSpeed");
            groundAcceleration=movement.Get("player.movement.groundAcceleration");airAcceleration=movement.Get("player.movement.airAcceleration");fixedSeconds=1/movement.Get("simulation.fixedTickHz");
            eye=movement.Get("camera.eyeHeight");target=height*combat.Get("zone.torsoY");probe=behavior.Get("bots.tactics.probeDistance");jumpSamples=(int)behavior.Get("bots.tactics.jumpSamples");
            anchors=new[]{arena.LowerRoutePoint,arena.UpperRoutePoint}.Concat(arena.ReadNavigationTransitions().SelectMany(t=>new[]{t.OrderedFeet[0],t.OrderedFeet[t.OrderedFeet.Length-1]})).ToArray();
        }
        public bool Surface(Vector3 feet,float probe,out Vector3 point)
        {
            Queries++;bool hit=physics.Raycast(feet+Vector3.up*skin,Vector3.down,out var floor,probe+skin,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
            point=hit?floor.point:feet;return hit&&floor.normal.y>0;
        }
        public bool FirstContact(Vector3 origin,Vector3 direction,float distance,out Vector3 point)
        {Queries++;bool hit=physics.Raycast(origin,direction,out var wall,distance,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);point=hit?wall.point:origin+direction*distance;return hit;}
        public bool SplashReach(Vector3 impact,Vector3 feet)
        {
            var delta=feet+Vector3.up*target-impact;Queries++;
            return !physics.Raycast(impact+delta.normalized*skin,delta.normalized,out _,Mathf.Max(0,delta.magnitude-skin),1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
        }
        public bool ClearRocketShot(Vector3 origin,Vector3 direction,float distance,out Vector3 endpoint)
        {
            Queries++;bool hit=physics.Raycast(origin,direction,out var wall,distance,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
            endpoint=hit?wall.point:origin+direction*distance;return !hit;
        }
        bool Locate(Vector3 feet,out NativeNavigationPoint point){Queries++;return navigation.TryLocate(feet,out point);}
        public bool CanMove(Vector3 feet,Vector3 direction,float distance)
        {
            direction.y=0;if(direction.sqrMagnitude==0)return true;direction.Normalize();
            if(!Locate(feet,out var start)||start.Support.StartsWith("transition:"))return false;
            var end=feet+direction*distance;
            if(!Locate(end,out var finish)||start.Support!=finish.Support)return false;
            Queries++;
            if(physics.CapsuleCast(feet+Vector3.up*(radius+skin),feet+Vector3.up*(height-radius),radius,direction,out _,distance,Mask,QueryTriggerInteraction.Ignore))return false;
            // Verify support along the segment, not only at endpoints across a hole.
            int steps=Mathf.CeilToInt(distance/probe);
            for(int i=1;i<steps;i++)if(!Locate(Vector3.Lerp(feet,end,(float)i/steps),out var support)||support.Support!=start.Support)return false;
            return true;
        }
        public bool CanJump(ParticipantState pose,Vector3 direction)
        {
            var feet=pose.Position;
            if(!pose.Grounded||direction.sqrMagnitude==0||!Locate(feet,out var start)||start.Support.StartsWith("transition:"))return false;
            direction.y=0;direction.Normalize();
            var horizontal=new Vector3(pose.Velocity.x,0,pose.Velocity.z);float vertical=jump;var previous=feet;
            for(int i=0;i<jumpSamples;i++)
            {
                horizontal=Vector3.MoveTowards(horizontal,direction*speed,(i==0?groundAcceleration:airAcceleration)*fixedSeconds);
                if(i>0)vertical-=gravity*fixedSeconds;
                var point=previous+(horizontal+Vector3.up*vertical)*fixedSeconds;
                bool landing=i>0&&point.y<=feet.y;
                if(landing)point.y=feet.y;
                var segment=point-previous;Queries++;
                if(physics.CapsuleCast(previous+Vector3.up*(radius+skin),previous+Vector3.up*(height-radius),radius,segment.normalized,out _,segment.magnitude,Mask,QueryTriggerInteraction.Ignore))return false;
                Queries++;if(physics.OverlapCapsule(point+Vector3.up*(radius+skin),point+Vector3.up*(height-radius),radius,overlaps,Mask,QueryTriggerInteraction.Ignore)>0)return false;
                if(landing)return Locate(point,out var finish)&&finish.Support==start.Support;
                previous=point;
            }
            // Reject trajectories exceeding the configured fixed-step query budget.
            return false;
        }
        public bool Covered(Vector3 feet,Vector3 enemy)
        {
            var delta=enemy+Vector3.up*target-(feet+Vector3.up*eye);Queries++;
            return physics.Raycast(feet+Vector3.up*eye,delta.normalized,out _,delta.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
        }
    }
}
