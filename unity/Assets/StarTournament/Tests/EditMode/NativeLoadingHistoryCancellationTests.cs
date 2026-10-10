using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeLoadingHistoryCancellationTests
    {
        [Test] public void CancelledStartupDoesNotReadOrCreateHistory()
        {
            string path=Path.Combine(Path.GetTempPath(),"cancelled-loading-"+Guid.NewGuid().ToString("N")+".json");
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(()=>new DesignLabHistory(path,new LabBundle(),cancellationToken:cancellation.Token));
                Assert.That(File.Exists(path),Is.False);
            }
        }
    }
}
