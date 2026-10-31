using System;
using System.Threading.Tasks;
namespace StarTournament.ProvingGround
{
    // Only independent pixel math goes here. Unity texture creation/upload stays on
    // the calling main thread. PerlinNoise's native binding is explicitly thread-safe.
    internal static class ArenaTextureWork
    {
        static readonly ParallelOptions Options=new ParallelOptions{MaxDegreeOfParallelism=Math.Max(1,Math.Min(4,Environment.ProcessorCount-1))};
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static bool ParallelEnabled=true;
#endif
        public static void Rows(int count,Action<int> row)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!ParallelEnabled){for(int y=0;y<count;y++)row(y);return;}
#endif
            if(Options.MaxDegreeOfParallelism==1){for(int y=0;y<count;y++)row(y);return;}
            Parallel.For(0,count,Options,row);
        }
    }
}
