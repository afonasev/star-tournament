namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateBotBehaviorDefault()
        {
            var p = new ProvingProfile { id = "unity-bot-behavior-v1", version = 1 };
            string[] ids = { "easy", "normal", "hard" }, labels = { "Салага", "Боец", "Ветеран" };
            float[] reaction = { .6f, .28f, .12f }, decision = { .35f, .2f, .1f };
            float[] aimSpeed = { 90, 160, 230 }, aimError = { 12, 5, 1.5f }, aimPeriod = { 1.4f, 1, .7f };
            float[] fireTolerance = { 18, 12, 8 }, preferredDistance = { 6, 5, 4 };
            float[] strafeSeconds = { 1.8f, 1.1f, .7f }, strafeWeight = { .35f, .65f, .85f };
            float[] jumpCooldown = { 6, 4, 3 }, jumpChance = { .12f, .18f, .22f };
            float[] retreatSeconds = { .7f, 1, 1.2f }, retreatHealth = { .2f, .3f, .35f }, supportChance = { .2f, .45f, .65f };
            for (int i = 0; i < ids.Length; i++)
            {
                string path = "bots."+ids[i]+".", label = labels[i]+" · ";
                p.Add(path+"reactionSeconds", "bots", label+"Реакция", "Задержка перед атакой новой замеченной цели.", "seconds", .05f, 2, .01f, reaction[i]);
                p.Add(path+"decisionSeconds", "bots", label+"Планирование", "Интервал пересмотра цели и пути.", "seconds", .05f, 1, .01f, decision[i]);
                p.Add(path+"aimDegreesPerSecond", "bots", label+"Наведение", "Максимальная скорость поворота прицела.", "degrees", 10, 240, 1, aimSpeed[i]);
                p.Add(path+"aimErrorDegrees", "bots", label+"Ошибка прицела", "Амплитуда плавной ошибки прицеливания.", "degrees", .5f, 30, .1f, aimError[i]);
                p.Add(path+"aimPeriodSeconds", "bots", label+"Ритм прицела", "Период изменения ошибки прицела.", "seconds", .1f, 5, .1f, aimPeriod[i]);
                p.Add(path+"fireToleranceDegrees", "bots", label+"Окно выстрела", "Допустимое отклонение прицела для попытки выстрела.", "degrees", 1, 30, 1, fireTolerance[i]);
                p.Add(path+"preferredDistanceMeters", "bots", label+"Дистанция", "Желательная дистанция перестрелки.", "meters", 1, 20, .1f, preferredDistance[i]);
                p.Add(path+"strafeSeconds", "bots", label+"Ритм стрейфа", "Средняя длительность одного бокового манёвра.", "seconds", .2f, 5, .1f, strafeSeconds[i]);
                p.Add(path+"strafeWeight", "bots", label+"Стрейф", "Вес бокового движения в перестрелке.", "ratio", 0, 1, .05f, strafeWeight[i]);
                p.Add(path+"jumpCooldownSeconds", "bots", label+"Пауза прыжков", "Минимальная пауза между прыжками.", "seconds", 1, 15, .1f, jumpCooldown[i]);
                p.Add(path+"jumpChance", "bots", label+"Боевой прыжок", "Вероятность прыжка при смене боевого манёвра и свободном приземлении.", "ratio", 0, 1, .01f, jumpChance[i]);
                p.Add(path+"retreatSeconds", "bots", label+"Короткий отход", "Предельное время отхода после обнаружения угрозы при низком здоровье.", "seconds", .1f, 3, .1f, retreatSeconds[i]);
                p.Add(path+"retreatHealthRatio", "bots", label+"Порог отхода", "Доля здоровья для временного отхода.", "ratio", .05f, .6f, .05f, retreatHealth[i]);
                p.Add(path+"supportChance", "bots", label+"Поддержка", "Склонность помочь доступному союзнику.", "ratio", 0, 1, .05f, supportChance[i]);
            }
            p.Add("bots.cooperation.separationMeters", "bots", "Личное пространство", "Дистанция разделения союзников.", "meters", 1, 5, .1f, 2.5f);
            p.Add("bots.cooperation.supportSeconds", "bots", "Время поддержки", "Максимальная длительность одного общего намерения.", "seconds", .5f, 8, .1f, 3);
            p.Add("bots.cooperation.supportDistanceMeters", "bots", "Дальность поддержки", "Предельная дистанция до союзника для помощи.", "meters", 3, 40, 1, 18);
            p.Add("bots.cooperation.personalitySpread", "bots", "Разнообразие", "Разброс индивидуальной дистанции и ритма относительно профиля.", "ratio", 0, .4f, .05f, .2f);
            p.Add("bots.tactics.retreatCooldownSeconds", "bots", "Пауза отхода", "Минимальная пауза между тактическими отходами.", "seconds", .1f, 20, .1f, 4);
            p.Add("bots.tactics.supportCooldownSeconds", "bots", "Пауза поддержки", "Минимальная пауза между тактическими действиями поддержки.", "seconds", .1f, 20, .1f, 5);
            p.Add("bots.tactics.probeDistance", "bots", "Дистанция пробы", "Дистанция тактической пробы для выбора безопасного действия.", "meters", .1f, 3, .1f, 1);
            p.Add("bots.tactics.jumpSamples", "bots", "Пробы прыжка", "Максимум fixed-step проб траектории motor; превышение отклоняет прыжок.", "count", 4, 512, 1, 128);
            p.Add("bots.tactics.coverDistance", "bots", "Дистанция укрытия", "Целевая дистанция до точки краткого укрытия.", "meters", 1, 8, .1f, 3);
            return p;
        }
    }
}
