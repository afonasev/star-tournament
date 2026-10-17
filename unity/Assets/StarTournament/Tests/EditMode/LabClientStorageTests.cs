using System;
using System.IO;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabClientStorageTests
    {
        string directory,project;
        [SetUp]public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-client-"+Guid.NewGuid().ToString("N"));project=Path.Combine(directory,"unity");
            Directory.CreateDirectory(Path.Combine(project,"Assets","StarTournament"));File.WriteAllText(Path.Combine(directory,".git"),"gitdir: fixture");
        }
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        [Test]public void EditorDefaultsToItsProjectWithoutWritingData()
        {Assert.That(LabProjectStorage.SelectProject(true,true,project,Array.Empty<string>(),null,null),Is.EqualTo(project));Assert.That(File.Exists(LabProjectStorage.ResolvePath(project)),Is.False);}
        [TestCase("-labReview")][TestCase("-colorIdentityReview")]
        public void ReviewOverridesKeepProductionStorage(string flag)
        {Assert.That(LabProjectStorage.SelectProject(true,true,project,new[]{flag},null,null),Is.Null);}
        [Test]public void IsolatedQaHistoryDisablesAutomaticProjectButExplicitQaProjectWins()
        {
            Assert.That(LabProjectStorage.SelectProject(true,true,project,Array.Empty<string>(),"owned-history",null),Is.Null);
            Assert.That(LabProjectStorage.SelectProject(true,true,project,Array.Empty<string>(),"owned-history",project),Is.EqualTo(project));
        }
        [Test]public void DevelopmentPlayerRequiresExplicitProjectAndValidatesItsArgument()
        {
            Assert.That(LabProjectStorage.SelectProject(false,true,project,Array.Empty<string>(),null,null),Is.Null);
            Assert.That(LabProjectStorage.SelectProject(false,true,project,new[]{"-labProject",project},"owned-history",null),Is.EqualTo(project));
            Assert.Throws<ArgumentException>(()=>LabProjectStorage.SelectProject(false,true,project,new[]{"-labProject"},null,null));
            Assert.Throws<ArgumentException>(()=>LabProjectStorage.SelectProject(false,true,project,new[]{"-labProject","relative"},null,null));
        }
        [Test]public void ProductionIgnoresAllAuthoringInputsBeforeTouchingTheirPaths()
        {Assert.That(LabProjectStorage.SelectProject(false,false,"missing",new[]{"-labProject"},null,"missing"),Is.Null);}
        [Test]public void ExplicitBootstrapPersistsSeedWithoutChangingItsContentOrPublishing()
        {
            var b=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()}};var c=LabReleaseCatalog.Factory(b);
            var h=LabProjectStorage.Open(project,b,c);string hash=h.Selected.Hash;Assert.That(File.Exists(h.StoragePath),Is.False);
            h.PersistAuthoring();Assert.That(File.Exists(h.StoragePath),Is.True);Assert.That(LabProjectStorage.Open(project,b,c).Selected.Hash,Is.EqualTo(hash));
            var prod=new DesignLabHistory(Path.Combine(directory,"player.json"),b,releases:c,persistMigration:false);
            Assert.Throws<InvalidOperationException>(()=>prod.PersistAuthoring());Assert.That(File.Exists(Path.Combine(directory,"player.json")),Is.False);
        }
    }
}
