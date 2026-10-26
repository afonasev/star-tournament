using System.Reflection;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    // Legacy multi-seat scenarios explicitly author their fixture, independent of the new-match default.
    static class NativeSetupFixture
    {
        public static void UnboundHumans(ProvingGround ground,int count=4)
        {
            while(ground.SetupBotCount>0)ground.RemoveBot(0);
            typeof(ProvingGround).GetMethod("SetSeatCount",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{count});
            var input=(SeatInputCoordinator)typeof(ProvingGround).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);input.Reset();
            var identities=(LocalIdentitySession)typeof(ProvingGround).GetField("identities",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            for(int seat=0;seat<count;seat++)identities.ClearSeat(seat);
        }
        public static void GuestIdentity(ProvingGround ground,int seat,UnityEngine.InputSystem.InputDevice device)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var identities=(LocalIdentitySession)typeof(ProvingGround).GetField("identities",flags).GetValue(ground);
            var fps=(FpsDisplay)typeof(ProvingGround).GetField("fps",flags).GetValue(ground);
            identities.ChooseGuest(seat,device.deviceId,MouseSensitivityPreference.Resolve(ground.Profile),fps.Visible);
            typeof(ProvingGround).GetMethod("RefreshInterface",flags).Invoke(ground,null);
        }
    }
}
