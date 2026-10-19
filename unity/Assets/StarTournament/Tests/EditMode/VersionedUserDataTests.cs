using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class VersionedUserDataTests
    {
        string directory,legacy,current;
        static LabBundle Small()=>new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()}};
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-versioned-data-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);legacy=Path.Combine(directory,"old.json");current=Path.Combine(directory,"new.json");
        }
        [TearDown] public void Cleanup(){Directory.Delete(directory,true);}
        void LegacyHistory()
        {
            var seed=new DesignLabHistory(legacy,Small());seed.Create("Legacy");
            var data=typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(seed);
            File.WriteAllText(legacy,JsonUtility.ToJson(data,true));
        }
        [Test] public void ProfilesImportWithoutWritingAndSaveOnlyNewNamespace()
        {
            var old=new PlayerProfileCatalog(legacy);var record=old.Create("Previous",.22f,true);
            var bytes=File.ReadAllBytes(legacy);var next=new PlayerProfileCatalog(current,legacy);
            Assert.That(next.Find(record.Id).Name,Is.EqualTo("Previous"));Assert.That(File.Exists(current),Is.False);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
            next.Rename(record.Id,"New");CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
            Assert.That(new PlayerProfileCatalog(legacy).Find(record.Id).Name,Is.EqualTo("Previous"));
            old.Rename(record.Id,"Edited after rollback");
            Assert.That(new PlayerProfileCatalog(current,legacy).Find(record.Id).Name,Is.EqualTo("New"),"Existing namespace is authoritative; no silent merge");
        }
        [Test] public void InvalidProfileDestinationDoesNotFallBackToLegacy()
        {
            new PlayerProfileCatalog(legacy).Create("Previous",.22f,true);var bytes=File.ReadAllBytes(legacy);
            File.WriteAllText(current,"{\"Version\":2,\"Profiles\":[]}");var invalid=File.ReadAllBytes(current);
            Assert.Throws<InvalidDataException>(()=>new PlayerProfileCatalog(current,legacy));
            CollectionAssert.AreEqual(invalid,File.ReadAllBytes(current));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
        }
        [Test] public void FailedProfilePublicationPreservesLegacyAndRemovesTemporaryFile()
        {
            var old=new PlayerProfileCatalog(legacy);var record=old.Create("Previous",.22f,true);
            var bytes=File.ReadAllBytes(legacy);var next=new PlayerProfileCatalog(current,legacy);
            Directory.CreateDirectory(current);
            Assert.Throws<IOException>(()=>next.Rename(record.Id,"New"));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
            Assert.That(Directory.GetFiles(directory,"*.tmp"),Is.Empty);
            Assert.That(Directory.GetFileSystemEntries(current),Is.Empty);
        }
        [Test] public void LabImportsLegacyWithoutWritingAndKeepsPreviousNamespaceAfterSave()
        {
            LegacyHistory();var bytes=File.ReadAllBytes(legacy);
            var next=new DesignLabHistory(current,Small(),persistMigration:false,legacyPath:legacy);
            Assert.That(next.StorageError,Is.Null);Assert.That(next.SelectedProfile.Name,Is.EqualTo("Legacy"));
            Assert.That(File.Exists(current),Is.False);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
            next.Create("New");CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
            Assert.That(JsonUtility.FromJson<LabParameterFile>(File.ReadAllText(current)).Schema,Is.EqualTo(2));
            Assert.That(new DesignLabHistory(legacy,Small(),persistMigration:false).SelectedProfile.Name,Is.EqualTo("Legacy"));
            File.WriteAllText(legacy,"unreadable older namespace");
            var reopened=new DesignLabHistory(current,Small(),persistMigration:false,legacyPath:legacy);
            Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.SelectedProfile.Name,Is.EqualTo("New"));
        }
        [Test] public void FailedLabImportPublicationPreservesLegacyAndCanRetry()
        {
            LegacyHistory();var bytes=File.ReadAllBytes(legacy);bool fail=true;
            var next=new DesignLabHistory(current,Small(),persistMigration:false,legacyPath:legacy,
                atomicPublish:(a,z)=>{if(fail)throw new IOException("injected publish failure");File.Move(a,z);});
            Assert.Throws<IOException>(()=>next.Create("New"));Assert.That(next.SelectedProfile.Name,Is.EqualTo("Legacy"));
            Assert.That(File.Exists(current),Is.False);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
            Assert.That(Directory.GetFiles(directory,"*.tmp"),Is.Empty);
            fail=false;next.Create("New");Assert.That(File.Exists(current),Is.True);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
        }
        [Test] public void InvalidLabDestinationDoesNotFallBackToLegacy()
        {
            LegacyHistory();var bytes=File.ReadAllBytes(legacy);File.WriteAllText(current,"{\"Schema\":99}");var invalid=File.ReadAllBytes(current);
            var next=new DesignLabHistory(current,Small(),persistMigration:false,legacyPath:legacy);
            Assert.That(next.StorageError,Is.Not.Null);Assert.Throws<IOException>(()=>next.Create("New"));
            CollectionAssert.AreEqual(invalid,File.ReadAllBytes(current));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(legacy));
        }
        [Test] public void DirectoryDestinationCannotSelectLegacyFile()
        {
            File.WriteAllText(legacy,"untouched");Directory.CreateDirectory(current);
            Assert.Throws<IOException>(()=>VersionedUserData.ReadPath(current,legacy));Assert.That(File.ReadAllText(legacy),Is.EqualTo("untouched"));
        }
    }
}
