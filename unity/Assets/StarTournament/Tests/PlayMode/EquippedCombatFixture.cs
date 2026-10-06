namespace StarTournament.ProvingGround.Tests.PlayMode
{
    /// <summary>Explicit equipped snapshot for isolated ballistic tests; map pickup behavior has separate physical fixtures.</summary>
    static class EquippedCombatFixture
    {
        public static void Equip(NativeCombatSession session)
        {
            var snapshot=session.Capture();
            for(int i=0;i<snapshot.Lives.Length;i++)
            {
                snapshot.Lives[i].ShotgunOwned=snapshot.Lives[i].RocketOwned=snapshot.Lives[i].CutterOwned=true;
                snapshot.Lives[i].ShotgunAmmo=snapshot.Lives[i].RocketAmmo=20;snapshot.Lives[i].CutterEnergy=30;
            }
            session.Restore(snapshot);
        }
    }
}
