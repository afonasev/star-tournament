using System;
using System.IO;
using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class PlayerProfilesTests
    {
        [Test] public void UsageCountsOnlyChangedAssignmentsAndGuestNumbersSurviveCompaction()
        {
            string dir=Path.Combine(Path.GetTempPath(),"lobby-profiles-"+Guid.NewGuid().ToString("N"));
            try
            {
                var catalog=new PlayerProfileCatalog(Path.Combine(dir,"profiles.json"));
                var a=catalog.Create("А",.2f,true);var z=catalog.Create("Я",.2f,true);var session=new LocalIdentitySession();
                Assert.That(catalog.ByUsage()[0].Id,Is.EqualTo(a.Id));
                session.ChooseProfile(0,1,z.Id,catalog);session.ChooseProfile(0,1,z.Id,catalog);
                Assert.That(z.SelectionCount,Is.EqualTo(1));Assert.That(catalog.ByUsage()[0].Id,Is.EqualTo(z.Id));
                Assert.That(new PlayerProfileCatalog(Path.Combine(dir,"profiles.json")).Find(z.Id).SelectionCount,Is.EqualTo(1));
                var first=session.ChooseGuest(0,1,.2f,true);var second=session.ChooseGuest(1,2,.2f,true);
                Assert.That(first.Number,Is.EqualTo(1));Assert.That(second.Number,Is.EqualTo(2));
                session.RemoveSeat(0,2);Assert.That(session.GuestAt(0),Is.SameAs(second));Assert.That(second.Number,Is.EqualTo(2));
                Assert.That(session.ChooseGuest(1,3,.2f,true).Number,Is.EqualTo(1));
            }
            finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }

        [Test]
        public void LegacyProfileReadPreservesBytesAndUsesGamepadDefaults()
        {
            string directory=Path.Combine(Path.GetTempPath(),"star-tournament-profiles-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(directory,"profiles.json");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(path,"{\"Version\":1,\"Profiles\":[{\"Id\":\"legacy\",\"Name\":\"Игрок\",\"MouseDegreesPerPixel\":0.22,\"ShowFps\":false}]}");
                var bytes=File.ReadAllBytes(path);
                var record=new PlayerProfileCatalog(path).Find("legacy");
                Assert.That(record.MouseDegreesPerPixel,Is.EqualTo(.22f));
                Assert.That(record.ShowFps,Is.False);
                Assert.That(record.GamepadLookVersion,Is.Zero);
                var profile=ProvingProfile.CreateDefault();
                var actual=GamepadLookSettings.Resolve(profile,record);
                var expected=GamepadLookSettings.Default(profile);
                Assert.That(actual,Is.EqualTo(expected));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path),"Opening a profile must not migrate it before startup is confirmed");
                Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
            }
            finally {Directory.Delete(directory,true);}
        }

        [TestCase(0)] [TestCase(2)]
        public void UnsupportedProfileVersionFailsWithoutOverwritingSource(int version)
        {
            string directory=Path.Combine(Path.GetTempPath(),"star-tournament-profiles-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(directory,"profiles.json");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(path,"{\"Version\":"+version+",\"Profiles\":[]}");
                var bytes=File.ReadAllBytes(path);
                Assert.Throws<InvalidDataException>(()=>new PlayerProfileCatalog(path));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
                Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
            }
            finally {Directory.Delete(directory,true);}
        }

        [Test]
        public void ProfilePersistsWhileGuestAndDevicePreferenceRemainInSession()
        {
            string directory=Path.Combine(Path.GetTempPath(),"star-tournament-profiles-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(directory,"profiles.json");
            try
            {
                var first=new PlayerProfileCatalog(path);
                var alex=first.Create("Алексей",.18f,true);
                first.Rename(alex.Id,"Александр");first.SetPersonal(alex.Id,.22f,false);
                var session=new LocalIdentitySession();
                Assert.That(session.ChooseProfile(0,101,alex.Id,first),Is.True);
                Assert.That(session.ChooseProfile(1,102,alex.Id,first),Is.False,"One profile cannot occupy two seats");
                var guest=session.ChooseGuest(1,102,.18f,true);guest.MouseDegreesPerPixel=.27f;
                session.ClearSeat(1);Assert.That(session.ChooseGuest(1,102,.18f,true),Is.SameAs(guest));
                session.ClearSeat(0);Assert.That(session.TryRestore(0,101,first),Is.True);
                var reopened=new PlayerProfileCatalog(path);
                Assert.That(reopened.Find(alex.Id).Name,Is.EqualTo("Александр"));
                Assert.That(reopened.Find(alex.Id).MouseDegreesPerPixel,Is.EqualTo(.22f).Within(.001f));
                Assert.That(reopened.Find(alex.Id).ShowFps,Is.False);
                var nextSession=new LocalIdentitySession();
                Assert.That(nextSession.TryRestore(0,101,reopened),Is.False,"Device preference is not durable");
                Assert.That(nextSession.GuestAt(1),Is.Null,"Guest data is not durable");
                reopened.Delete(alex.Id);Assert.That(new PlayerProfileCatalog(path).Find(alex.Id),Is.Null);
            }
            finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
        }
    }
}
