using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround
{
    /// <summary>Bounded world-body effects. Health/armor notices are read-only; time belongs to the session.</summary>
    public sealed class HitFeedbackPresentation:IDisposable
    {
        sealed class Patch {public GameObject Root;public Mesh Mesh;public int Participant,Life;public double Born;public bool Shield;}
        readonly NativeCombatSession session;readonly GameObject[] bodies;readonly ProvingProfile profile,blood;
        readonly double[] lastBlood,lastShield;readonly List<Patch> patches=new List<Patch>();
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        Material bloodMaterial,shieldMaterial;bool disposed;
        public int BloodMarks {get;private set;}public int ShieldFlashes {get;private set;}
        public HitFeedbackPresentation(NativeCombatSession session,GameObject[] bodies,ProvingProfile tuning,ProvingProfile bloodProfile)
        {
            this.session=session;this.bodies=bodies;profile=tuning.DetachedCopy();blood=bloodProfile.DetachedCopy();
            lastBlood=new double[bodies.Length];lastShield=new double[bodies.Length];
            for(int i=0;i<bodies.Length;i++){lastBlood[i]=lastShield[i]=double.NegativeInfinity;bodies[i].GetComponent<TrooperVisual>()?.ConfigureHitFeedback(profile);}
            session.Damaged+=Observe;session.StateRestored+=Clear;
        }
        public void Observe(DamageNotice notice)
        {
            int p=notice.Participant;
            if(disposed||p<0||p>=bodies.Length||!bodies[p]||!notice.Impact.Valid||notice.HealthLost+notice.ArmorLost<=0)return;
            var life=session.Life(p);if(life.Life!=notice.Life||life.Dead)return;
            bodies[p].GetComponent<TrooperVisual>()?.Hit(notice);
            bool stain=blood.Get("blood.enabled")!=0&&notice.HealthLost>0&&notice.Time-lastBlood[p]>=profile.Get("hit.bloodInterval");
            bool shield=notice.ArmorLost>0&&notice.Time-lastShield[p]>=profile.Get("hit.shieldInterval");
            if(!stain&&!shield)return;
            // Rate-limit the shot, not each pellet: one shot may create several bounded patches.
            bool placed=false;
            if(notice.Contacts!=null&&notice.Contacts.Count>0)
            {
                foreach(var impact in notice.Contacts)placed|=Place(impact);
            }
            else placed=Place(notice.Impact);
            if(!placed)return;
            if(stain)lastBlood[p]=notice.Time;
            if(shield)lastShield[p]=notice.Time;
            bool Place(FatalImpact impact)
            {
                if(!impact.Valid)return false;
                var contact=SkinnedContactPatch.Find(bodies[p],impact.Point,impact.Direction);
                if(contact==null)return false;
                if(stain)Add(contact,notice,false);
                if(shield)Add(contact,notice,true);
                return true;
            }
        }
        void Add(SkinnedContactPatch.Contact contact,DamageNotice notice,bool shield)
        {
            if(!shield)
                while(patches.FindAll(p=>!p.Shield&&p.Participant==notice.Participant).Count>=(int)profile.Get("hit.maxBloodMarksPerParticipant"))
                    Remove(patches.FindIndex(p=>!p.Shield&&p.Participant==notice.Participant));
            int cap=(int)profile.Get(shield?"hit.maxShieldFlashes":"hit.maxBloodMarks");
            while((shield?ShieldFlashes:BloodMarks)>=cap)
            {int oldest=patches.FindIndex(p=>p.Shield==shield);Remove(oldest);}
            var material=Material(shield);
            var root=SkinnedContactPatch.Create(contact,profile.Get(shield?"hit.shieldSize":"hit.bloodSize"),material,
                shield?"hit-contact-shield":"hit-contact-blood",out var mesh);
            if(!root)return;
            patches.Add(new Patch{Root=root,Mesh=mesh,Participant=notice.Participant,Life=notice.Life,Born=notice.Time,Shield=shield});
            if(shield)ShieldFlashes++;else BloodMarks++;
            RenderPatch(patches[patches.Count-1],notice.Time);
        }
        Material Material(bool shield)
        {
            var current=shield?shieldMaterial:bloodMaterial;if(current)return current;
            var shader=Resources.Load<Shader>("HitContact");if(!shader)throw new InvalidOperationException("HitContact shader missing");
            current=new Material(shader){name=shield?"shield-contact-blue":"body-blood-oxblood"};
            current.SetFloat("_Shield",shield?1:0);
            current.SetColor("_Color",shield?new Color(profile.Get("hit.shieldRed"),profile.Get("hit.shieldGreen"),profile.Get("hit.shieldBlue"))*profile.Get("hit.shieldIntensity"):
                new Color(blood.Get("blood.red"),blood.Get("blood.green"),blood.Get("blood.blue"),blood.Get("blood.opacity")));
            // Intensity boosts RGB only. Alpha remains an opacity rather than HDR intensity.
            if(shield){var color=current.GetColor("_Color");color.a=1;current.SetColor("_Color",color);shieldMaterial=current;}else bloodMaterial=current;
            return current;
        }
        void RenderPatch(Patch patch,double time)
        {
            float age=(float)(time-patch.Born),duration=profile.Get(patch.Shield?"hit.shieldSeconds":"hit.bloodSeconds");
            float opacity=patch.Shield?1-Mathf.Clamp01(age/duration):
                1-Mathf.Clamp01((age/duration-(1-profile.Get("hit.bloodFadeFraction")))/profile.Get("hit.bloodFadeFraction"));
            block.Clear();block.SetFloat("_Opacity",opacity);patch.Root.GetComponent<Renderer>().SetPropertyBlock(block);
        }
        public void Render()
        {
            if(disposed)return;
            for(int i=patches.Count-1;i>=0;i--)
            {
                var patch=patches[i];var life=session.Life(patch.Participant);
                if(!patch.Root||life.Life!=patch.Life||life.Dead||session.Time-patch.Born>=profile.Get(patch.Shield?"hit.shieldSeconds":"hit.bloodSeconds")){Remove(i);continue;}
                RenderPatch(patch,session.Time);
            }
        }
        void Remove(int index)
        {
            var patch=patches[index];
            // Detach synchronously before death clones the actor. Deferred Destroy alone would clone owned meshes.
            if(patch.Root){patch.Root.SetActive(false);patch.Root.transform.SetParent(null);Destroy(patch.Root);}
            if(patch.Mesh)Destroy(patch.Mesh);if(patch.Shield)ShieldFlashes--;else BloodMarks--;patches.RemoveAt(index);
        }
        public void ClearParticipant(int participant)
        {
            for(int i=patches.Count-1;i>=0;i--)if(patches[i].Participant==participant)Remove(i);
            lastBlood[participant]=lastShield[participant]=double.NegativeInfinity;bodies[participant].GetComponent<TrooperVisual>()?.ClearHit();
        }
        public void Clear(){for(int i=0;i<bodies.Length;i++)ClearParticipant(i);}
        public void Dispose()
        {if(disposed)return;disposed=true;session.Damaged-=Observe;session.StateRestored-=Clear;Clear();Destroy(bloodMaterial);Destroy(shieldMaterial);}
        static void Destroy(Object item){if(!item)return;if(Application.isPlaying)Object.Destroy(item);else Object.DestroyImmediate(item);}
    }
}
