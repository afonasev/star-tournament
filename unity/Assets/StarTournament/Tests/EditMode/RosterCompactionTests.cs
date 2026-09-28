using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class RosterCompactionTests
    {
        [Test] public void RemovalPreservesDisconnectedDeviceAndGuestIdentity()
        {
            var first=InputSystem.AddDevice<Gamepad>();var second=InputSystem.AddDevice<Gamepad>();
            try
            {
                var input=new SeatInputCoordinator();input.SetActiveSeatCount(3);Assert.That(input.Assign(0,first),Is.True);Assert.That(input.Assign(1,second),Is.True);input.SetHumanSeat(2,false);
                var identity=new LocalIdentitySession();var guest=identity.ChooseGuest(1,second.deviceId,.5f,true);
                var setup=new NativeBotSetup();setup.SetAi(2,true);setup.SetSeatDifficulty(2,2);
                InputSystem.DisableDevice(second);
                input.RemoveSeat(0);identity.RemoveSeat(0,3);setup.RemoveSeat(0,3);
                Assert.That(input.ActiveSeatCount,Is.EqualTo(2));Assert.That(input.DeviceAt(0),Is.SameAs(second));Assert.That(input.Ready,Is.False);
                Assert.That(identity.GuestAt(0),Is.SameAs(guest));Assert.That(identity.HasIdentity(1),Is.False);
                Assert.That(input.IsHumanSeat(1),Is.False);Assert.That(setup.IsAi(1),Is.True);Assert.That(setup.SeatDifficulty(1),Is.EqualTo(2));
                InputSystem.EnableDevice(second);Assert.That(input.Ready,Is.True);
            }
            finally {InputSystem.RemoveDevice(first);InputSystem.RemoveDevice(second);}
        }
        [Test] public void ReplacementRefusesOccupiedDeviceWithoutLosingAssignment()
        {
            var a=InputSystem.AddDevice<Gamepad>();var b=InputSystem.AddDevice<Gamepad>();var c=InputSystem.AddDevice<Gamepad>();
            try
            {
                var input=new SeatInputCoordinator();input.SetActiveSeatCount(2);input.Assign(0,a);input.Assign(1,b);
                Assert.That(input.Replace(0,b),Is.False);Assert.That(input.DeviceAt(0),Is.SameAs(a));Assert.That(input.Replace(0,c),Is.True);Assert.That(input.DeviceAt(1),Is.SameAs(b));
            }
            finally {InputSystem.RemoveDevice(a);InputSystem.RemoveDevice(b);InputSystem.RemoveDevice(c);}
        }
        [Test] public void ProfileMovesAndRemainsUniqueAfterCompactionAndRebinding()
        {
            var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
            try
            {
                var catalog=new PlayerProfileCatalog(path);var profile=catalog.Create("Игрок",.3f,true);var identities=new LocalIdentitySession();
                Assert.That(identities.ChooseProfile(1,11,profile.Id,catalog),Is.True);identities.RemoveSeat(0,2);
                Assert.That(identities.ProfileId(0),Is.EqualTo(profile.Id));Assert.That(identities.ChooseProfile(1,12,profile.Id,catalog),Is.False);
                identities.RememberBinding(0,13);identities.ClearSeat(0);Assert.That(identities.TryRestore(1,13,catalog),Is.True);
            }
            finally {if(File.Exists(path))File.Delete(path);}
        }
    }
}
