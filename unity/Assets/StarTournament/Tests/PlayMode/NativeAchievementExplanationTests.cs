using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeAchievementExplanationTests
    {
        static readonly string[] Ids={"slowpoke","jumper","bad-friend","diamond-eye","cemetery-sponsor","first-pancake","humanitarian","walking-target","pharmacy-magnate","weapon-sommelier","trainee","own-pain","no-help-needed","all-mine","warning-fire","cardio","almost-dangerous","assistant-assistant","armor-didnt-help","greed","own-opponent","jumped-to-end","noise-force","bad-trade","pacifist","enemy-within","why-ammo","worst-own-enemy","team-saboteur","collector"};
        [Test]
        public void EveryCatalogAwardHasAnAuthoredNeutralOrMasculineFullCondition()
        {
            Assert.That(Ids.Length,Is.EqualTo(30));
            for(int i=0;i<Ids.Length;i++)
            {
                Assert.That(NativeAchievementCatalog.IsKnown(Ids[i]),Is.True,Ids[i]);
                var explanation=NativeAchievementsView.ExplanationFor(Ids[i]);
                Assert.That(explanation,Is.Not.Null.And.Not.Empty,Ids[i]);
                Assert.That(explanation,Does.Not.Contain("Награда сохранена"),Ids[i]);
            }
            Assert.That(NativeAchievementsView.ExplanationFor("first-pancake"),Does.Contain("Первая смерть"));
            Assert.That(NativeAchievementsView.ExplanationFor("warning-fire"),Does.Contain("выстрелов").And.Contain("убийств"));
            Assert.That(NativeAchievementsView.ExplanationFor("cardio"),Does.Contain("расстояния").And.Contain("убийств"));
            Assert.That(NativeAchievementsView.ExplanationFor("almost-dangerous"),Does.Contain("минимумом убийств"));
            Assert.That(NativeAchievementsView.ExplanationFor("diamond-eye"),Does.Contain("Самая низкая точность").And.Contain("достаточной стрельбе"));
            Assert.That(NativeAchievementsView.ExplanationFor("noise-force"),Does.Contain("выстрелов").And.Contain("низкая точность").And.Contain("достаточной стрельбе"));
            Assert.That(NativeAchievementsView.ExplanationFor("bad-trade"),Does.Contain("от врагов").And.Contain("урона врагам"));
            Assert.That(NativeAchievementsView.ExplanationFor("pacifist"),Does.Contain("ассистов").And.Contain("убийств").And.Contain("урона врагам"));
            Assert.That(NativeAchievementsView.ExplanationFor("enemy-within"),Does.Contain("урона союзникам").And.Contain("меньше всех урона врагам"));
            Assert.That(NativeAchievementsView.ExplanationFor("why-ammo"),Does.Contain("выстрелов").And.Contain("Достаточно стрелял").And.Contain("точности").And.Contain("урона врагам"));
            Assert.That(NativeAchievementsView.ExplanationFor("collector"),Does.Contain("бонусов").And.Contain("расстояния").And.Contain("убийств"));
            foreach(string id in new[]{"pacifist","enemy-within","why-ammo","worst-own-enemy","team-saboteur","collector"})
                Assert.That(NativeAchievementsView.ExplanationFor(id),Does.Not.Contain("Ноль").And.Not.Contain("ноль"));
            Assert.That(NativeAchievementsView.ExplanationFor("why-ammo",1),Does.Contain("ноль попаданий"));
            Assert.That(NativeAchievementsView.ExplanationFor("pacifist",1),Does.Contain("Ноль убийств"));
        }

        [Test]
        public void UnknownAwardKeepsItsSnapshotFactWithoutInventingEligibility()
        {
            Assert.That(NativeAchievementsView.ExplanationFor("future-award"),Is.EqualTo("Награда сохранена в итогах матча."));
        }
    }
}
