namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateRosterDefault()
        {
            var p=new ProvingProfile{id="unity-native-roster-v1",version=1};
            p.Add("spawn.initialSearchBudgetNodes","spawn","Бюджет стартовой расстановки","Максимум проверяемых узлов поиска; исчерпание возвращает явную ошибку, а не частичную расстановку.","nodes",100,1000000,100,100000);
            p.Add("spawn.initialCandidateBudget","spawn","Бюджет стартовых кандидатов","Ограничивает generation/physics queries и размер pair tables; превышение даёт явную ошибку до поиска.","candidates",32,4096,32,1024);
            return p;
        }
    }
}
