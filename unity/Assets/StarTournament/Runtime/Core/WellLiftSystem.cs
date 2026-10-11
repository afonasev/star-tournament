using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public enum WellLiftPhase { Ready, Boarding, Moving, AwaitEmpty }
    [Serializable] public sealed class WellLiftState { public string Id; public float Height, Target; public double Countdown; public WellLiftPhase Phase; }
    /// <summary>Tick-owned state. Collider objects only project this state, never advance its clocks.</summary>
    public sealed class WellLiftSystem
    {
        readonly ArenaLiftDefinition[] definitions;readonly WellLiftState[] states;readonly Dictionary<string,Collider> colliders=new Dictionary<string,Collider>();
        readonly float speed,tolerance;
        public WellLiftSystem(ArenaLiftDefinition[] definitions,ProvingProfile profile)
        {
            this.definitions=definitions;speed=profile.Get("well.liftSpeed");tolerance=profile.Get("well.boardingTolerance");
            states=definitions.Select(d=>new WellLiftState{Id=d.Id,Height=d.InitialHeight,Target=d.InitialHeight,Phase=WellLiftPhase.Ready}).ToArray();
        }
        public ArenaLiftDefinition Definition(string id)=>definitions.Single(d=>d.Id==id);
        public WellLiftState[] Capture()=>states.Select(Copy).ToArray();
        static WellLiftState Copy(WellLiftState s)=>new WellLiftState{Id=s.Id,Height=s.Height,Target=s.Target,Countdown=s.Countdown,Phase=s.Phase};
        public void Reset(){for(int i=0;i<states.Length;i++)states[i]=new WellLiftState{Id=definitions[i].Id,Height=definitions[i].InitialHeight,Target=definitions[i].InitialHeight,Phase=WellLiftPhase.Ready};Project();}
        public void Attach(string id,Collider collider){colliders.Add(id,collider);Project();}
        public bool OnPlatform(string id,ParticipantState pose)
        {
            var d=Definition(id);var s=states.Single(x=>x.Id==id);
            return Math.Abs(pose.Position.x-d.Center.x)<d.Size.x*.5f&&Math.Abs(pose.Position.z-d.Center.z)<d.Size.z*.5f&&Math.Abs(pose.Position.y-s.Height)<=tolerance&&(pose.Grounded||pose.Velocity.y<=0);
        }
        public bool Supports(ParticipantState pose)=>definitions.Any(d=>OnPlatform(d.Id,pose));
        public void Validate(WellLiftState[] snapshot)
        {
            if(snapshot==null||snapshot.Length!=states.Length||snapshot.Any(s=>s==null)||snapshot.Select(s=>s.Id).Distinct().Count()!=states.Length)throw new ArgumentException("Invalid lift set");
            foreach(var d in definitions)
            {
                var s=snapshot.SingleOrDefault(x=>x.Id==d.Id);
                if(s==null||!Finite(s.Height)||!Finite(s.Target)||double.IsNaN(s.Countdown)||double.IsInfinity(s.Countdown)||s.Height<d.Bottom||s.Height>d.Top||(s.Target!=d.Bottom&&s.Target!=d.Top)||s.Countdown<0||s.Countdown>1||!Enum.IsDefined(typeof(WellLiftPhase),s.Phase))throw new ArgumentException("Invalid lift state");
                if(s.Phase!=WellLiftPhase.Moving&&s.Height!=d.Bottom&&s.Height!=d.Top)throw new ArgumentException("Stationary lift between storeys");
                if(s.Phase!=WellLiftPhase.Boarding&&s.Countdown!=0)throw new ArgumentException("Invalid lift countdown");
                if(s.Phase==WellLiftPhase.Boarding&&(s.Target==s.Height||s.Countdown==0)||s.Phase==WellLiftPhase.Moving&&s.Target==s.Height||(s.Phase==WellLiftPhase.AwaitEmpty||s.Phase==WellLiftPhase.Ready)&&s.Target!=s.Height)throw new ArgumentException("Invalid lift phase/target");
            }
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        public void Restore(WellLiftState[] snapshot){Validate(snapshot);for(int i=0;i<states.Length;i++)states[i]=Copy(snapshot.Single(x=>x.Id==states[i].Id));Project();}
        public void Tick(float seconds,CharacterMotor[] motors,Func<int,bool> alive)
        {
            Advance(seconds,definitions.Select(d=>Enumerable.Range(0,motors.Length).Where(i=>alive(i)&&OnPlatform(d.Id,motors[i].State)).ToArray()).ToArray(),(lift,riders,delta)=>
            {
                // Remove only the moving deck during carry: walls/ceilings still constrain CharacterController.Move.
                var collider=colliders[definitions[lift].Id];collider.enabled=false;
                try{foreach(int i in riders)motors[i].CarryPlatform(Vector3.up*delta);}finally{collider.enabled=true;}
            });
        }
        // Explicit occupancy input also makes the state machine independently testable without renderer objects.
        public void Advance(float seconds,int[][] riders,Action<int,int[],float> carry=null)
        {
            if(!Finite(seconds)||seconds<=0||riders.Length!=states.Length)throw new ArgumentException("Invalid lift tick");
            for(int i=0;i<states.Length;i++)
            {
                var s=states[i];var d=definitions[i];double remaining=seconds;
                if(s.Phase==WellLiftPhase.AwaitEmpty){if(riders[i].Length==0)s.Phase=WellLiftPhase.Ready;continue;}
                if(s.Phase==WellLiftPhase.Ready)
                {
                    if(riders[i].Length==0)continue;
                    s.Target=s.Height==d.Bottom?d.Top:d.Bottom;s.Phase=WellLiftPhase.Boarding;s.Countdown=1; // Approved exact rule.
                }
                if(s.Phase==WellLiftPhase.Boarding)
                {
                    double used=Math.Min(remaining,s.Countdown);s.Countdown-=used;remaining-=used;
                    if(s.Countdown>1e-7)continue;s.Countdown=0;s.Phase=WellLiftPhase.Moving;
                }
                if(s.Phase==WellLiftPhase.Moving)
                {
                    float before=s.Height;s.Height=Mathf.MoveTowards(s.Height,s.Target,speed*(float)remaining);
                    carry?.Invoke(i,riders[i],s.Height-before);
                    if(s.Height==s.Target)s.Phase=riders[i].Length==0?WellLiftPhase.Ready:WellLiftPhase.AwaitEmpty;
                }
            }
            Project();
        }
        void Project(){foreach(var s in states)if(colliders.TryGetValue(s.Id,out var c))c.transform.position=Definition(s.Id).Center+Vector3.up*(s.Height-Definition(s.Id).Size.y*.5f);Physics.SyncTransforms();}
    }
}
