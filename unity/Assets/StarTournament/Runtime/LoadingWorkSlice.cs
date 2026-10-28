using System.Diagnostics;
namespace StarTournament.ProvingGround
{
    // Scheduling budget only: never changes construction order, assets or simulation.
    internal sealed class LoadingWorkSlice
    {
        readonly Stopwatch watch=Stopwatch.StartNew();
        public bool Expired=>watch.Elapsed.TotalMilliseconds>=4;
        // Restart after the yield so time spent waiting for a frame is not charged as work.
        public void Restart()=>watch.Restart();
    }
}
