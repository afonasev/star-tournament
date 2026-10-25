using System;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class NativeBackgroundPolicyContractTests
    {
        [Test] public void AlreadyAccessoryNeverSwitchesAgain(){int calls=0;var e=RenderingBaselineMacBackground.EnsureAccessory(()=>1,()=>{calls++;return false;});Assert.AreEqual(0,calls);Assert.AreEqual(1,e.before);Assert.AreEqual(1,e.after);Assert.AreEqual("not-called-already-accessory",e.switchReturn);}
        [Test] public void SwitchMustBeConfirmedByGetter(){long policy=0;var e=RenderingBaselineMacBackground.EnsureAccessory(()=>policy,()=>{policy=1;return true;});Assert.AreEqual(0,e.before);Assert.AreEqual("true",e.switchReturn);Assert.AreEqual(1,e.after);}
        [Test] public void FailedActualPolicyCannotPass(){Assert.Throws<InvalidOperationException>(()=>RenderingBaselineMacBackground.EnsureAccessory(()=>0,()=>false));}
    }
}
