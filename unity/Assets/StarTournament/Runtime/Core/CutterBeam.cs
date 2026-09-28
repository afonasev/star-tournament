using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public struct CutterContact
    {
        public int Life;
        public double Seconds, DamageRemainder;
    }
    [Serializable] public sealed class CutterBeamState
    {
        public bool Active, Wall;
        public double EmissionSeconds;
        public Vector3 Origin, Endpoint;
        public CutterContact[] Contacts;
        public CutterBeamState Clone()=>new CutterBeamState { Active=Active,Wall=Wall,EmissionSeconds=EmissionSeconds,Origin=Origin,Endpoint=Endpoint,Contacts=(CutterContact[])Contacts.Clone() };
    }
    /// <summary>Serializable contact integrator. Queries supply distances; visuals never supply hits.</summary>
    public sealed class CutterBeam
    {
        readonly double rate,extension,range,tick,damage;
        CutterBeamState state;
        public CutterBeamState Read()=>state.Clone();
        public float Range=>(float)range;
        public CutterBeam(ProvingProfile p,int participants)
        {
            if(p.Validate().Count!=0)throw new ArgumentException("Invalid cutter profile");
            rate=DecimalValue(p.Get("cutter.energyPerSecond"));range=p.Get("cutter.maxRangeMeters");
            // Profile values originate as decimal UI numbers. Remove float representation noise before clock arithmetic.
            extension=DecimalValue(p.Get("cutter.extensionSeconds"));tick=DecimalValue(p.Get("cutter.contactTickSeconds"));
            damage=DecimalValue(p.Get("cutter.referenceDamage"))/DecimalValue(p.Get("cutter.referenceContactSeconds"))*tick;
            state=new CutterBeamState{Contacts=new CutterContact[participants]};
        }
        static double DecimalValue(float f)=>double.Parse(f.ToString("R",System.Globalization.CultureInfo.InvariantCulture),System.Globalization.CultureInfo.InvariantCulture);
        public void Stop()
        { state.Active=false;state.Wall=false;state.EmissionSeconds=0;state.Endpoint=state.Origin;for(int i=0;i<state.Contacts.Length;i++)state.Contacts[i].Seconds=0; }
        public void Reset(){Stop();Array.Clear(state.Contacts,0,state.Contacts.Length);}
        public void ValidateSnapshot(CutterBeamState s)
        {
            if(s==null||s.Contacts==null||s.Contacts.Length!=state.Contacts.Length||!Finite(s.EmissionSeconds)||s.EmissionSeconds<0||!Finite(s.Origin)||!Finite(s.Endpoint)||(!s.Active&&s.EmissionSeconds!=0))throw new ArgumentException("Invalid cutter snapshot");
            foreach(var c in s.Contacts)if(c.Life<0||!Finite(c.Seconds)||c.Seconds<0||c.Seconds>=tick||!Finite(c.DamageRemainder)||c.DamageRemainder<0||c.DamageRemainder>=1)throw new ArgumentException("Invalid cutter contact");
        }
        public void Restore(CutterBeamState s)
        { ValidateSnapshot(s);state=s.Clone();
        }
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(double d)=>!double.IsNaN(d)&&!double.IsInfinity(d);
        public double Advance(double seconds,double energy,Vector3 origin,Vector3 direction,float wallDistance,float[] distances,int[] lives,float multiplier,Action<int,float> apply)
        {
            if(!Finite(seconds)||seconds<0||!Finite(energy)||energy<0)throw new ArgumentException("Invalid beam interval");
            double duration=Math.Min(seconds,energy/rate), before=state.EmissionSeconds;
            if(duration<=0){Stop();return 0;}
            state.Active=true;state.Origin=origin;state.EmissionSeconds+=duration;
            double length=range*(extension<=0?1:Math.Min(1,state.EmissionSeconds/extension));
            state.Wall=wallDistance<=length;state.Endpoint=origin+direction*(float)Math.Min(length,wallDistance);
            for(int i=0;i<state.Contacts.Length;i++)
            {
                var c=state.Contacts[i];if(c.Life!=lives[i])c=new CutterContact{Life=lives[i]};
                double distance=distances[i];
                if(distance<0||distance>wallDistance||distance>length){c.Seconds=0;state.Contacts[i]=c;continue;}
                double arrival=extension<=0?0:distance/range*extension;
                double contact=Math.Max(0,duration-Math.Max(0,arrival-before));
                c.Seconds+=contact;
                // Epsilon only counters floating clock addition, never a gameplay tolerance.
                int ticks=(int)Math.Floor((c.Seconds+1e-9)/tick);
                c.Seconds=Math.Max(0,c.Seconds-ticks*tick);
                if(ticks>0)
                {
                    double exact=c.DamageRemainder+ticks*damage*multiplier;
                    float whole=(float)Math.Floor(exact+1e-9);c.DamageRemainder=Math.Max(0,exact-whole);
                    if(whole>0)apply(i,whole);
                }
                state.Contacts[i]=c;
            }
            return duration*rate;
        }
    }
}
