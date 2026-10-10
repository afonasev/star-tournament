using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class GamepadLookSettingsTests
    {
        [Test] public void LegacyIdentityDefaultsAndNewSettingsPersistWithoutAffectingOtherIdentity()
        {
            var path=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".json");
            try
            {
                var p=ProvingProfile.CreateDefault();var c=new PlayerProfileCatalog(path);
                var first=c.Create("Первый",.12f,true);var second=c.Create("Второй",.2f,false);
                Assert.That(GamepadLookSettings.Resolve(p,first).AutoLevel,Is.True);
                c.SetGamepad(first.Id,new GamepadLookSettings{Horizontal=120,Vertical=240,AutoLevel=false,ReturnDelay=1.25f});
                var restored=new PlayerProfileCatalog(path);
                var look=GamepadLookSettings.Resolve(p,restored.Find(first.Id));
                Assert.That(look.Horizontal,Is.EqualTo(120));Assert.That(look.Vertical,Is.EqualTo(240));Assert.That(look.AutoLevel,Is.False);Assert.That(look.ReturnDelay,Is.EqualTo(1.25f));
                var legacy=new PlayerProfileRecord{GamepadLookVersion=1,GamepadHorizontal=120,GamepadVertical=240};
                Assert.That(GamepadLookSettings.Resolve(p,legacy).ReturnDelay,Is.EqualTo(p.Get(GamepadLookSettings.DelayPath)));
                Assert.That(GamepadLookSettings.Resolve(p,restored.Find(second.Id)).Horizontal,Is.EqualTo(p.Get(GamepadLookSettings.HorizontalPath)));
                Assert.That(restored.Find(first.Id).MouseDegreesPerPixel,Is.EqualTo(.12f));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void SeparateAxesAndHeldIntentSurviveConsumptionButClearOnPauseAndDisconnect()
        {
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                var seats=new SeatInputCoordinator();seats.Assign(0,pad);var p=ProvingProfile.CreateDefault();
                InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=new Vector2(1,1)});InputSystem.Update();
                seats.Capture(p,1f,null,_=>new GamepadLookSettings{Horizontal=100,Vertical=200,AutoLevel=true});
                var a=seats.Consume(0);Assert.That(a.LookDegrees.y,Is.EqualTo(a.LookDegrees.x*2).Within(.001));
                Assert.That(a.ManualLook,Is.True);Assert.That(a.GamepadLookAssistance,Is.True);
                var held=seats.Consume(0);Assert.That(held.LookDegrees,Is.EqualTo(Vector2.zero));Assert.That(held.ManualLook,Is.True);
                seats.Clear();Assert.That(seats.Consume(0).GamepadLookAssistance,Is.False);
                seats.Capture(p,.1f);InputSystem.RemoveDevice(pad);seats.Capture(p,.1f);
                Assert.That(seats.Consume(0).GamepadLookAssistance,Is.False);
            }
            finally{if(pad.added)InputSystem.RemoveDevice(pad);}
        }
        [Test] public void SurfacePitchProjectsSlopeOntoYawAndNeverRolls()
        {
            var normal=new Vector3(0,1,-.5f).normalized;
            Assert.That(CharacterMotor.SurfacePitch(normal,Vector3.forward),Is.EqualTo(-Mathf.Atan(.5f)*Mathf.Rad2Deg).Within(.001));
            Assert.That(CharacterMotor.SurfacePitch(normal,Vector3.back),Is.GreaterThan(0));
            Assert.That(CharacterMotor.SurfacePitch(normal,Vector3.right),Is.EqualTo(0));
            Assert.That(CharacterMotor.SurfacePitch(Vector3.up,Vector3.forward),Is.EqualTo(0));
        }
    }
}
