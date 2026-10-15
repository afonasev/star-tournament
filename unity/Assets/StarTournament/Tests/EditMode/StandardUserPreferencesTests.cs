using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class StandardUserPreferencesTests
    {
        [Test] public void ResetAllowListPreservesConsentProgressAndUnconfirmedDisplay()
        {
            var values=new Dictionary<string,int>{
                {NativeUserPreferences.ScaleKey,125},{NativeAudioPreferences.MasterKey,35},
                {"StarTournament.Settings.Gamepad.InvertX",1},{NativeVisualPreferences.Key(VisualEffect.Bloom)+".Strength",25},
                {"StarTournament.Statistics.Consent",1},{"StarTournament.Progress.Record",12},
                {"OtherGame.Sound",15},{"StarTournament.Graphics.Width",1024}};
            int frameRate=Application.targetFrameRate,writes=0;
            try{NativeUserPreferences.Reset(k=>values.Remove(k),()=>writes++);}
            finally{Application.targetFrameRate=frameRate;}
            Assert.That(values.Count,Is.EqualTo(4));Assert.That(writes,Is.EqualTo(1));
            Assert.That(values["StarTournament.Statistics.Consent"],Is.EqualTo(1));
            Assert.That(values["StarTournament.Progress.Record"],Is.EqualTo(12));Assert.That(values["OtherGame.Sound"],Is.EqualTo(15));
            Assert.That(values["StarTournament.Graphics.Width"],Is.EqualTo(1024),"Risky display stays unchanged until its own confirmation");
        }
        [Test] public void ProfileResetKeepsIdentitiesAndSurvivesReload()
        {
            string directory=Path.Combine(Path.GetTempPath(),"standard-profile-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(directory,"profiles.json");
            try
            {
                var defaults=ProvingProfile.CreateDefault();var catalog=new PlayerProfileCatalog(path);
                var a=catalog.Create("Первый",.2f,false);var b=catalog.Create("Второй",.3f,false);
                var look=GamepadLookSettings.Default(defaults);look.InvertX=true;look.InvertY=true;look.Deadzone=.5f;
                catalog.SetGamepad(a.Id,look);catalog.ResetPreferences(defaults);
                var loaded=new PlayerProfileCatalog(path);
                Assert.That(loaded.Profiles.Count,Is.EqualTo(2));Assert.That(loaded.Find(a.Id).Name,Is.EqualTo("Первый"));Assert.That(loaded.Find(b.Id).Name,Is.EqualTo("Второй"));
                Assert.That(loaded.Find(a.Id).ShowFps,Is.True);
                var reset=GamepadLookSettings.Resolve(defaults,loaded.Find(a.Id));
                Assert.That(reset.InvertX||reset.InvertY,Is.False);Assert.That(reset.Deadzone,Is.EqualTo(defaults.Get("input.deadzone")));
                Assert.That(loaded.Find(a.Id).MouseDegreesPerPixel,Is.EqualTo(MouseSensitivityPreference.Descriptor(defaults).DefaultValue));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void FailedProfileResetRestoresPreviousInMemoryPreferences()
        {
            string directory=Path.Combine(Path.GetTempPath(),"standard-profile-"+Guid.NewGuid().ToString("N"));string path=Path.Combine(directory,"profiles.json");
            try
            {
                var catalog=new PlayerProfileCatalog(path);var a=catalog.Create("Сохранённый",.3f,false);
                File.Delete(path);Directory.CreateDirectory(path);
                Assert.Throws<IOException>(()=>catalog.ResetPreferences(ProvingProfile.CreateDefault()));
                Assert.That(catalog.Find(a.Id).MouseDegreesPerPixel,Is.EqualTo(.3f));Assert.That(catalog.Find(a.Id).ShowFps,Is.False);
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void PersonalDeadzoneAndInversionAffectLookButDoNotInvertMovement()
        {
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                var p=ProvingProfile.CreateDefault();var input=new SeatInputCoordinator();input.Assign(0,pad);
                var settings=GamepadLookSettings.Default(p);settings.Deadzone=.8f;
                InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=new Vector2(.25f,0),leftStick=new Vector2(.25f,0)});InputSystem.Update();
                input.Capture(p,.1f,null,_=>settings);var inside=input.Consume(0);
                Assert.That(inside.LookDegrees,Is.EqualTo(Vector2.zero));Assert.That(inside.Move,Is.EqualTo(Vector2.zero));
                InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=new Vector2(.9f,.9f),leftStick=new Vector2(.9f,0)});InputSystem.Update();
                settings.Deadzone=.1f;settings.InvertX=false;settings.InvertY=false;input.Capture(p,.1f,null,_=>settings);var normal=input.Consume(0);
                settings.InvertX=true;settings.InvertY=true;input.Capture(p,.1f,null,_=>settings);var inverted=input.Consume(0);
                Assert.That(inverted.LookDegrees.x,Is.EqualTo(-normal.LookDegrees.x).Within(.0001));Assert.That(inverted.LookDegrees.y,Is.EqualTo(-normal.LookDegrees.y).Within(.0001));
                Assert.That(inverted.Move,Is.EqualTo(normal.Move));Assert.That(inverted.Move.x,Is.GreaterThan(0));
            }
            finally{InputSystem.RemoveDevice(pad);}
        }
        [Test] public void PreferenceMetadataSnapsInvalidScaleSafely()
        {
            Assert.That(NativeUserPreferences.Snap(NativeUserPreferences.ScaleDescriptor,float.NaN),Is.EqualTo(100));
            Assert.That(NativeUserPreferences.Snap(NativeUserPreferences.ScaleDescriptor,500),Is.EqualTo(150));
            Assert.That(NativeUserPreferences.Snap(NativeUserPreferences.ScaleDescriptor,103),Is.EqualTo(105));
        }
    }
}
