namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateNavigationDefault()
        {
            var p = new ProvingProfile { id = "unity-bot-navigation-v1", version = 1 };
            p.Add("bots.navigation.waypointRadius", "bot-navigation", "Достижение точки", "Горизонтальная точность прибытия к углу маршрута.", "meters", .1f, 1, .05f, .25f);
            p.Add("bots.navigation.heightTolerance", "bot-navigation", "Высота опоры", "Допустимая разница высоты стоп и навигационной поверхности.", "meters", .05f, .6f, .01f, .3f);
            p.Add("bots.navigation.sampleDistance", "bot-navigation", "Поиск поверхности", "Радиус проекции стоп на NavMesh; высота проверяется отдельно.", "meters", .1f, 2, .1f, .6f);
            p.Add("bots.navigation.stuckSeconds", "bot-navigation", "Застревание", "Окно без уменьшения оставшегося пути до попытки выхода.", "seconds", .5f, 5, .1f, 1.5f);
            p.Add("bots.navigation.progressMeters", "bot-navigation", "Прогресс", "Минимальное уменьшение оставшегося пути за окно.", "meters", .1f, 2, .1f, .4f);
            p.Add("bots.navigation.recoverySeconds", "bot-navigation", "Уступание", "Длительность бокового выхода обычным движением.", "seconds", .1f, 2, .1f, .7f);
            p.Add("bots.navigation.maximumRecoveries", "bot-navigation", "Попытки выхода", "Предел попыток без прогресса до явного отказа.", "count", 1, 8, 1, 3);
            p.Add("bots.navigation.repathSeconds", "bot-navigation", "Частота маршрута", "Минимальный интервал запросов при обновлении цели.", "seconds", .1f, 2, .1f, .3f);
            p.Add("bots.navigation.slowDistance", "bot-navigation", "Торможение к точке", "Расстояние плавного уменьшения команды у угла пути.", "meters", .2f, 3, .1f, 1);
            return p;
        }
    }
}
