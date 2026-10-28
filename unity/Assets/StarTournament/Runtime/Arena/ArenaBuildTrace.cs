using System;
using System.Diagnostics;
namespace StarTournament.ProvingGround
{
    // Opt-in Editor/development diagnostic. Wall times include coroutine frame waits.
    internal sealed class ArenaBuildTrace
    {
        readonly string owner;readonly Stopwatch watch;
        public ArenaBuildTrace(string owner)
        {
            this.owner=owner;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LOADING_PHASES")=="1")watch=Stopwatch.StartNew();
#endif
        }
        public void Mark(string phase)
        {
            if(watch==null)return;
            UnityEngine.Debug.Log($"ARENA_LOAD_PHASE owner={owner} phase={phase} ms={watch.Elapsed.TotalMilliseconds:F3}");watch.Restart();
        }
    }
}
