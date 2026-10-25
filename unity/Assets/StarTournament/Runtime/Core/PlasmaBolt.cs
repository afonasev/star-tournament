using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public struct PlasmaBoltState
    {
        public uint Id;
        public int Owner, OwnerLife;
        public Vector3 Position, Direction;
        public float Distance, DamageMultiplier;
        public bool AccuracyTracked;
        public void Validate(int participants,uint sequence)
        {
            // Tolerance is a serialization numeric invariant, not gameplay tuning.
            if(Id==0||Id>sequence||Owner<0||Owner>=participants||OwnerLife<1||!Finite(Position)||!Finite(Direction)||Mathf.Abs(Direction.sqrMagnitude-1)>1e-4f||!Finite(Distance)||Distance<0||!Finite(DamageMultiplier)||DamageMultiplier<1)
                throw new ArgumentException("Invalid plasma bolt snapshot");
        }
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }
    public readonly struct PlasmaBoltContact
    {
        public readonly PlasmaBoltState Bullet;
        public readonly PelletNotice Contact;
        public readonly float AppliedDamage;
        public readonly double Time;
        public PlasmaBoltContact(PlasmaBoltState bullet,PelletNotice contact,float appliedDamage,double time)
        { Bullet=bullet;Contact=contact;AppliedDamage=appliedDamage;Time=time; }
    }
}
