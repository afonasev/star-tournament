using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Approved equal viewport topology. Fractions are structural, not tunable camera parameters.</summary>
    public static class LocalSeatLayout
    {
        public static Rect Viewport(int count, int seat)
        {
            if(count<1 || count>SeatInputCoordinator.SeatCount || seat<0 || seat>=count)
                throw new ArgumentOutOfRangeException();
            if(count==1) return new Rect(0,0,1,1);
            if(count==2) return new Rect(seat*.5f,0,.5f,1);
            return new Rect(seat%2*.5f,seat<2?.5f:0,.5f,.5f);
        }
        public static Rect? PersistentStandings(int count) => count==3 ? new Rect(.5f,0,.5f,.5f) : (Rect?)null;
    }
}
