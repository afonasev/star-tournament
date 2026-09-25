using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public sealed class WeaponModelPresentation : MonoBehaviour
    {
        static readonly Dictionary<string,Material> rifleMaterials=new Dictionary<string,Material>();
        Renderer shotgun;
        GameObject rifle;
        Transform rifleMuzzle;
        public Vector3 RifleMuzzle => rifleMuzzle ? rifleMuzzle.position : transform.position;
        public void Initialize(GameObject prefab,ProvingProfile profile)
        {
            if(!prefab)throw new System.ArgumentException("Rifle GLB missing",nameof(prefab));
            var mount=GetComponentsInChildren<Transform>(true).Single(x=>x.name=="trooper:rig:weapon-mount");
            shotgun=GetComponentsInChildren<Renderer>(true).Single(x=>x.name=="weapon:joined");
            rifle=Object.Instantiate(prefab,mount);rifle.name="automatic-rifle";
            rifle.transform.localPosition=Vector3.zero;rifle.transform.localRotation=Quaternion.identity;
            // The imported grip already carries the authored rig scale; a second correction made the model fill the viewport.
            rifle.transform.localScale=Vector3.one*profile.Get("presentation.rifleModelScale");
            foreach(var item in rifle.GetComponentsInChildren<Transform>(true))item.gameObject.layer=gameObject.layer;
            foreach(var renderer in rifle.GetComponentsInChildren<Renderer>(true))
            {
                if(gameObject.layer>=15)renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var imported=renderer.sharedMaterials;
                for(int i=0;i<imported.Length;i++)imported[i]=RifleMaterial(imported[i]?.name);
                renderer.sharedMaterials=imported;
            }
            rifleMuzzle=rifle.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="rifle-muzzle");
            Show(WeaponId.Rifle);
        }
        public void Show(WeaponId weapon)
        {
            if(shotgun)shotgun.enabled=weapon==WeaponId.Shotgun;
            if(rifle)rifle.SetActive(weapon==WeaponId.Rifle);
        }
        static Material RifleMaterial(string name)
        {
            string key=name ?? "dark";
            if(rifleMaterials.TryGetValue(key,out var cached)&&cached)return cached;
            Color color=key.Contains("lime")?new Color(.56f,.93f,.055f):
                key.Contains("cobalt")?new Color(.035f,.11f,.52f):
                key.Contains("metal")?new Color(.46f,.55f,.64f):new Color(.012f,.022f,.055f);
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material=new Material(shader){name="rifle " + key,color=color};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",key.Contains("lime")?.2f:.55f);
            rifleMaterials[key]=material;return material;
        }
    }
    /// <summary>Reads combat state; never changes movement, hit zones, damage or spawn choice.</summary>
    public sealed class CombatPresentation : System.IDisposable
    {
        sealed class Corpse { public int Participant; public GameObject Root; public double Expires; public TrooperVisual Visual; }
        sealed class PelletEffect { public GameObject Root, Trace; public Vector3 Start, TraceEnd, End, Normal; public double Begins; public bool Contact; public WeaponId Weapon; }
        readonly NativeCombatSession session;
        readonly ProvingProfile movement, tuning;
        readonly Transform owner;
        readonly PhysicsScene physics;
        readonly Camera[] cameras;
        readonly GameObject[] bodies, views;
        readonly NativeMatchComposition composition;
        readonly int[] local;
        int ViewOf(int participant)=>composition!=null?composition.SeatOf(participant):participant;
        readonly Vector3[] deathEye, deathFeet, killerPoint, orbitStart;
        readonly double[] deathTime;
        readonly bool[] tracking;
        bool disposed;
        readonly List<Corpse> corpses = new List<Corpse>();
        readonly List<PelletEffect> pellets = new List<PelletEffect>();
        Material shotMaterial, traceMaterial, rifleTraceMaterial;
        public int ActiveShotEffectCount => pellets.Count;
        public CombatPresentation(NativeCombatSession session, ProvingProfile movement, ProvingProfile tuning,
            Transform owner, PhysicsScene physics, Camera[] cameras, GameObject[] bodies, GameObject[] views, NativeMatchComposition composition=null)
        {
            if(bodies.Length!=session.ParticipantCount || views.Length!=cameras.Length ||
                (composition==null?cameras.Length!=session.ParticipantCount:composition.ParticipantCount!=session.ParticipantCount || composition.LocalCount!=cameras.Length))
                throw new System.ArgumentException("Presentation ownership mismatch");
            this.composition=composition;local=composition?.Read().LocalParticipants ?? Enumerable.Range(0,cameras.Length).ToArray();
            this.session=session; this.movement=movement; this.tuning=tuning; this.owner=owner; this.physics=physics;
            this.cameras=cameras; this.bodies=bodies; this.views=views;
            deathEye=new Vector3[bodies.Length]; deathFeet=new Vector3[bodies.Length]; killerPoint=new Vector3[bodies.Length]; orbitStart=new Vector3[bodies.Length];
            deathTime=new double[bodies.Length]; tracking=new bool[bodies.Length];
            session.Died += OnDeath;
            session.Respawned += OnRespawn;
            session.Fired += OnFire;
            session.ShotResolved += OnShotResolved;
        }
        void OnFire(int seat,float damage)
        {
            bodies[seat].GetComponent<TrooperVisual>()?.Fire(session.Time);
            if(session.Life(seat).SelectedWeapon==WeaponId.Shotgun)bodies[seat].GetComponent<VectorShotPresentation>()?.Fire(session.Time);
            int view=ViewOf(seat);if(view>=0){views[view].GetComponent<TrooperVisual>()?.Fire(session.Time);if(session.Life(seat).SelectedWeapon==WeaponId.Shotgun)views[view].GetComponent<VectorShotPresentation>()?.Fire(session.Time);}
        }
        void OnShotResolved(ShotNotice notice)
        {
            int capacity=(int)movement.Get("presentation.shotMaxEffects");
            foreach(var pellet in notice.Pellets)
            {
                if(pellets.Count>=capacity) break;
                var root=new GameObject("shot-pellet-"+notice.Sequence+"-"+pellets.Count);root.transform.SetParent(owner,false);
                var trace=GameObject.CreatePrimitive(PrimitiveType.Capsule);trace.name="pellet-streak";trace.transform.SetParent(root.transform,false);
                var collider=trace.GetComponent<Collider>();if(collider)Object.Destroy(collider);
                trace.GetComponent<Renderer>().material=TraceMaterial(notice.Weapon);
                var muzzle=Muzzle(notice.Shooter,pellets.Count,notice.Weapon);
                var clearance=Mathf.Min(movement.Get("presentation.shotMuzzleClearance"),Vector3.Distance(muzzle,pellet.Endpoint)*.5f);
                var start=muzzle+pellet.Direction*clearance;
                trace.transform.position=start;
                var traceEnd=notice.Weapon==WeaponId.Rifle?start+pellet.Direction*Mathf.Min(movement.Get("presentation.rifleTracerLength"),Vector3.Distance(start,pellet.Endpoint)):pellet.Endpoint;
                // Rifle presentation is tracer only: no green sphere at muzzle or endpoint.
                bool contact=notice.Weapon!=WeaponId.Rifle && pellet.Contact!=PelletContact.Miss;
                // Multiple hitscan pellets commonly share one small hit volume: render one readable contact flash,
                // not a stack of identical spheres. The individual flight paths remain visible.
                if(contact)
                    foreach(var existing in pellets)
                        if(existing.Contact && Vector3.Distance(existing.End,pellet.Endpoint)<movement.Get("presentation.shotImpactSize")*2f) { contact=false;break; }
                pellets.Add(new PelletEffect{Root=root,Trace=trace,Start=start,TraceEnd=traceEnd,End=pellet.Endpoint,Normal=pellet.Normal,Begins=notice.Time,Contact=contact,Weapon=notice.Weapon});
            }
        }
        Material ShotMaterial()
        {
            if(shotMaterial)return shotMaterial;
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            shotMaterial=new Material(shader){color=new Color(1f,.72f,.08f,1f)};
            if(shotMaterial.HasProperty("_BaseColor"))shotMaterial.SetColor("_BaseColor",new Color(1f,.72f,.08f,1f));
            if(shotMaterial.HasProperty("_EmissionColor")){shotMaterial.EnableKeyword("_EMISSION");shotMaterial.SetColor("_EmissionColor",new Color(1f,.45f,.02f,1f));}
            return shotMaterial;
        }
        Material TraceMaterial(WeaponId weapon)
        {
            if(weapon==WeaponId.Rifle)
            {
                if(rifleTraceMaterial)return rifleTraceMaterial;
                var rifleShader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                var color=new Color(movement.Get("presentation.rifleTracerRed"),movement.Get("presentation.rifleTracerGreen"),movement.Get("presentation.rifleTracerBlue"),1f);
                rifleTraceMaterial=new Material(rifleShader){color=color};
                if(rifleTraceMaterial.HasProperty("_BaseColor"))rifleTraceMaterial.SetColor("_BaseColor",color);
                return rifleTraceMaterial;
            }
            if(traceMaterial)return traceMaterial;
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            traceMaterial=new Material(shader){color=new Color(1f,.78f,.4f,1f)};
            if(traceMaterial.HasProperty("_BaseColor"))traceMaterial.SetColor("_BaseColor",new Color(1f,.78f,.4f,1f));
            if(traceMaterial.HasProperty("_EmissionColor")){traceMaterial.EnableKeyword("_EMISSION");traceMaterial.SetColor("_EmissionColor",new Color(1f,.55f,.15f,1f));}
            return traceMaterial;
        }
        Vector3 Muzzle(int participant,int pellet,WeaponId weapon)
        {
            int view=ViewOf(participant);
            if(weapon==WeaponId.Rifle)
            {
                var rifle=(view>=0?views[view]:bodies[participant]).GetComponent<WeaponModelPresentation>();
                if(rifle)return rifle.RifleMuzzle;
            }
            var visual=(view>=0?views[view]:bodies[participant]).GetComponent<VectorShotPresentation>();
            return visual!=null && visual.MuzzleCount==2?visual.Muzzle(pellet):bodies[participant].transform.position+Vector3.up*movement.Get("camera.eyeHeight");
        }

        void RenderPellets()
        {
            float flight=movement.Get("presentation.shotFlightSeconds"), impact=movement.Get("presentation.shotImpactSeconds");
            for(int i=pellets.Count-1;i>=0;i--)
            {
                var effect=pellets[i];float elapsed=(float)(session.Time-effect.Begins);
                if(elapsed<flight)
                {
                    float progress=Mathf.Clamp01(elapsed/flight);var head=Vector3.Lerp(effect.Start,effect.TraceEnd,progress);
                    var tail=Vector3.Lerp(effect.Start,effect.TraceEnd,Mathf.Max(0,progress-.18f));
                    Vector3 delta=head-tail;effect.Trace.transform.SetPositionAndRotation((head+tail)*.5f,Quaternion.FromToRotation(Vector3.up,delta.normalized));
                    float width=movement.Get(effect.Weapon==WeaponId.Rifle?"presentation.rifleTracerWidth":"presentation.shotTracerWidth");effect.Trace.transform.localScale=new Vector3(width,delta.magnitude*.5f,width);continue;
                }
                if(effect.Trace){Object.Destroy(effect.Trace);effect.Trace=null;if(effect.Contact)CreateImpact(effect);}
                if(elapsed>=flight+impact){Object.Destroy(effect.Root);pellets.RemoveAt(i);}
            }
        }
        void CreateImpact(PelletEffect effect)
        {
            var impact=GameObject.CreatePrimitive(PrimitiveType.Sphere);impact.name="shot-impact";impact.transform.SetParent(effect.Root.transform,false);
            impact.transform.position=effect.End+effect.Normal*.01f;impact.transform.localScale=Vector3.one*movement.Get("presentation.shotImpactSize");
            var collider=impact.GetComponent<Collider>();if(collider)Object.Destroy(collider);
            var renderer=impact.GetComponent<Renderer>();renderer.material=effect.Weapon==WeaponId.Rifle?TraceMaterial(WeaponId.Rifle):ShotMaterial();
        }
        void OnRespawn(int seat)
        {
            foreach(var corpse in corpses)if(corpse.Participant==seat&&corpse.Root)
                foreach(var t in corpse.Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;
            bodies[seat].SetActive(true);int view=ViewOf(seat);if(view>=0)views[view].SetActive(true);
            bodies[seat].GetComponent<TrooperVisual>()?.ResetPose(session.Time);
            if(view>=0)views[view].GetComponent<TrooperVisual>()?.ResetPose(session.Time);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            session.Died-=OnDeath; session.Respawned-=OnRespawn; session.Fired-=OnFire;session.ShotResolved-=OnShotResolved;
            foreach(var corpse in corpses) if(corpse.Root) { corpse.Root.SetActive(false); Object.Destroy(corpse.Root); }
            corpses.Clear();
            foreach(var pellet in pellets)if(pellet.Root)Object.Destroy(pellet.Root);
            pellets.Clear();if(shotMaterial)Object.Destroy(shotMaterial);if(traceMaterial)Object.Destroy(traceMaterial);if(rifleTraceMaterial)Object.Destroy(rifleTraceMaterial);
            foreach(var body in bodies)if(body)body.SetActive(true);
            foreach(var view in views)if(view)view.SetActive(true);
        }
        void OnDeath(DeathNotice notice)
        {
            int seat=notice.Seat;
            deathFeet[seat]=notice.Pose.Position;
            deathEye[seat]=notice.Pose.Position+Vector3.up*movement.Get("camera.eyeHeight");
            deathTime[seat]=session.Time; tracking[seat]=true;
            killerPoint[seat]=deathEye[seat]+Quaternion.Euler(notice.Pose.Pitch,notice.Pose.Yaw,0)*Vector3.forward;
            var corpse=Object.Instantiate(bodies[seat],owner);
            foreach(var particles in corpse.GetComponentsInChildren<ParticleSystem>())particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            // Unity object cloning is not a contract for copying renderer property blocks.
            var sourceRenderers=bodies[seat].GetComponentsInChildren<Renderer>(true);
            var corpseRenderers=corpse.GetComponentsInChildren<Renderer>(true);
            for(int r=0;r<sourceRenderers.Length;r++)
                for(int m=0;m<sourceRenderers[r].sharedMaterials.Length;m++)
                {
                    var block=new MaterialPropertyBlock();sourceRenderers[r].GetPropertyBlock(block,m);
                    corpseRenderers[r].SetPropertyBlock(block,m);
                }
            corpse.name=$"corpse-{seat+1}-life-{notice.Life.Life}";
            corpse.SetActive(true);
            // The fixed death-eye camera is inside its own corpse. Hide that corpse only from
            // the owner during killer tracking; no-killer orbit and other views still see it.
            // Owner body layers 11–14 are a renderer routing invariant shared with ProvingGround.
            int corpseLayer=notice.Life.KillerId!=null&&ViewOf(seat)>=0?11+ViewOf(seat):0;
            foreach(var t in corpse.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=corpseLayer;
            foreach(var collider in corpse.GetComponentsInChildren<Collider>(true)) { collider.enabled=false; Object.Destroy(collider); }
            var source=bodies[seat].GetComponent<TrooperVisual>();
            var visual=corpse.GetComponent<TrooperVisual>();
            if(visual)
            {
                visual.Initialize(source.Clips,source.Tuning); visual.BeginDeath(session.Time);
                corpse.transform.SetPositionAndRotation(notice.Pose.Position,Quaternion.Euler(0,notice.Pose.Yaw,0));
            }
            else corpse.transform.SetPositionAndRotation(notice.Pose.Position+Vector3.up*tuning.Get("death.bodyHeight"),Quaternion.Euler(90,notice.Pose.Yaw,0));
            // Presentation-only support projection also handles airborne deaths; it never moves the motor.
            Vector3 supportOrigin=notice.Pose.Position+Vector3.up*movement.Get("camera.eyeHeight");
            float supportRange=Mathf.Max(movement.Get("player.capsule.height"),supportOrigin.y-movement.Get("world.minimumSupportHeight")+movement.Get("player.capsule.height"));
            if(physics.Raycast(supportOrigin,Vector3.down,out var support,supportRange,
                (1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer),QueryTriggerInteraction.Ignore))
            {
                var renderers=corpse.GetComponentsInChildren<Renderer>();
                if(visual) corpse.transform.position=support.point;
                else if(renderers.Length>0)
                {
                    var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    corpse.transform.position+=Vector3.up*(support.point.y-bounds.min.y);
                }
                deathFeet[seat]=support.point;
            }
            corpses.Add(new Corpse { Participant=seat, Root=corpse, Visual=visual, Expires=session.Time+tuning.Get("death.corpseSeconds") });
            bodies[seat].SetActive(false);int view=ViewOf(seat);if(view>=0)views[view].SetActive(false);
            UpdateKiller(seat); // Capture killer life immediately, even if it dies later in this tick.
            orbitStart[seat]=deathEye[seat]-killerPoint[seat];orbitStart[seat].y=0;
            if(orbitStart[seat].sqrMagnitude<.0001f)orbitStart[seat]=Quaternion.Euler(0,notice.Pose.Yaw,0)*Vector3.back;
            orbitStart[seat].Normalize();
        }
        void UpdateKiller(int seat)
        {
            if (!tracking[seat]) return;
            var dead=session.Life(seat);
            for(int i=0;i<session.ParticipantCount;i++)
            {
                var killer=session.Life(i);
                if(killer.ParticipantId!=dead.KillerId) continue;
                if(killer.Life!=dead.KillerLife) { tracking[seat]=false; return; }
                killerPoint[seat]=session.Pose(i).Position+Vector3.up*movement.Get("camera.eyeHeight");
                if(killer.Dead) tracking[seat]=false;
                return;
            }
        }
        public string DeathMessage(int seat)
        {
            var life=session.Life(seat);
            if(!life.Dead) return "";
            string killer="Вы погибли";
            if(life.KillerId!=null)
                for(int p=0;p<session.ParticipantCount;p++)if(session.Life(p).ParticipantId==life.KillerId)
                    { killer="Вас убил "+(composition?.Participant(p).Name ?? ("Игрок "+(p+1)));break; }
            return killer+"\nВозрождение через "+Mathf.CeilToInt((float)life.RespawnRemaining);
        }
        public void Render()
        {
            RenderPellets();
            foreach(var body in bodies)if(body)body.GetComponent<VectorShotPresentation>()?.Render(session.Time);
            foreach(var view in views)if(view)view.GetComponent<VectorShotPresentation>()?.Render(session.Time);
            for(int c=corpses.Count-1;c>=0;c--)
            {
                if(session.Time>=corpses[c].Expires) { Object.Destroy(corpses[c].Root); corpses.RemoveAt(c); }
                else corpses[c].Visual?.Render(session.Time,Vector3.zero,0,0);
            }
            for(int p=0;p<session.ParticipantCount;p++)if(!session.Life(p).Dead)
                bodies[p].GetComponent<TrooperVisual>()?.Render(session.Time,session.Pose(p).Velocity,session.Life(p).Health,session.Life(p).Life);
            for(int v=0;v<cameras.Length;v++)
            {
                int i=local[v];var life=session.Life(i); var pose=session.Pose(i);
                if(!life.Dead)
                {
                    views[v].GetComponent<TrooperVisual>()?.Render(session.Time,pose.Velocity,life.Health,life.Life);
                    cameras[v].transform.SetPositionAndRotation(pose.Position+Vector3.up*movement.Get("camera.eyeHeight"),Quaternion.Euler(pose.Pitch,pose.Yaw,0));
                    continue;
                }
                Vector3 position=deathEye[i], target;
                if(life.KillerId!=null)
                {
                    UpdateKiller(i); target=killerPoint[i];
                    float elapsed=(float)(session.Time-deathTime[i]);
                    float transition=tuning.Get("killcam.killerTransitionSeconds");
                    float progress=Mathf.Clamp01(elapsed/transition);
                    progress=progress*progress*(3f-2f*progress);
                    float angle=elapsed*tuning.Get("killcam.killerOrbitSpeed");
                    Vector3 radial=Quaternion.Euler(0,angle,0)*orbitStart[i];
                    Vector3 desired=target+radial*tuning.Get("killcam.killerOrbitRadius")+Vector3.up*tuning.Get("killcam.killerAboveEyes");
                    position=Vector3.Lerp(deathEye[i],desired,progress);
                    Vector3 offset=position-target;
                    float radius=tuning.Get("killcam.clipRadius");
                    if(offset.sqrMagnitude>0 && physics.SphereCast(target,radius,offset.normalized,out var wall,offset.magnitude,
                        1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore))
                        position=target+offset.normalized*Mathf.Max(radius,wall.distance-radius);
                }
                else
                {
                    target=deathFeet[i]+Vector3.up*tuning.Get("death.bodyHeight");
                    float angle=pose.Yaw+(float)(session.Time-deathTime[i])*tuning.Get("killcam.orbitSpeed");
                    Vector3 offset=Quaternion.Euler(0,angle,0)*Vector3.back*tuning.Get("killcam.orbitRadius")+Vector3.up*tuning.Get("killcam.orbitHeight");
                    float radius=tuning.Get("killcam.clipRadius");
                    position=target+offset;
                    if(physics.SphereCast(target,radius,offset.normalized,out var wall,offset.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore))
                        position=target+offset.normalized*Mathf.Max(radius,wall.distance-radius);
                }
                Vector3 direction=target-position;
                if(direction.sqrMagnitude>0) cameras[v].transform.SetPositionAndRotation(position,Quaternion.LookRotation(direction));
            }
        }
    }
}
