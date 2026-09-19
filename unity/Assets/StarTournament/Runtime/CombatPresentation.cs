using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Reads combat state; never changes movement, hit zones, damage or spawn choice.</summary>
    public sealed class CombatPresentation : System.IDisposable
    {
        sealed class Corpse { public GameObject Root; public double Expires; }
        readonly NativeCombatSession session;
        readonly ProvingProfile movement, tuning;
        readonly Transform owner;
        readonly PhysicsScene physics;
        readonly Camera[] cameras;
        readonly GameObject[] bodies, views;
        readonly Vector3[] deathEye, deathFeet, killerPoint;
        readonly double[] deathTime;
        readonly bool[] tracking;
        readonly List<Corpse> corpses = new List<Corpse>();
        public CombatPresentation(NativeCombatSession session, ProvingProfile movement, ProvingProfile tuning,
            Transform owner, PhysicsScene physics, Camera[] cameras, GameObject[] bodies, GameObject[] views)
        {
            if(cameras.Length!=session.ParticipantCount || bodies.Length!=session.ParticipantCount || views.Length!=session.ParticipantCount)
                throw new System.ArgumentException("Presentation requires one view per local participant in this human-only slice");
            this.session=session; this.movement=movement; this.tuning=tuning; this.owner=owner; this.physics=physics;
            this.cameras=cameras; this.bodies=bodies; this.views=views;
            deathEye=new Vector3[cameras.Length]; deathFeet=new Vector3[cameras.Length]; killerPoint=new Vector3[cameras.Length];
            deathTime=new double[cameras.Length]; tracking=new bool[cameras.Length];
            session.Died += OnDeath;
            session.Respawned += OnRespawn;
        }
        void OnRespawn(int seat) { bodies[seat].SetActive(true); views[seat].SetActive(true); }
        public void Dispose()
        {
            session.Died-=OnDeath; session.Respawned-=OnRespawn;
            foreach(var corpse in corpses) if(corpse.Root) { corpse.Root.SetActive(false); Object.Destroy(corpse.Root); }
            corpses.Clear();
            for(int i=0;i<bodies.Length;i++) { bodies[i].SetActive(true); views[i].SetActive(true); }
        }
        void OnDeath(DeathNotice notice)
        {
            int seat=notice.Seat;
            deathFeet[seat]=notice.Pose.Position;
            deathEye[seat]=notice.Pose.Position+Vector3.up*movement.Get("camera.eyeHeight");
            deathTime[seat]=session.Time; tracking[seat]=true;
            killerPoint[seat]=deathEye[seat]+Quaternion.Euler(notice.Pose.Pitch,notice.Pose.Yaw,0)*Vector3.forward;
            var corpse=Object.Instantiate(bodies[seat],owner);
            corpse.name=$"corpse-{seat+1}-life-{notice.Life.Life}";
            corpse.SetActive(true);
            foreach(var t in corpse.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=0;
            foreach(var collider in corpse.GetComponentsInChildren<Collider>(true)) { collider.enabled=false; Object.Destroy(collider); }
            corpse.transform.SetPositionAndRotation(notice.Pose.Position+Vector3.up*tuning.Get("death.bodyHeight"),Quaternion.Euler(90,notice.Pose.Yaw,0));
            // Presentation-only support projection also handles airborne deaths; it never moves the motor.
            Vector3 supportOrigin=notice.Pose.Position+Vector3.up*movement.Get("camera.eyeHeight");
            float supportRange=Mathf.Max(movement.Get("player.capsule.height"),supportOrigin.y-movement.Get("fixture.floorHeight")+movement.Get("player.capsule.height"));
            if(physics.Raycast(supportOrigin,Vector3.down,out var support,supportRange,
                (1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer),QueryTriggerInteraction.Ignore))
            {
                var renderers=corpse.GetComponentsInChildren<Renderer>();
                if(renderers.Length>0)
                {
                    var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    corpse.transform.position+=Vector3.up*(support.point.y-bounds.min.y);
                }
                deathFeet[seat]=support.point;
            }
            corpses.Add(new Corpse { Root=corpse, Expires=session.Time+tuning.Get("death.corpseSeconds") });
            bodies[seat].SetActive(false); views[seat].SetActive(false);
            UpdateKiller(seat); // Capture killer life immediately, even if it dies later in this tick.
        }
        void UpdateKiller(int seat)
        {
            if (!tracking[seat]) return;
            var dead=session.Life(seat);
            for(int i=0;i<cameras.Length;i++)
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
            string killer=life.KillerId==null ? "Вы погибли" : "Вас убил Игрок "+life.KillerId.Replace("player-","");
            return killer+"\nВозрождение через "+Mathf.CeilToInt((float)life.RespawnRemaining);
        }
        public void Render()
        {
            for(int c=corpses.Count-1;c>=0;c--) if(session.Time>=corpses[c].Expires)
            { Object.Destroy(corpses[c].Root); corpses.RemoveAt(c); }
            for(int i=0;i<cameras.Length;i++)
            {
                var life=session.Life(i); var pose=session.Pose(i);
                if(!life.Dead)
                {
                    cameras[i].transform.SetPositionAndRotation(pose.Position+Vector3.up*movement.Get("camera.eyeHeight"),Quaternion.Euler(pose.Pitch,pose.Yaw,0));
                    continue;
                }
                Vector3 position=deathEye[i], target;
                if(life.KillerId!=null) { UpdateKiller(i); target=killerPoint[i]; }
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
                if(direction.sqrMagnitude>0) cameras[i].transform.SetPositionAndRotation(position,Quaternion.LookRotation(direction));
            }
        }
    }
}
