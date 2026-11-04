using System;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class EmptyLobbyDraftTests
    {
        [Test] public void RemovingLastSeatReleasesDeviceIdentityAndSetupState()
        {
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                var input=new SeatInputCoordinator();input.SetActiveSeatCount(1);Assert.That(input.Assign(0,pad),Is.True);
                var identities=new LocalIdentitySession();identities.ChooseGuest(0,pad.deviceId,.2f,false);
                var bots=new NativeBotSetup();bots.SetSeatDifficulty(0,2);
                input.RemoveSeat(0);identities.RemoveSeat(0,1);bots.RemoveSeat(0,1);
                Assert.That(input.ActiveSeatCount,Is.Zero);Assert.That(input.DeviceAt(0),Is.Null);Assert.That(input.Ready,Is.False);
                Assert.That(identities.HasIdentity(0),Is.False);Assert.That(bots.HumanCount(0),Is.Zero);
                Assert.That(input.Assign(0,pad),Is.False,"No hidden seat can capture the free device");
                input.SetActiveSeatCount(1);Assert.That(input.Assign(0,pad),Is.True);Assert.That(input.Ready,Is.True);
                Assert.That(bots.SeatDifficulty(0),Is.EqualTo(1));
                input.SetActiveSeatCount(0);Assert.That(input.Ready,Is.False);
                Assert.Throws<ArgumentOutOfRangeException>(()=>input.SetActiveSeatCount(-1));
                Assert.Throws<ArgumentOutOfRangeException>(()=>input.SetActiveSeatCount(5));
            }
            finally{InputSystem.RemoveDevice(pad);}
        }
        [Test] public void ZeroViewDraftCanRetainBotsButCannotBuildMatch()
        {
            var draft=new NativeBotSetup();Assert.That(draft.CanAdd(0),Is.True);draft.Add(0);
            Assert.That(draft.Count,Is.EqualTo(1));Assert.That(draft.HumanCount(0),Is.Zero);
            var teams=new[]{NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamA,NativeTeam.TeamB};
            Assert.Throws<ArgumentException>(()=>draft.Build(0,NativeMatchMode.Ffa,teams,false),"Editable state must not weaken the runtime composition contract");
            draft.SetSeatBot(0,draft.At(0));draft.RemoveSeat(0,1);
            Assert.That(draft.IsAi(0),Is.False);Assert.That(draft.Count,Is.EqualTo(1),"Deleting a local view leaves additional bots unchanged");
        }
    }
}
