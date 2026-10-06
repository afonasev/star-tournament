using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public enum NativeBotPickupKind { Heal, Armor, Speed, Damage, Weapon }
    // Deliberately excludes collector, remaining timer and all participant state.
    [Serializable] public struct NativeBotPickupEvent
    { public string Id; public NativeBotPickupKind Kind; public WeaponId Weapon; public Vector3 Position; public bool Available; public int Revision; public double OccurredAt; }
    [Serializable] public struct NativeBotPickupDelivery
    { public int Receiver,Life; public double DeliverAt; public NativeBotPickupEvent Event; }
    [Serializable] public sealed class NativeBotPickupObserver
    { public int Life; public bool Alive; public NativeBotPickupEvent[] Known=Array.Empty<NativeBotPickupEvent>(); }
    [Serializable] public sealed class NativeBotPickupSnapshot
    { public int Version=1; public string Configuration; public double Time=-1; public NativeBotPickupEvent[] Source; public NativeBotPickupObserver[] Observers; public NativeBotPickupDelivery[] Pending; }
    public sealed class NativeBotPickupKnowledge
    {
        readonly float[] delay; readonly string configuration;
        NativeBotPickupEvent[] source=Array.Empty<NativeBotPickupEvent>();
        NativeBotPickupObserver[] observers; readonly List<NativeBotPickupDelivery> pending=new List<NativeBotPickupDelivery>();
        public double Time {get;private set;}=-1;
        public NativeBotPickupKnowledge(NativeBotDifficulty[] difficulties,ProvingProfile profile)
        {
            var canonical=ProvingProfile.CreateBotPerceptionDefault();
            if(difficulties==null||difficulties.Any(d=>!Enum.IsDefined(typeof(NativeBotDifficulty),d))||profile==null||profile.Id!=canonical.Id||profile.Version!=canonical.Version||profile.Validate().Count!=0)throw new ArgumentException("Invalid pickup configuration");
            foreach(var d in canonical.Descriptors)if(!d.Contains(profile.Get(d.Path)))throw new ArgumentException("Invalid perception value: "+d.Path);
            delay=difficulties.Select(d=>profile.Get("bots."+d.ToString().ToLowerInvariant()+".pickupDelaySeconds")).ToArray();
            if(delay.Any(d=>d<=0||!Finite(d)))throw new ArgumentException("Positive pickup delay required");
            configuration=JsonUtility.ToJson(profile)+"|"+string.Join(",",difficulties.Select(d=>(int)d));
            observers=delay.Select(_=>new NativeBotPickupObserver()).ToArray();
        }
        public NativeBotPickupEvent[] Read(int receiver)=>(NativeBotPickupEvent[])observers[receiver].Known.Clone();
        static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        static bool Valid(NativeBotPickupEvent e)=>!string.IsNullOrWhiteSpace(e.Id)&&Enum.IsDefined(typeof(NativeBotPickupKind),e.Kind)&&Finite(e.Position.x)&&Finite(e.Position.y)&&Finite(e.Position.z)&&Finite(e.OccurredAt)&&e.OccurredAt>=0&&e.Revision>0&&(e.Kind!=NativeBotPickupKind.Weapon||e.Weapon==WeaponId.Shotgun||e.Weapon==WeaponId.RocketLauncher||e.Weapon==WeaponId.Cutter);
        public void Sample(double time,NativeBotObservationFrame[] frames,NativeBotPickupEvent[] world)
        {
            if(!Finite(time)||time<0||time<=Time||frames==null||frames.Length!=observers.Length||world==null||world.Select(e=>e.Id).Distinct().Count()!=world.Length||world.Any(e=>string.IsNullOrWhiteSpace(e.Id)||!Finite(e.Position.x)||!Finite(e.Position.y)||!Finite(e.Position.z))||frames.Any(f=>f==null||f.OwnLife<1))throw new ArgumentException("Invalid pickup sample");
            var changed=new List<NativeBotPickupEvent>();var next=new List<NativeBotPickupEvent>();
            foreach(var raw in world)
            {
                int old=Array.FindIndex(source,e=>e.Id==raw.Id);var value=old<0?raw:source[old];
                if(old>=0&&(value.Kind!=raw.Kind||value.Position!=raw.Position||value.Weapon!=raw.Weapon))throw new ArgumentException("Pickup identity changed");
                if(old<0||value.Available!=raw.Available){value=raw;value.Revision=old<0?1:source[old].Revision+1;value.OccurredAt=time;changed.Add(value);}
                if(!Valid(value))throw new ArgumentException("Invalid pickup event");next.Add(value);
            }
            if(source.Any(e=>!world.Any(w=>w.Id==e.Id)))throw new ArgumentException("Static pickup catalogue disappeared");
            Time=time;source=next.OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
            for(int r=0;r<observers.Length;r++)
            {
                var o=observers[r];bool reset=o.Life!=frames[r].OwnLife||!o.Alive||!frames[r].Alive;
                if(reset){o.Known=Array.Empty<NativeBotPickupEvent>();pending.RemoveAll(p=>p.Receiver==r);}
                o.Life=frames[r].OwnLife;o.Alive=frames[r].Alive;if(!o.Alive)continue;
                foreach(var e in reset?source:changed.ToArray())pending.Add(new NativeBotPickupDelivery{Receiver=r,Life=o.Life,Event=e,DeliverAt=time+delay[r]});
            }
            foreach(var p in pending.Where(p=>p.DeliverAt<=time).OrderBy(p=>p.Event.OccurredAt).ThenBy(p=>p.Event.Revision))
            {
                var o=observers[p.Receiver];if(!o.Alive||o.Life!=p.Life)continue;
                var list=o.Known.ToList();int i=list.FindIndex(e=>e.Id==p.Event.Id);
                if(i<0)list.Add(p.Event);else if(list[i].Revision<p.Event.Revision)list[i]=p.Event;
                o.Known=list.OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
            }
            pending.RemoveAll(p=>p.DeliverAt<=time);
        }
        public NativeBotPickupSnapshot Capture()=>Copy(new NativeBotPickupSnapshot{Configuration=configuration,Time=Time,Source=source,Observers=observers,Pending=pending.ToArray()});
        static NativeBotPickupSnapshot Copy(NativeBotPickupSnapshot s)=>JsonUtility.FromJson<NativeBotPickupSnapshot>(JsonUtility.ToJson(s));
        public void ValidateSnapshot(NativeBotPickupSnapshot s)
        {
            if(s==null||s.Version!=1||s.Configuration!=configuration||!Finite(s.Time)||s.Time< -1||s.Source==null||s.Observers==null||s.Observers.Length!=observers.Length||s.Pending==null||s.Source.Any(e=>!Valid(e)||e.OccurredAt>s.Time)||s.Source.Select(e=>e.Id).Distinct().Count()!=s.Source.Length)throw new ArgumentException("Invalid pickup snapshot");
            bool EventValid(NativeBotPickupEvent e)=>Valid(e)&&e.OccurredAt<=s.Time&&s.Source.Any(x=>x.Id==e.Id&&x.Kind==e.Kind&&x.Position==e.Position&&x.Weapon==e.Weapon&&x.Revision>=e.Revision&&(x.Revision!=e.Revision||(x.Available==e.Available&&x.OccurredAt==e.OccurredAt)));
            for(int i=0;i<s.Observers.Length;i++){var o=s.Observers[i];if(o==null||o.Known==null||o.Life<0||!o.Alive&&o.Known.Length>0||o.Known.Any(e=>!EventValid(e))||o.Known.Select(e=>e.Id).Distinct().Count()!=o.Known.Length)throw new ArgumentException("Invalid pickup observer");}
            if(s.Pending.Any(p=>p.Receiver<0||p.Receiver>=delay.Length||!s.Observers[p.Receiver].Alive||p.Life!=s.Observers[p.Receiver].Life||!EventValid(p.Event)||!Finite(p.DeliverAt)||p.DeliverAt<=s.Time||p.DeliverAt>s.Time+delay[p.Receiver]+.00001))throw new ArgumentException("Invalid pickup delivery");
        }
        public void Restore(NativeBotPickupSnapshot s)
        {
            ValidateSnapshot(s);
            var copy=Copy(s);Time=copy.Time;source=copy.Source;observers=copy.Observers;pending.Clear();pending.AddRange(copy.Pending);
        }
    }
}
