using System.Text.RegularExpressions;
namespace StarTournament.ProvingGround
{
    // Presentation aliases only: IDs, resource paths and immutable Lab metadata stay exact.
    public static class GameDisplayNames
    {
        static readonly Regex Aliases=new Regex(@"Combat Bowl|Технические тоннели|Лунная лаборатория|Колодец|Колодца|Колодце|COBALT|VECTOR|Vector|FORGE|Forge|Crimson|CRIMSON|Pulse|PULSE|Винтовка|винтовка|винтовки|Винтовки|Резаком|резаком|Резаку|резаку|Резака|резака|Резак|резак");
        public static string Text(string value)=>value==null?null:Aliases.Replace(value,m=>Name(m.Value));
        static string Name(string value)
        {
            switch(value)
            {
                case "Combat Bowl":return "Орбита";
                case "Технические тоннели":return "Тоннели";
                case "Лунная лаборатория":return "Луна-9";
                case "Колодец":return "Станция Вега";
                case "Колодца":case "Колодце":return "Станции Вега";
                case "COBALT":case "Винтовка":return "Автомат";
                case "винтовка":return "автомат";
                case "винтовки":return "автомата";
                case "Винтовки":return "Автомата";
                case "VECTOR":case "Vector":return "Дробовик";
                case "FORGE":case "Forge":return "Плазмомёт";
                case "Crimson":case "CRIMSON":case "Pulse":case "PULSE":return "Ракетница";
                case "Резаком":return "Лазером";case "резаком":return "лазером";
                case "Резаку":return "Лазеру";case "резаку":return "лазеру";
                case "Резака":return "Лазера";case "резака":return "лазера";
                case "Резак":return "Лазер";default:return "лазер";
            }
        }
    }
}
