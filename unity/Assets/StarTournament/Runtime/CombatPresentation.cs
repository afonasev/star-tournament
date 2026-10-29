using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public sealed class WeaponModelPresentation : MonoBehaviour
    {
        static readonly Dictionary<string,Material> rifleMaterials=new Dictionary<string,Material>();
        Renderer shotgun;
        public ShotgunAmmoDisplay ShotgunDisplay {get;private set;}
        public ForgeAmmoDisplay ForgeDisplay {get;private set;}
        public PulseAmmoDisplay PulseDisplay {get;private set;}
        public RifleAmmoDisplay RifleDisplay {get;private set;}
        Renderer batteryRenderer;
        Material batteryMaterial;
        public float CutterChargeFraction {get;private set;}=0;
        GameObject rifle,pulse,cutter,plasma;
        Transform rifleMuzzle,cutterMuzzle,pulseMuzzle,plasmaMuzzle;
        public float PulseGripScale { get; private set; }=1;
        public float PlasmaViewScale { get; private set; }=1;
        public Vector3 PlasmaMuzzle=>plasmaMuzzle?plasmaMuzzle.position:transform.position;
        public Vector3 PulseMuzzle=>pulseMuzzle?pulseMuzzle.position:transform.position;
        public ProvingProfile CutterGripProfile {get;private set;}
        public Vector3 CutterMuzzle=>cutterMuzzle?cutterMuzzle.position:transform.position;
        public Vector3 RifleMuzzle => rifleMuzzle ? rifleMuzzle.position : transform.position;
        // The imported marker locates the bore, but retains Blender's 90-degree
        // empty rotation. The rifle mesh is authored along its own +Z barrel axis.
        public Vector3 RifleDirection => rifle ? rifle.transform.forward : transform.forward;
        public void Initialize(GameObject prefab,ProvingProfile profile,GameObject pulsePrefab=null,GameObject cutterPrefab=null,ProvingProfile cutterProfile=null,GameObject plasmaPrefab=null,ProvingProfile plasmaProfile=null,GameObject plasmaLod1Prefab=null)
        {
            if(!prefab)throw new System.ArgumentException("Rifle GLB missing",nameof(prefab));
            var mount=GetComponentsInChildren<Transform>(true).Single(x=>x.name=="trooper:rig:weapon-mount");
            shotgun=GetComponentsInChildren<Renderer>(true).Single(x=>x.name=="weapon:joined");
            ShotgunDisplay=new GameObject("shotgun-ammo-display").AddComponent<ShotgunAmmoDisplay>();
            ShotgunDisplay.Initialize(shotgun.transform,profile,gameObject.layer);
            rifle=Object.Instantiate(prefab,mount);rifle.name="automatic-rifle";
            rifle.transform.localPosition=Vector3.zero;rifle.transform.localRotation=Quaternion.identity;
            // The imported grip already carries the authored rig scale; a second correction made the model fill the viewport.
            rifle.transform.localScale=Vector3.one*profile.Get("presentation.rifleModelScale");
            foreach(var item in rifle.GetComponentsInChildren<Transform>(true))item.gameObject.layer=gameObject.layer;
            foreach(var renderer in rifle.GetComponentsInChildren<Renderer>(true))
            {
                if(gameObject.layer>=15)renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var imported=renderer.sharedMaterials;
                // COBALT carries its authored PBR maps and distinct metal/rubber finishes.
                // Retain legacy mapping only for historical rifle assets.
                for(int i=0;i<imported.Length;i++)
                    if(imported[i] && !imported[i].name.StartsWith("cobalt",System.StringComparison.Ordinal))
                        imported[i]=RifleMaterial(imported[i].name);
                renderer.sharedMaterials=imported;
            }
            RifleDisplay=new GameObject("cobalt-ammo-display").AddComponent<RifleAmmoDisplay>();
            RifleDisplay.Initialize(rifle.transform,profile,gameObject.layer);
            rifleMuzzle=rifle.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="rifle-muzzle");
            if(pulsePrefab)
            {
                pulse=Object.Instantiate(pulsePrefab,mount);pulse.name="pulse-launcher";
                pulse.transform.localPosition=Vector3.zero;pulse.transform.localRotation=Quaternion.identity;
                var rig=GetComponent<TrooperVisual>()?.Tuning;
                PulseGripScale=rig!=null&&rig.Version>=32?rig.Get("grip.crimson.detail.scale"):1;
                pulse.transform.localScale=Vector3.one*profile.Get("presentation.rocketModelScale")*PulseGripScale;
                pulseMuzzle=pulse.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="pulse-muzzle");
                PulseDisplay=new GameObject("pulse-ammo-display").AddComponent<PulseAmmoDisplay>();
                PulseDisplay.Initialize(pulse.transform,profile,gameObject.layer);
                foreach(var t in pulse.GetComponentsInChildren<Transform>(true))t.gameObject.layer=gameObject.layer;
                foreach(var r in pulse.GetComponentsInChildren<Renderer>(true))
                { if(gameObject.layer>=15)r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; }
            }
            if(cutterPrefab)
            {
                var cp=cutterProfile??ProvingProfile.CreateCutterDefault();CutterGripProfile=cp;
                cutter=Object.Instantiate(cutterPrefab,mount);cutter.name="cutter";
                cutter.transform.localPosition=new Vector3(cp.Get("cutter.modelX"),cp.Get("cutter.modelY"),cp.Get("cutter.modelZ"));
                cutter.transform.localRotation=Quaternion.Euler(cp.Get("cutter.modelPitch"),cp.Get("cutter.modelYaw"),cp.Get("cutter.modelRoll"));cutter.transform.localScale=Vector3.one*cp.Get("cutter.modelScale");
                cutterMuzzle=cutter.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="cutter-muzzle");
                batteryRenderer=cutter.GetComponentsInChildren<Renderer>(true).SingleOrDefault(r=>r.name=="SEAM battery charge");
                if(batteryRenderer)
                {
                    var defaults=ProvingProfile.CreateCutterDefault();
                    float Read(string key){var path="cutter.seam.battery."+key;return cp.Descriptors.Any(d=>d.Path==path)?cp.Get(path):defaults.Get(path);}
                    batteryMaterial=new Material(Resources.Load<Shader>("CutterBattery")){name="SEAM charge display"};
                    batteryMaterial.SetColor("_ChargedColor",new Color(Read("red"),Read("green"),Read("blue"))*Read("intensity"));
                    batteryMaterial.SetColor("_DarkColor",new Color(Read("darkLevel"),Read("darkLevel"),Read("darkLevel"),1));
                    batteryMaterial.SetFloat("_Charge",0);batteryRenderer.sharedMaterial=batteryMaterial;
                    batteryRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;batteryRenderer.receiveShadows=false;
                }
                foreach(var t in cutter.GetComponentsInChildren<Transform>(true))t.gameObject.layer=gameObject.layer;
                foreach(var renderer in cutter.GetComponentsInChildren<Renderer>(true))if(gameObject.layer>=15)renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if(plasmaPrefab)
            {
                plasma=Object.Instantiate(plasmaPrefab,mount);plasma.name="forge-plasma";
                plasma.transform.localPosition=Vector3.zero;plasma.transform.localRotation=Quaternion.identity;
                var pp=plasmaProfile??ProvingProfile.CreatePlasmaDefault();
                PlasmaViewScale=gameObject.layer>=15?pp.Get("plasma.viewScale"):1;
                plasma.transform.localScale=Vector3.one*pp.Get("plasma.modelScale")*PlasmaViewScale;
                plasmaMuzzle=plasma.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="forge-muzzle");
                ForgeDisplay=new GameObject("forge-ammo-display").AddComponent<ForgeAmmoDisplay>();
                ForgeDisplay.Initialize(plasma.transform,profile,gameObject.layer);
                if(gameObject.layer<15)AddForgeLod(plasma,plasmaLod1Prefab);
                foreach(var t in plasma.GetComponentsInChildren<Transform>(true))t.gameObject.layer=gameObject.layer;
                foreach(var r in plasma.GetComponentsInChildren<Renderer>(true))if(gameObject.layer>=15)r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Show(WeaponId.Rifle);
        }
        // Only the authoritative energy ratio enters presentation. The asset/shader owns
        // exactly sixteen cells; changing their number would require authoring a new cartridge.
        public void UpdateCutterCharge(double energy,float capacity)
        {
            float next=capacity>0?Mathf.Clamp01((float)(energy/capacity)):0;
            if(next==CutterChargeFraction)return;
            CutterChargeFraction=next;if(batteryMaterial)batteryMaterial.SetFloat("_Charge",next);
        }
        void OnDestroy(){ForgeDisplay?.ReleaseResources();if(batteryMaterial)Destroy(batteryMaterial);}
        public static void AddForgeLod(GameObject root,GameObject lowPrefab)
        {
            if(!lowPrefab)return;
            var high=root.GetComponentsInChildren<Renderer>(true);var low=Object.Instantiate(lowPrefab,root.transform);
            low.name="forge-lod1";low.transform.localPosition=Vector3.zero;low.transform.localRotation=Quaternion.identity;low.transform.localScale=Vector3.one;
            // Screen-relative LOD selection is an asset-rendering budget, never a gameplay distance.
            var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.12f,high),new LOD(.005f,low.GetComponentsInChildren<Renderer>(true))});group.RecalculateBounds();
        }
        public void HideForCorpse()
        {
            ShotgunDisplay?.SetVisible(false);
            PulseDisplay?.SetVisible(false);
            ForgeDisplay?.SetVisible(false);
            RifleDisplay?.SetVisible(false);
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
                if(renderer.GetComponent<ShotgunAmmoDisplay>()!=null||renderer.name=="weapon:joined"||renderer.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name=="automatic-rifle"||t.name=="pulse-launcher"||t.name=="cutter"||t.name=="forge-plasma"))renderer.enabled=false;
        }
        public WeaponId ShownWeapon { get; private set; }
        public void Show(WeaponId weapon)
        {
            ShownWeapon=weapon;
            if(shotgun)shotgun.enabled=weapon==WeaponId.Shotgun;
            ShotgunDisplay?.SetVisible(weapon==WeaponId.Shotgun);
            PulseDisplay?.SetVisible(weapon==WeaponId.RocketLauncher);
            if(rifle)rifle.SetActive(weapon==WeaponId.Rifle);
            RifleDisplay?.SetVisible(weapon==WeaponId.Rifle);
            if(plasma)plasma.SetActive(weapon==WeaponId.Plasma);
            ForgeDisplay?.SetVisible(weapon==WeaponId.Plasma);
            if(cutter)cutter.SetActive(weapon==WeaponId.Cutter);
            if(pulse)pulse.SetActive(weapon==WeaponId.RocketLauncher);
        }
        public void Tint(Color identity,bool boosted)
        {
            var renderers=new List<Renderer>();if(shotgun)renderers.Add(shotgun);
            if(rifle)renderers.AddRange(rifle.GetComponentsInChildren<Renderer>(true));
            if(pulse)renderers.AddRange(pulse.GetComponentsInChildren<Renderer>(true));
            if(plasma)renderers.AddRange(plasma.GetComponentsInChildren<Renderer>(true));
            if(cutter)renderers.AddRange(cutter.GetComponentsInChildren<Renderer>(true));
            foreach(var renderer in renderers.Where(r=>r!=batteryRenderer&&r.GetComponent<ForgeAmmoDisplay>()==null&&r.GetComponent<PulseAmmoDisplay>()==null&&r.GetComponent<RifleAmmoDisplay>()==null))
                for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
                {
                    var material=renderer.sharedMaterials[slot];if(!material)continue;
                    bool status=TrooperIdentityPresentation.IsStatus(material);
                    if(boosted||status)TrooperIdentityPresentation.Paint(renderer,slot,boosted?Color.red:identity,status);
                    else renderer.SetPropertyBlock(null,slot);
                }
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
        sealed class Corpse { public int Participant; public GameObject Root; public double Expires; public TrooperVisual Visual; public DeathRagdoll Ragdoll; }
        sealed class PelletEffect { public GameObject Root, Trace, Core; public ParticleSystem[] ImpactParticles; public float ImpactTime; public PelletContact Surface; public Vector3 Start, TraceEnd, End, Normal; public double Begins; public bool Contact, Emitted; public int Shooter, Barrel; public WeaponId Weapon; }
        readonly NativeCombatSession session;
        readonly BloodPresentation bloodPresentation;
        readonly RocketPresentation rocketPresentation;
        internal RocketPresentation RocketEffectsForReview=>rocketPresentation;
        readonly RifleBulletPresentation riflePresentation;
        readonly CobaltShotPresentation cobaltShot;
        public CobaltShotPresentation CobaltShotForReview=>cobaltShot;
        readonly PlasmaPresentation plasmaPresentation;
        readonly ForgeReactorPresentation forgeReactor;
        readonly float forgeAmmoCapacity;
        public ForgeReactorPresentation ForgeReactorForReview=>forgeReactor;
        readonly CutterPresentation cutterPresentation;
        readonly ProvingProfile movement, tuning, deathProfile;
        DeathPhysicsWorld deathPhysics;
        readonly Transform owner;
        readonly PhysicsScene physics;
        readonly Camera[] cameras;
        readonly GameObject[] bodies, views;
        readonly FirstPersonWeaponSwitch[] weaponSwitches;
        readonly NativeMatchComposition composition;
        readonly int[] local;
        int ViewOf(int participant)=>composition!=null?composition.SeatOf(participant):participant;
        readonly Vector3[] deathEye, deathFeet, killerPoint, orbitStart;
        readonly double[] deathTime;
        readonly bool[] tracking;
        bool disposed;
        readonly HitFeedbackPresentation hitFeedback;
        public HitFeedbackPresentation HitFeedbackForReview=>hitFeedback;
        readonly List<Corpse> corpses = new List<Corpse>();
        readonly List<PelletEffect> pellets = new List<PelletEffect>();
        Material shotMaterial, traceMaterial, rifleTraceMaterial;
        readonly ShotgunDetailArt shotgunArt;
        public int ActiveShotEffectCount => pellets.Count+riflePresentation.ActiveCount+plasmaPresentation.ActiveCount;
        public CombatPresentation(NativeCombatSession session, ProvingProfile movement, ProvingProfile tuning,
            Transform owner, PhysicsScene physics, Camera[] cameras, GameObject[] bodies, GameObject[] views, NativeMatchComposition composition=null,ProvingProfile deathProfile=null,ProvingProfile bloodProfile=null,GameObject rocketPrefab=null,ProvingProfile rocketEffects=null,ProvingProfile hitProfile=null)
        {
            if(bodies.Length!=session.ParticipantCount || views.Length!=cameras.Length ||
                (composition==null?cameras.Length!=session.ParticipantCount:composition.ParticipantCount!=session.ParticipantCount || composition.LocalCount!=cameras.Length))
                throw new System.ArgumentException("Presentation ownership mismatch");
            this.deathProfile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(deathProfile??ProvingProfile.CreateDeathDefault()));
            this.composition=composition;local=composition?.Read().LocalParticipants ?? Enumerable.Range(0,cameras.Length).ToArray();
            shotgunArt=new ShotgunDetailArt(movement);
            this.session=session; this.movement=movement; this.tuning=tuning; this.owner=owner; this.physics=physics;
            this.cameras=cameras; this.bodies=bodies; this.views=views;
            weaponSwitches=new FirstPersonWeaponSwitch[views.Length];
            for(int v=0;v<views.Length;v++)
                if(views[v].GetComponent<TrooperVisual>())weaponSwitches[v]=new FirstPersonWeaponSwitch(views[v],cameras[v]);
            hitFeedback=new HitFeedbackPresentation(session,bodies,hitProfile??ProvingProfile.CreateHitFeedbackDefault(),bloodProfile??ProvingProfile.CreateBloodDefault());
            bloodPresentation=new BloodPresentation(session,bloodProfile??ProvingProfile.CreateBloodDefault(),owner,physics);
            cutterPresentation=new CutterPresentation(session,owner,bodies,views,local);
            rocketPresentation=new RocketPresentation(session,movement,owner,rocketPrefab,rocketEffects);
            plasmaPresentation=new PlasmaPresentation(session,owner,seat=>Muzzle(seat,0,WeaponId.Plasma));
            forgeAmmoCapacity=session.PlasmaProfile.Get("plasma.pickupAmmo");
            forgeReactor=new ForgeReactorPresentation(session,owner,bodies,views,local);
            cobaltShot=new CobaltShotPresentation(session,movement,owner,bodies,views,local);
            riflePresentation=new RifleBulletPresentation(session,movement,owner,seat=>Muzzle(seat,0,WeaponId.Rifle),cobaltShot);
            deathEye=new Vector3[bodies.Length]; deathFeet=new Vector3[bodies.Length]; killerPoint=new Vector3[bodies.Length]; orbitStart=new Vector3[bodies.Length];
            deathTime=new double[bodies.Length]; tracking=new bool[bodies.Length];
            session.StateRestored+=OnStateRestored;
            session.Died += OnDeath;
            session.Respawned += OnRespawn;
            session.Fired += OnFire;
            session.ShotResolved += OnShotResolved;
        }
        void OnFire(int seat,float damage)
        {
            bodies[seat].GetComponent<TrooperVisual>()?.Fire(session.Time,session.Life(seat).SelectedWeapon);
            if(session.Life(seat).SelectedWeapon==WeaponId.Shotgun)bodies[seat].GetComponent<VectorShotPresentation>()?.Fire(session.Time);
            int view=ViewOf(seat);if(view>=0){views[view].GetComponent<TrooperVisual>()?.Fire(session.Time,session.Life(seat).SelectedWeapon);if(session.Life(seat).SelectedWeapon==WeaponId.Shotgun)views[view].GetComponent<VectorShotPresentation>()?.Fire(session.Time);}
        }
        void OnShotResolved(ShotNotice notice)
        {
            // FixedUpdate can run several times before LateUpdate. Sample this shot's pose now,
            // before freezing its world-space muzzle; never use the previous rendered camera.
            PrepareShotPose(notice.Shooter);
            if(notice.Weapon==WeaponId.Plasma){plasmaPresentation.Launch(notice.Sequence,notice.Shooter);return;}
            if(notice.Weapon==WeaponId.Rifle)
            {
                cobaltShot.Fire(notice.Shooter);
                riflePresentation.Launch(notice.Sequence,notice.Shooter);
                return;
            }
            if(notice.Weapon==WeaponId.RocketLauncher)
                {
                    var muzzle=Muzzle(notice.Shooter,0,notice.Weapon);
                    var direction=session.Rockets.Single(r=>r.Id==notice.Sequence).Direction;
                    rocketPresentation.Launch(notice.Sequence,muzzle-notice.Origin,muzzle,direction);
                }
            int capacity=(int)movement.Get("presentation.shotMaxEffects");
            for(int pelletIndex=0;pelletIndex<notice.Pellets.Length;pelletIndex++)
            {
                var pellet=notice.Pellets[pelletIndex];
                if(pellets.Count>=capacity) break;
                var root=new GameObject("shot-pellet-"+notice.Sequence+"-"+pellets.Count);root.transform.SetParent(owner,false);
                var trace=GameObject.CreatePrimitive(PrimitiveType.Capsule);trace.name="pellet-streak";trace.transform.SetParent(root.transform,false);
                // Destroy is deferred; disable now so another fixed tick cannot collide with a visual.
                var collider=trace.GetComponent<Collider>();if(collider){collider.enabled=false;Object.Destroy(collider);}
                var traceRenderer=trace.GetComponent<Renderer>();traceRenderer.sharedMaterial=TraceMaterial(notice.Weapon);
                traceRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;traceRenderer.receiveShadows=false;
                // No intermediate frame may expose the shot-time pose before the final view pose is sampled.
                traceRenderer.enabled=false;
                var muzzle=Muzzle(notice.Shooter,pelletIndex,notice.Weapon);
                var direction=(pellet.Endpoint-muzzle).normalized;
                var clearance=Mathf.Min(movement.Get("presentation.shotMuzzleClearance"),Vector3.Distance(muzzle,pellet.Endpoint)*.5f);
                var start=muzzle+direction*clearance;
                trace.transform.position=start;
                var traceEnd=notice.Weapon==WeaponId.Rifle?start+direction*Mathf.Min(movement.Get("presentation.rifleTracerLength"),Vector3.Distance(start,pellet.Endpoint)):pellet.Endpoint;
                // Rifle presentation is tracer only: no green sphere at muzzle or endpoint.
                bool contact=notice.Weapon!=WeaponId.Rifle && pellet.Contact!=PelletContact.Miss;
                // Multiple hitscan pellets commonly share one small hit volume: render one readable contact flash,
                // not a stack of identical spheres. The individual flight paths remain visible.
                if(contact)
                    foreach(var existing in pellets)
                        if(existing.Contact && Vector3.Distance(existing.End,pellet.Endpoint)<movement.Get("presentation.shotImpactSize")*2f) { contact=false;break; }
                pellets.Add(new PelletEffect{Root=root,Trace=trace,Core=notice.Weapon==WeaponId.Shotgun?shotgunArt.Core(root.transform,root.layer):null,Surface=pellet.Contact,Start=start,TraceEnd=traceEnd,End=pellet.Endpoint,Normal=pellet.Normal,Begins=notice.Time,Contact=contact,Weapon=notice.Weapon,Shooter=notice.Shooter,Barrel=pelletIndex});
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
            if(weapon==WeaponId.Shotgun)return shotgunArt.Metal; // Lit metal, no emission or bloom tracer.
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
            var traceColor=new Color(movement.Get("presentation.shotTracerRed"),movement.Get("presentation.shotTracerGreen"),movement.Get("presentation.shotTracerBlue"),1f);
            traceMaterial=new Material(shader){color=traceColor};
            if(traceMaterial.HasProperty("_BaseColor"))traceMaterial.SetColor("_BaseColor",traceColor);
            return traceMaterial;
        }
        void PrepareShotPose(int participant)
        {
            var life=session.Life(participant);var pose=session.Pose(participant);
            int view=ViewOf(participant);
            var source=view>=0?views[view]:bodies[participant];
            if(view>=0)cameras[view].transform.SetPositionAndRotation(
                pose.Position+Vector3.up*movement.Get("camera.eyeHeight"),Quaternion.Euler(pose.Pitch,pose.Yaw,0));
            // Fired is dispatched after ShotResolved. Seed the same recoil clock before sampling
            // so an automatic follow-up cannot freeze the previous shot's animated barrel.
            var visual=source.GetComponent<TrooperVisual>();
            if(visual)visual.EquippedWeapon=WeaponSwitchPose.Read(life,session.WeaponSwitchSeconds).Weapon;
            visual?.Fire(session.Time,life.SelectedWeapon);
            visual?.Render(session.Time,pose.Velocity,life.Health,life.Life,pose.Grounded);
            if(view>=0)weaponSwitches[view]?.Apply(life,session.WeaponSwitchSeconds);
            else source.GetComponent<WeaponModelPresentation>()?.Show(life.SelectedWeapon);
        }
        Vector3 Muzzle(int participant,int pellet,WeaponId weapon)
        {
            int view=ViewOf(participant);
            if(weapon==WeaponId.Rifle||weapon==WeaponId.RocketLauncher||weapon==WeaponId.Plasma)
            {
                var model=(view>=0?views[view]:bodies[participant]).GetComponent<WeaponModelPresentation>();
                if(model)return weapon==WeaponId.Plasma?model.PlasmaMuzzle:weapon==WeaponId.Rifle?model.RifleMuzzle:model.PulseMuzzle;
            }
            var visual=(view>=0?views[view]:bodies[participant]).GetComponent<VectorShotPresentation>();
            return visual!=null && visual.MuzzleCount==2?visual.Muzzle(pellet):bodies[participant].transform.position+Vector3.up*movement.Get("camera.eyeHeight");
        }

        const float TraceWindowFraction=.18f; // Existing authored trace window; shared by birth and subsequent travel.
        void BeginVisiblePellet(PelletEffect effect,float flight)
        {
            if(!session.Life(effect.Shooter).Dead)
            {
                var muzzle=Muzzle(effect.Shooter,effect.Barrel,effect.Weapon);
                var direction=(effect.End-muzzle).normalized;
                var clearance=Mathf.Min(movement.Get("presentation.shotMuzzleClearance"),Vector3.Distance(muzzle,effect.End)*.5f);
                effect.Start=muzzle+direction*clearance;
                effect.TraceEnd=effect.Weapon==WeaponId.Rifle?effect.Start+direction*Mathf.Min(movement.Get("presentation.rifleTracerLength"),Vector3.Distance(effect.Start,effect.End)):effect.End;
            }
            // First draw has a nonzero segment whose tail is still at the rendered muzzle.
            // Its age is derived from the configured fixed tick and existing trace window, not another timer setting.
            effect.Begins=session.Time-Mathf.Min(1f/movement.Get("simulation.fixedTickHz"),flight*TraceFraction(effect));
            effect.Emitted=true;effect.Trace.GetComponent<Renderer>().enabled=true;
            if(effect.Core)effect.Core.GetComponent<Renderer>().enabled=true;
        }
        float TraceFraction(PelletEffect effect)
        {
            if(effect.Weapon!=WeaponId.Shotgun)return TraceWindowFraction;
            var distance=Vector3.Distance(effect.Start,effect.TraceEnd);
            return distance>0?Mathf.Min(TraceWindowFraction,movement.Get(ProvingProfile.VectorPrecisionPrefix+"traceLength")/distance):TraceWindowFraction;
        }
        void RenderPellets()
        {
            float flight=movement.Get("presentation.shotFlightSeconds"), impact=movement.Get("presentation.shotImpactSeconds");
            for(int i=pellets.Count-1;i>=0;i--)
            {
                var effect=pellets[i];
                float lifetime=effect.Weapon==WeaponId.Shotgun?impact*Mathf.Max(movement.Get(ProvingProfile.VectorPrecisionPrefix+"chipLifetimeScale"),movement.Get(ProvingProfile.VectorPrecisionPrefix+"dustLifetimeScale")):impact;
                if(!effect.Emitted)BeginVisiblePellet(effect,flight);
                float elapsed=(float)(session.Time-effect.Begins);
                if(elapsed<flight)
                {
                    float progress=Mathf.Clamp01(elapsed/flight);var head=Vector3.Lerp(effect.Start,effect.TraceEnd,progress);
                    var tail=Vector3.Lerp(effect.Start,effect.TraceEnd,Mathf.Max(0,progress-TraceFraction(effect)));
                    Vector3 delta=head-tail;effect.Trace.transform.SetPositionAndRotation((head+tail)*.5f,Quaternion.FromToRotation(Vector3.up,delta.normalized));
                    float width=movement.Get(effect.Weapon==WeaponId.Rifle?"presentation.rifleTracerWidth":"presentation.shotTracerWidth");effect.Trace.transform.localScale=new Vector3(width,delta.magnitude*.5f,width);
                    if(effect.Core){effect.Core.transform.position=head;effect.Core.transform.localScale=Vector3.one*width;}
                    continue;
                }
                if(effect.Trace){Object.Destroy(effect.Trace);effect.Trace=null;if(effect.Core){Object.Destroy(effect.Core);effect.Core=null;}if(effect.Contact)CreateImpact(effect);}
                if(effect.ImpactParticles!=null)
                {
                    float age=Mathf.Clamp(elapsed-flight,0,lifetime),delta=age-effect.ImpactTime;
                    if(delta>0)foreach(var ps in effect.ImpactParticles){ps.Simulate(delta,true,false,false);ps.Pause();}
                    effect.ImpactTime=age;
                }
                if(elapsed>=flight+lifetime){Object.Destroy(effect.Root);pellets.RemoveAt(i);}
            }
        }
        void CreateImpact(PelletEffect effect)
        {
            if(effect.Weapon==WeaponId.Shotgun)
            {
                effect.ImpactParticles=shotgunArt.Impact(effect.Root.transform,effect.End,effect.Normal,effect.Surface,movement,(uint)(effect.Barrel+1));return;
            }
            var impact=GameObject.CreatePrimitive(PrimitiveType.Sphere);impact.name="shot-impact";impact.transform.SetParent(effect.Root.transform,false);
            impact.transform.position=effect.End+effect.Normal*.01f;impact.transform.localScale=Vector3.one*movement.Get("presentation.shotImpactSize");
            var collider=impact.GetComponent<Collider>();if(collider){collider.enabled=false;Object.Destroy(collider);}
            var renderer=impact.GetComponent<Renderer>();renderer.material=effect.Weapon==WeaponId.Rifle?TraceMaterial(WeaponId.Rifle):ShotMaterial();
        }
        void OnStateRestored()
        {
            foreach(var pellet in pellets)if(pellet.Root){pellet.Root.SetActive(false);Object.Destroy(pellet.Root);}
            pellets.Clear();
            foreach(var body in bodies)if(body)body.GetComponent<VectorShotPresentation>()?.Clear(session.Time);
            foreach(var view in views)if(view)view.GetComponent<VectorShotPresentation>()?.Clear(session.Time);
            foreach(var corpse in corpses)if(corpse.Root){corpse.Root.SetActive(false);Object.Destroy(corpse.Root);}
            corpses.Clear();deathPhysics?.Dispose();deathPhysics=null;
            System.Array.Clear(tracking,0,tracking.Length);
            for(int p=0;p<session.ParticipantCount;p++)
            {
                bodies[p].SetActive(true);bodies[p].GetComponent<TrooperVisual>()?.ResetPose(session.Time);
                int v=ViewOf(p);if(v>=0){views[v].SetActive(true);views[v].GetComponent<TrooperVisual>()?.ResetPose(session.Time);}
                if(session.Life(p).Dead)OnDeath(new DeathNotice(p,session.Life(p),session.Pose(p)));
            }
        }
        void OnRespawn(int seat)
        {
            hitFeedback.ClearParticipant(seat);
            foreach(var corpse in corpses)if(corpse.Participant==seat&&corpse.Root)
                foreach(var t in corpse.Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;
            bodies[seat].SetActive(true);int view=ViewOf(seat);if(view>=0)views[view].SetActive(true);
            bodies[seat].GetComponent<TrooperVisual>()?.ResetPose(session.Time);
            if(view>=0)views[view].GetComponent<TrooperVisual>()?.ResetPose(session.Time);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            forgeReactor.Dispose();shotgunArt.Dispose();hitFeedback.Dispose();bloodPresentation.Dispose();rocketPresentation.Dispose();riflePresentation.Dispose();cobaltShot.Dispose();plasmaPresentation.Dispose();cutterPresentation.Dispose();
            session.StateRestored-=OnStateRestored;session.Died-=OnDeath; session.Respawned-=OnRespawn; session.Fired-=OnFire;session.ShotResolved-=OnShotResolved;
            foreach(var corpse in corpses) if(corpse.Root) { corpse.Root.SetActive(false); Object.Destroy(corpse.Root); }
            corpses.Clear();deathPhysics?.Dispose();deathPhysics=null;
            foreach(var pellet in pellets)if(pellet.Root)Object.Destroy(pellet.Root);
            pellets.Clear();if(shotMaterial)Object.Destroy(shotMaterial);if(traceMaterial)Object.Destroy(traceMaterial);if(rifleTraceMaterial)Object.Destroy(rifleTraceMaterial);
            foreach(var body in bodies)if(body)body.SetActive(true);
            foreach(var view in views)if(view)view.SetActive(true);
        }
        void OnDeath(DeathNotice notice)
        {
            int seat=notice.Seat;
            hitFeedback.ClearParticipant(seat);
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
            corpse.transform.SetPositionAndRotation(notice.Pose.Position,Quaternion.Euler(0,notice.Pose.Yaw,0));
            var visual=corpse.GetComponent<TrooperVisual>();
            DeathRagdoll ragdoll=null;
            if(visual)
            {
                if(deathPhysics==null)deathPhysics=new DeathPhysicsWorld(owner.gameObject.scene,deathProfile,1/movement.Get("simulation.fixedTickHz"),session.Time);
                ragdoll=deathPhysics.Add(corpse,notice.Pose,notice.Impact,session.Time);
            }
            else
            {
                // Unskinned diagnostic fixtures retain their existing floor-projected placeholder.
                // Shipping troopers always take the physical path above, including airborne deaths.
                corpse.transform.rotation=Quaternion.Euler(90,notice.Pose.Yaw,0);
                Vector3 origin=notice.Pose.Position+Vector3.up*movement.Get("camera.eyeHeight");
                float range=Mathf.Max(movement.Get("player.capsule.height"),origin.y-movement.Get("world.minimumSupportHeight")+movement.Get("player.capsule.height"));
                if(physics.Raycast(origin,Vector3.down,out var support,range,(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer),QueryTriggerInteraction.Ignore))
                {
                    var renderers=corpse.GetComponentsInChildren<Renderer>();
                    if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);corpse.transform.position+=Vector3.up*(support.point.y-bounds.min.y);}
                }
            }
            corpses.Add(new Corpse { Participant=seat, Root=corpse, Visual=visual, Ragdoll=ragdoll, Expires=session.Time+tuning.Get("death.corpseSeconds") });
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
                    { killer=p==seat?"Ты убил себя":"Вас убил "+(composition?.Participant(p).Name ?? ("Игрок "+(p+1)));break; }
            return killer+"\nВозрождение через "+Mathf.CeilToInt((float)life.RespawnRemaining);
        }
        public void Render()
        {
            hitFeedback.Render();bloodPresentation.Render();
            rocketPresentation.Render();
            foreach(var body in bodies)if(body)body.GetComponent<VectorShotPresentation>()?.Render(session.Time);
            foreach(var view in views)if(view)view.GetComponent<VectorShotPresentation>()?.Render(session.Time);
            for(int c=corpses.Count-1;c>=0;c--)
            {
                if(session.Time>=corpses[c].Expires) { deathPhysics?.Remove(corpses[c].Ragdoll); Object.Destroy(corpses[c].Root); corpses.RemoveAt(c); }

            }
            deathPhysics?.Advance(session.Time);
            for(int p=0;p<session.ParticipantCount;p++)
            {
                var life=session.Life(p);
                var bodyVisual=bodies[p].GetComponent<TrooperVisual>();
                if(bodyVisual)bodyVisual.EquippedWeapon=WeaponSwitchPose.Read(life,session.WeaponSwitchSeconds).Weapon;
                if(!life.Dead)bodies[p].GetComponent<TrooperVisual>()?.Render(session.Time,session.Pose(p).Velocity,life.Health,life.Life,session.Pose(p).Grounded);
                // Both representations use the same simulation phase: the model changes only at the lift apex.
                var bodyWeapon=bodies[p].GetComponent<WeaponModelPresentation>();
                bodyWeapon?.Show(WeaponSwitchPose.Read(life,session.WeaponSwitchSeconds).Weapon);
                bodyWeapon?.ShotgunDisplay?.SetAmmo(life.ShotgunAmmo);
                bodyWeapon?.PulseDisplay?.SetAmmo(life.RocketAmmo,session.RocketAmmoCapacity);
                bodyWeapon?.ForgeDisplay?.SetAmmo(life.PlasmaAmmo,forgeAmmoCapacity);
                if(life.Dead)bodyWeapon?.ForgeDisplay?.SetVisible(false);
                if(life.Dead)bodyWeapon?.PulseDisplay?.SetVisible(false);
                bodyWeapon?.RifleDisplay?.Render(life.RifleAmmo,session.RifleStartingAmmo,session.Time,life.Life);
                if(life.Dead){bodyWeapon?.ShotgunDisplay?.SetVisible(false);bodyWeapon?.RifleDisplay?.SetVisible(false);}
            }
            for(int v=0;v<cameras.Length;v++)
            {
                int i=local[v];var life=session.Life(i); var pose=session.Pose(i);
                views[v].GetComponent<WeaponModelPresentation>()?.ShotgunDisplay?.SetAmmo(life.ShotgunAmmo);
                views[v].GetComponent<WeaponModelPresentation>()?.PulseDisplay?.SetAmmo(life.RocketAmmo,session.RocketAmmoCapacity);
                var viewWeapon=views[v].GetComponent<WeaponModelPresentation>();
                viewWeapon?.ForgeDisplay?.SetAmmo(life.PlasmaAmmo,forgeAmmoCapacity);
                if(life.Dead)viewWeapon?.ForgeDisplay?.SetVisible(false);
                viewWeapon?.RifleDisplay?.Render(life.RifleAmmo,session.RifleStartingAmmo,session.Time,life.Life);
                if(life.Dead)viewWeapon?.RifleDisplay?.SetVisible(false);
                if(!life.Dead)
                {
                    // The grip solver uses world-space joints. Sample under this
                    // frame's camera, matching PrepareShotPose and frozen re-renders.
                    cameras[v].transform.SetPositionAndRotation(pose.Position+Vector3.up*movement.Get("camera.eyeHeight"),Quaternion.Euler(pose.Pitch,pose.Yaw,0));
                    var viewVisual=views[v].GetComponent<TrooperVisual>();
                    if(viewVisual)viewVisual.EquippedWeapon=WeaponSwitchPose.Read(life,session.WeaponSwitchSeconds).Weapon;
                    views[v].GetComponent<TrooperVisual>()?.Render(session.Time,pose.Velocity,life.Health,life.Life,pose.Grounded);
                    weaponSwitches[v]?.Apply(life,session.WeaponSwitchSeconds);
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
            // Capture a new trace only after all cameras, animation and weapon-switch joints are final for this frame.
            RenderPellets();riflePresentation.Render();plasmaPresentation.Render();cutterPresentation.Render();cobaltShot.Render();forgeReactor.Render();
        }
    }
}
