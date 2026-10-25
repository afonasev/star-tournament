using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class PlasmaInputTests : InputTestFixture
    {
        [Test] public void FifthKeySelectsPlasmaForItsOwnerOnce()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.AddDevice<Mouse>();var pad=InputSystem.AddDevice<Gamepad>();
            var seats=new SeatInputCoordinator();Assert.That(seats.Assign(0,keyboard),Is.True);Assert.That(seats.Assign(1,pad),Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit5));InputSystem.Update();seats.Capture(ProvingProfile.CreateDefault(),.02f);
            Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.Plasma));Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.None));Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.None));
        }
    }
    public sealed class PlasmaBotTests
    {
        [Test] public void ExpectedDamageUsesPlasmaProfileChargeAndFiniteTravel()
        {
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();var life=ProvingProfile.CreateCombatDefault();var plasma=ProvingProfile.CreatePlasmaDefault();
            var enemy=new NativeBotMemoryEntry{Visible=true,Sighting=new NativeBotSighting(1,1,Vector3.forward*10)};
            var state=new CombatLife("bot",life).Read();state.PlasmaOwned=true;state.PlasmaAmmo=100;state.SelectedWeapon=WeaponId.Plasma;state.Ammo=100;
            var frame=new NativeBotFrame{Pose=new ParticipantState(),Life=state,Knowledge=new NativeBotKnowledge{Enemies=new[]{enemy}}};
            float Score(ProvingProfile p)=>new NativeShotgunPolicy(move,combat,lifecycle:life,plasma:p).Estimate.Estimate(WeaponId.Plasma,frame,enemy,0,0,1,16,32,230,0,.7f,1);
            float ready=Score(plasma);Assert.That(ready,Is.GreaterThan(0));plasma.Set("plasma.damage",1);Assert.That(Score(plasma),Is.LessThan(ready));
            plasma=ProvingProfile.CreatePlasmaDefault();plasma.Set("plasma.speed",1);Assert.That(Score(plasma),Is.LessThan(ready));frame.Life.PlasmaAmmo=0;Assert.That(Score(plasma),Is.Zero);
        }
    }
}
