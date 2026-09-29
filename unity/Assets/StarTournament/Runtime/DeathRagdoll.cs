using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround
{
    /// <summary>One world shared by all corpse visuals. Only match clock advances it;
    /// its proxies can never enter gameplay queries or move live controllers.</summary>
    internal sealed class DeathPhysicsWorld : IDisposable
    {
        readonly Scene scene;
        readonly PhysicsScene physics;
        readonly ProvingProfile profile;
        readonly List<DeathRagdoll> bodies=new List<DeathRagdoll>();
        readonly PhysicsMaterial material;
        readonly float step;
        double clock;
        bool disposed;
        public DeathPhysicsWorld(Scene source,ProvingProfile profile,float step,double clock)
        {
            this.profile=profile;this.step=step;this.clock=clock;
            scene=SceneManager.CreateScene("corpse-physics-"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics=scene.GetPhysicsScene();
            material=new PhysicsMaterial("corpse-friction"){dynamicFriction=profile.Get("corpse.friction"),staticFriction=profile.Get("corpse.friction"),bounciness=0};
            foreach(var root in source.GetRootGameObjects())
                foreach(var c in root.GetComponentsInChildren<Collider>())
                {
                    if(!c.enabled||c.isTrigger||(c.gameObject.layer!=ProvingArena.WorldLayer&&c.gameObject.layer!=ProvingArena.MovementOnlyLayer))continue;
                    var go=new GameObject("corpse-support-"+c.name);SceneManager.MoveGameObjectToScene(go,scene);
                    go.transform.SetPositionAndRotation(c.transform.position,c.transform.rotation);go.transform.localScale=c.transform.lossyScale;
                    if(c is BoxCollider box){var copy=go.AddComponent<BoxCollider>();copy.center=box.center;copy.size=box.size;}
                    else if(c is MeshCollider mesh){var copy=go.AddComponent<MeshCollider>();copy.sharedMesh=mesh.sharedMesh;copy.convex=mesh.convex;}
                    else throw new InvalidOperationException("Unsupported corpse support collider: "+c.GetType().Name);
                }
        }
        public DeathRagdoll Add(GameObject visual,ParticipantState pose,FatalImpact impact,double now)
        {
            Advance(now);
            var body=new DeathRagdoll(visual,scene,profile,material,pose.Velocity,impact.Velocity(profile));
            // Neither self-collision nor corpse piles can inject unstable impulses. World contacts remain enabled.
            foreach(var collider in body.Colliders)
            {
                foreach(var other in body.Colliders)if(collider!=other)Physics.IgnoreCollision(collider,other);
                foreach(var old in bodies)foreach(var other in old.Colliders)Physics.IgnoreCollision(collider,other);
            }
            bodies.Add(body);Physics.SyncTransforms();return body;
        }
        public void Remove(DeathRagdoll body){if(body==null)return;bodies.Remove(body);body.Dispose();}
        public void Advance(double now)
        {
            if(disposed)return;
            // Bounded fixed substeps; no real-time catch-up while the match is paused.
            while(now-clock>=step-0.000001)
            {
                foreach(var body in bodies)body.LimitVelocity();
                if(bodies.Count>0)physics.Simulate(step);
                clock+=step;
            }
            foreach(var body in bodies)body.Present();
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var body in bodies)body.Dispose();bodies.Clear();
            foreach(var root in scene.GetRootGameObjects())root.SetActive(false);
            SceneManager.UnloadSceneAsync(scene);Object.Destroy(material);
        }
    }

    public sealed class DeathRagdoll : IDisposable
    {
        readonly List<(Transform bone,Rigidbody proxy)> segments=new List<(Transform,Rigidbody)>();
        readonly GameObject root;
        readonly ProvingProfile profile;
        public Collider[] Colliders {get;}
        public Vector3 Center=>segments[0].proxy.position;
        public float MaximumSegmentSpeed=>segments.Max(s=>s.proxy.linearVelocity.magnitude);
        public bool Sleeping=>segments.All(s=>s.proxy.IsSleeping());
        public DeathRagdoll(GameObject visual,Scene scene,ProvingProfile profile,PhysicsMaterial material,Vector3 velocity,Vector3 impulse)
        {
            this.profile=profile;
            // Preserve the evaluated pose. Destroying the manual graph doesn't Rebind bones.
            var animation=visual.GetComponent<TrooperVisual>();animation?.Release();if(animation)animation.Ragdoll=this;
            foreach(var animator in visual.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
            // Approved fallback: the current weapon mount is authored for a two-hand pose.
            // Hide corpse weapons, rather than retaining a stiff support-hand grip or a floating prop.
            visual.GetComponent<WeaponModelPresentation>()?.HideForCorpse();
            root=new GameObject("ragdoll-proxies");SceneManager.MoveGameObjectToScene(root,scene);
            var bones=visual.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var map=new Dictionary<string,Rigidbody>();
            // Bone names and parent topology are immutable imported rig data. Capsule dimensions
            // derive from those authored landmarks; tunable thickness/mass/limits live in Lab.
            Add("Hips","Spine",null,true);
            Add("Chest","Neck","Hips",true);
            Add("Head",null,"Chest",true);
            foreach(var side in new[]{"Left","Right"})
            {
                Add(side+"UpperArm",side+"LowerArm","Chest",false);
                Add(side+"LowerArm",side+"Hand",side+"UpperArm",false);
                Add(side+"UpperLeg",side+"LowerLeg","Hips",false);
                Add(side+"LowerLeg",side+"Foot",side+"UpperLeg",false);
            }
            void Add(string name,string end,string parent,bool torso)
            {
                var bone=bones[name];
                Vector3 endpoint=end!=null?bones[end].position:bone.position+(bone.position-bones["Neck"].position);
                var delta=endpoint-bone.position;float length=delta.magnitude;
                var go=new GameObject(name);go.transform.SetParent(root.transform);go.transform.SetPositionAndRotation(bone.position,bone.rotation);
                var shapeGo=new GameObject("shape");shapeGo.transform.SetParent(go.transform,false);
                shapeGo.transform.position=bone.position+delta*.5f;shapeGo.transform.rotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
                var shape=shapeGo.AddComponent<CapsuleCollider>();
                float width=torso?name=="Head"?length*2:Vector3.Distance(bones["LeftUpperArm"].position,bones["RightUpperArm"].position):length;
                shape.radius=width*profile.Get("corpse.radiusRatio")*(torso?profile.Get(name=="Head"?"corpse.headRadiusScale":"corpse.torsoRadiusScale"):1);shape.height=length+2*shape.radius;shape.direction=1;shape.sharedMaterial=material;
                var rb=go.AddComponent<Rigidbody>();rb.mass=profile.Get("corpse.mass")*(torso?profile.Get("corpse.torsoMassRatio"):1);
                rb.useGravity=false;
                rb.linearDamping=profile.Get("corpse.linearDamping");rb.angularDamping=profile.Get("corpse.angularDamping");
                rb.maxAngularVelocity=profile.Get("corpse.maximumAngularSpeed");rb.maxDepenetrationVelocity=profile.Get("corpse.depenetrationSpeed");rb.sleepThreshold=profile.Get("corpse.sleepThreshold");
                // Solver counts are engine-quality invariants to prevent joint separation, not game tuning.
                rb.solverIterations=12;rb.solverVelocityIterations=4;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                rb.linearVelocity=Vector3.ClampMagnitude(velocity+impulse,profile.Get("corpse.maximumSpeed"));
                if(parent!=null)
                {
                    var joint=go.AddComponent<CharacterJoint>();joint.connectedBody=map[parent];joint.autoConfigureConnectedAnchor=false;
                    joint.anchor=Vector3.zero;joint.connectedAnchor=map[parent].transform.InverseTransformPoint(bone.position);
                    joint.axis=go.transform.InverseTransformDirection(delta.normalized);
                    // Choose a nonparallel basis; .9 is a numerical basis-selection invariant.
                    joint.swingAxis=Vector3.Cross(joint.axis,Mathf.Abs(Vector3.Dot(joint.axis,Vector3.up))<.9f?Vector3.up:Vector3.right).normalized;
                    float swing=profile.Get(torso?"corpse.torsoSwingDegrees":"corpse.swingDegrees");float twist=profile.Get("corpse.twistDegrees");
                    joint.lowTwistLimit=new SoftJointLimit{limit=-twist};joint.highTwistLimit=new SoftJointLimit{limit=twist};
                    joint.swing1Limit=new SoftJointLimit{limit=swing};joint.swing2Limit=new SoftJointLimit{limit=swing};
                    joint.enableProjection=true;joint.projectionDistance=profile.Get("corpse.projectionDistance");
                    joint.enablePreprocessing=false;
                }
                rb.WakeUp();
                map.Add(name,rb);segments.Add((bone,rb));
            }
            // Child collider belongs to its parent rigidbody; use that collider for IgnoreCollision.
            Colliders=segments.Select(s=>s.proxy.GetComponentInChildren<Collider>()).ToArray();
        }
        public void LimitVelocity()
        {
            foreach(var (_,rb) in segments)
            {
                if(!rb.IsSleeping())rb.AddForce(Vector3.down*profile.Get("corpse.gravity"),ForceMode.Acceleration);
                if(rb.linearVelocity.sqrMagnitude>profile.Get("corpse.maximumSpeed")*profile.Get("corpse.maximumSpeed"))rb.linearVelocity=Vector3.ClampMagnitude(rb.linearVelocity,profile.Get("corpse.maximumSpeed"));
            }
        }
        public void Present(){foreach(var (bone,rb) in segments)if(bone)bone.SetPositionAndRotation(rb.position,rb.rotation);}
        public void Dispose(){if(root){root.SetActive(false);Object.Destroy(root);}}
    }
}
