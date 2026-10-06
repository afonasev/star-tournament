using System;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Bounded prediction from filtered knowledge, never a future combat result.</summary>
    public sealed class NativeBotWeaponEstimate
    {
        readonly ProvingProfile combat, life, cutter;
        readonly ShotgunResolver zones;
        readonly NativeShotgunPolicy aim;
        public NativeBotWeaponEstimate(ProvingProfile movement,ProvingProfile combat,ProvingProfile life,ProvingProfile cutter,NativeShotgunPolicy aim)
        {
            this.combat=combat;this.life=life;this.cutter=cutter;this.aim=aim;
            zones=new ShotgunResolver(combat,movement.Get("player.capsule.height"));
        }
        public float Estimate(WeaponId id,NativeBotFrame frame,NativeBotMemoryEntry target,double time,float phase,
            float horizon,int timeSamples,int spreadSamples,float rate,float error,float period,float lead)
        {
            if(!target.Visible||!CombatLife.Owned(frame.Life,id))return 0;
            bool changing=id!=frame.Life.SelectedWeapon;
            float delay=changing?life.Get("weapon.switchSeconds"):(float)frame.Life.SwitchRemaining;
            if(frame.Life.SwitchRemaining>0&&frame.Life.PendingWeapon!=id)return 0;
            if(frame.Life.PendingWeapon==id&&frame.Life.SwitchRemaining>0)delay=(float)frame.Life.SwitchRemaining;
            delay=Mathf.Max(delay,(float)CombatLife.CooldownFor(frame.Life,id));
            float duration=horizon-delay;
            if(duration<=0)return 0;
            float distance=Vector3.Distance(frame.Pose.Position,target.Sighting.Position);
            if(id==WeaponId.Cutter)
            {
                if(distance<cutter.Get("cutter.botMinimumDistance")||distance>cutter.Get("cutter.maxRangeMeters")*cutter.Get("cutter.botRangeFraction"))return 0;
                duration=Mathf.Min(duration,(float)frame.Life.CutterEnergy/cutter.Get("cutter.energyPerSecond"));
            }
            else if(CombatLife.AmmoFor(frame.Life,id)<=0)return 0;
            int steps=Mathf.Max(2,timeSamples);float dt=horizon/steps,score=0;
            var pose=frame.Pose;float nextShot=delay;
            int rounds=id==WeaponId.Cutter?0:CombatLife.AmmoFor(frame.Life,id);
            float cooldown=id==WeaponId.Rifle?life.Get("rifle.cooldownSeconds"):id==WeaponId.Shotgun?life.Get("combat.cooldownSeconds"):life.Get("rocket.cooldownSeconds");
            float damage=id==WeaponId.Rifle?combat.Get("rifle.damage"):id==WeaponId.Shotgun?combat.Get("shot.damage"):combat.Get("rocket.maximumDamage");
            float boostMultiplier=life.Get("damageBoost.multiplier");
            var beamDelivered=id==WeaponId.Cutter?new float[frame.Knowledge.Enemies.Length]:null;
            float delivered=0;
            for(int i=0;i<steps;i++)
            {
                float t=(i+.5f)*dt;pose.Position=frame.Pose.Position+frame.Pose.Velocity*t;
                var observed=target.Sighting.Position+target.Sighting.Velocity*t;
                float travel=id==WeaponId.Rifle?Vector3.Distance(pose.Position,observed)/combat.Get("rifle.speed"):0;
                var point=observed+Vector3.up*aim.TargetHeight+target.Sighting.Velocity*travel*lead;
                var action=aim.AimPoint(pose,point,time+t,dt,phase,rate,error,period,out _);
                pose.Yaw+=action.LookDegrees.x;pose.Pitch-=action.LookDegrees.y;
                if(id==WeaponId.Cutter)
                {
                    float from=Mathf.Max(i*dt,delay),to=Mathf.Min((i+1)*dt,delay+duration);
                    if(to<=from)continue;
                    float extension=cutter.Get("cutter.extensionSeconds");
                    float range=cutter.Get("cutter.maxRangeMeters")*(extension<=0?1:Mathf.Clamp01((t-delay)/extension));
                    var origin=pose.Position+Vector3.up*aim.EyeHeight;
                    var ray=Quaternion.Euler(pose.Pitch,pose.Yaw,0)*Vector3.forward;
                    for(int enemyIndex=0;enemyIndex<frame.Knowledge.Enemies.Length;enemyIndex++)
                    {
                        var enemy=frame.Knowledge.Enemies[enemyIndex];if(!enemy.Visible)continue;
                        float contact=Coverage(origin,ray,enemy.Sighting,t,range);
                        float hitDamage=cutter.Get("cutter.referenceDamage")/cutter.Get("cutter.referenceContactSeconds")*(to-from)*contact*(frame.DamageRemaining>t?boostMultiplier:1);
                        if(enemy.Sighting.HasVitals)hitDamage=Mathf.Min(hitDamage,Mathf.Max(0,enemy.Sighting.Health+enemy.Sighting.Armor-beamDelivered[enemyIndex]));
                        score+=hitDamage/(1+t/horizon);beamDelivered[enemyIndex]+=hitDamage;
                    }
                }
                else
                {
                    while(rounds>0&&nextShot<(i+1)*dt&&nextShot<horizon)
                    {
                        float flight=id==WeaponId.Rifle?travel:id==WeaponId.RocketLauncher?distance/combat.Get("rocket.speed"):0;
                        float probability;
                        if(id==WeaponId.RocketLauncher)
                            probability=Mathf.Clamp01(1-aim.TargetHeight/aim.RocketRadius); // Existing safe RocketPlan owns body/floor/LOS choice.
                        else
                        {
                            probability=0;float spread=Mathf.Tan(combat.Get(id==WeaponId.Rifle?"rifle.spread":"shot.spread")*Mathf.Deg2Rad);
                            var rotation=Quaternion.Euler(pose.Pitch,pose.Yaw,0);var origin=pose.Position+Vector3.up*aim.EyeHeight;
                            int count=Mathf.Max(1,spreadSamples);
                            for(int n=0;n<count;n++)
                            {
                                // Golden-angle equal-area disk quadrature: technical sampling algorithm,
                                // not a new spread or damage multiplier. Count is profile-owned.
                                float r=Mathf.Sqrt((n+.5f)/count)*spread,a=n*2.39996323f;
                                var ray=rotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,1).normalized;
                                probability+=Coverage(origin,ray,target.Sighting,t+flight,id==WeaponId.Rifle?float.PositiveInfinity:zones.Range);
                            }
                            probability/=count;
                        }
                        float hitDamage=damage*probability*(frame.DamageRemaining>nextShot?boostMultiplier:1);
                        if(target.Sighting.HasVitals)hitDamage=Mathf.Min(hitDamage,Mathf.Max(0,target.Sighting.Health+target.Sighting.Armor-delivered));
                        score+=hitDamage/(1+(nextShot+flight)/horizon);delivered+=hitDamage;
                        nextShot+=cooldown;rounds--;
                    }
                }
            }
            return score;
        }
        float Coverage(Vector3 origin,Vector3 ray,NativeBotSighting target,float future,float range)
        {
            var feet=target.Position+target.Velocity*future;var delta=origin-feet;
            float facing=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            var pose=new ParticipantState{Position=feet,Yaw=facing};
            var body=new CombatTarget(target.Participant,target.Life,pose);
            float front=zones.ResolveTarget(origin,ray,body,range).TargetIndex>=0?1:0;
            // Enemy yaw is absent from filtered sightings. Equal frontal/side uncertainty
            // avoids reading a hidden authoritative pose or pretending an exact silhouette.
            pose.Yaw+=90;body=new CombatTarget(target.Participant,target.Life,pose);
            return (front+(zones.ResolveTarget(origin,ray,body,range).TargetIndex>=0?1:0))*.5f;
        }
    }
}
