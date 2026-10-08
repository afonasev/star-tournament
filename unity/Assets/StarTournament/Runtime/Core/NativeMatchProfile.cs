using System;
using System.Linq;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateMatchDefault()
        {
            var p = new ProvingProfile { id = "unity-native-match-v1", version = 2 };
            // Baseline: released prototype-v1@12. Ranges are shared by setup and validation.
            p.Add("match.durationMinutes", "match", "Длительность", "Основное время; равенство запускает overtime.", "minutes", 1, 30, 1, 5);
            p.Add("match.targetPoints", "match", "Цель по очкам", "Используется только при включённой цели.", "points", 1000, 20000, 100, 3000);
            p.Add("match.ffaTargetDefault", "match", "Цель: каждый сам за себя", "Начальная цель при выборе одиночного режима.", "points", 1000, 20000, 100, 2000);
            p.Add("match.teamTargetDefault", "match", "Цель: командный бой", "Начальная цель при выборе командного режима.", "points", 1000, 20000, 100, 3500);
            p.Add("score.assistWindow", "scoring", "Окно помощи", "Последний положительный урон учитывается включительно.", "seconds", 0, 60, 1, 5);
            p.Add("score.assistPoints", "scoring", "Очки помощи", "Награда один раз за чужое убийство.", "points", 0, 1000, 1, 50);
            int[] totals = { 100, 300, 500, 800, 1200 };
            for (int i = 0; i < totals.Length; i++)
                p.Add("score.chainTotal"+(i+1), "scoring", "Итог серии "+(i+1), "Накопительная награда серии без assists.", "points", 0, 20000, 1, totals[i]);
            p.Add("score.chainIncrement", "scoring", "После пятого", "Дополнительная награда за каждое следующее убийство.", "points", 0, 20000, 1, 400);
            p.Add("score.friendlyOrSelfKillPenalty", "scoring", "Штраф за убийство себя/союзника", "Вычитание из личного счёта за каждую такую смерть; матч сохраняет выбранное значение.", "points", 0, 20000, 1, 200);
            p.Add("achievement.minimumShots", "match", "Выстрелы для достижений", "Минимум фактических выстрелов для точностных номинаций.", "shots", 1, 1000, 1, 10);
            p.Add("achievement.minimumBeamSeconds", "match", "Время луча для достижений", "Минимальная длительность луча для точностных номинаций.", "seconds", .1f, 60, .1f, 1);
            return p;
        }
    }

    [Serializable]
    public struct NativeMatchConfiguration
    {
        public int DurationMinutes;
        public bool TargetEnabled;
        public int TargetPoints;
        public static NativeMatchConfiguration Default(ProvingProfile p) => new NativeMatchConfiguration
        { DurationMinutes = (int)p.Get("match.durationMinutes"), TargetPoints = (int)p.Get("match.targetPoints") };
        public void Validate(ProvingProfile p)
        {
            ValidateValue(p, "match.durationMinutes", DurationMinutes);
            ValidateValue(p, "match.targetPoints", TargetPoints);
        }
        public static void ValidateValue(ProvingProfile p, string path, float value)
        {
            var d = p.Descriptors.First(x => x.Path == path);
            if (!d.Contains(value) || Math.Abs((value-d.Minimum)/d.Step-Math.Round((value-d.Minimum)/d.Step)) > .0001)
                throw new ArgumentException("Invalid value/step: "+path);
        }
    }
}
