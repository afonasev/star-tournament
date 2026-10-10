using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Read-only combat subscriber. Death facts are evaluated after the entire combat tick.</summary>
    public sealed class NativeBotBanter : IDisposable
    {
        readonly NativeCombatSession session;
        readonly NativeMatchComposition composition;
        readonly PhysicsScene physics;
        readonly float eyeHeight,maximumHealth;
        readonly List<DeathNotice> deaths=new List<DeathNotice>();
        public BotBanter Banter { get; }
        public NativeBotBanter(NativeCombatSession session,NativeMatchComposition composition,PhysicsScene physics,
            float eyeHeight,float maximumHealth,int seed=0,BotBanterPolicy policy=null)
        {
            this.session=session;this.composition=composition;this.physics=physics;
            this.eyeHeight=eyeHeight;this.maximumHealth=maximumHealth;
            Banter=new BotBanter(composition,session.LifeStates,seed,policy);
            session.AttackEmitted+=Attack;session.Damaged+=Damage;session.Died+=Death;
            session.Respawned+=Respawn;session.StateRestored+=Restore;session.TickCompleted+=Flush;
        }
        void Attack(int shooter,int life,WeaponId weapon,double time,Vector3 origin,Vector3 direction,float range)
        {
            int target=-1;float nearest=float.PositiveInfinity;
            for(int p=0;p<session.ParticipantCount;p++)
            {
                if(p==shooter||session.Life(p).Dead||composition.Roster.AreAllies(shooter,p))continue;
                var delta=session.Pose(p).Position+Vector3.up*eyeHeight-origin;float distance=delta.magnitude;
                // A narrow visible cone is evidence of aimed fire, never just proximity or an AI target.
                if(distance<=0||distance>range||distance>=nearest||Vector3.Dot(direction,delta/distance)<.99f)continue;
                if(physics.Raycast(origin,delta/distance,out _,distance,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore))continue;
                nearest=distance;target=p;
            }
            Banter.Attack(shooter,life,target,target<0?0:session.Life(target).Life,time);
        }
        void Damage(DamageNotice n)=>Banter.Damage(n.Participant,n.Life,n.Shooter,n.ShooterLife,n.HealthLost,n.ArmorLost,n.Time);
        void Death(DeathNotice n)=>deaths.Add(n);
        void Respawn(int p)=>Banter.Respawn(p,session.Life(p).Life);
        void Restore(){deaths.Clear();Banter.Reset(session.LifeStates,session.Time);}
        int Index(string id)
        {
            for(int p=0;p<session.ParticipantCount;p++)if(session.Life(p).ParticipantId==id)return p;
            return -1;
        }
        public void Flush()
        {
            // Public for manual simulation/review callers; normal gameplay uses TickCompleted.
            var lives=session.LifeStates;
            foreach(var n in deaths)
            {
                int killer=n.Life.KillerId==null?-1:Index(n.Life.KillerId);
                bool pending=session.RifleBullets.Any(b=>b.Owner==n.Seat&&b.OwnerLife==n.Life.Life||b.Owner==killer&&b.OwnerLife==n.Life.KillerLife)||
                    session.Rockets.Any(r=>r.Owner==n.Seat&&r.OwnerLife==n.Life.Life||r.Owner==killer&&r.OwnerLife==n.Life.KillerLife);
                Banter.Death(n.Seat,killer,n.Life.KillerLife,n.Impact.Valid?n.Impact.Weapon:WeaponId.Rifle,lives,maximumHealth,session.Time,pending);
            }
            deaths.Clear();
        }
        public void Dispose()
        {
            session.AttackEmitted-=Attack;session.Damaged-=Damage;session.Died-=Death;
            session.Respawned-=Respawn;session.StateRestored-=Restore;session.TickCompleted-=Flush;deaths.Clear();
        }
    }
}
