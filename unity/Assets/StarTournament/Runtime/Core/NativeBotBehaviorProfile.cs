namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateBotBehaviorDefault()
        {
            var p = new ProvingProfile { id = "unity-bot-behavior-v1", version = 3 };
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
            AddBotTactics(p);
            return p;
        }
        internal ProvingProfile BeforeBotWeaponEvaluation()
        {
            if(id!="unity-bot-behavior-v1"||version!=3)return this;
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));p.version=2;
            p.descriptors.RemoveAll(d=>d.Path=="bots.strategy.weaponTimeSamples"||d.Path=="bots.strategy.weaponSpreadSamples");
            p.values.RemoveAll(v=>v.Path=="bots.strategy.weaponTimeSamples"||v.Path=="bots.strategy.weaponSpreadSamples");
            p.RebalanceDefault("bots.strategy.weaponHorizonSeconds",.25f);return p;
        }
        internal ProvingProfile BeforeBotTactics()
        {
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(BeforeBotWeaponEvaluation()));
            if(p.id=="unity-bot-behavior-v1"){p.version=1;p.descriptors.RemoveAll(d=>d.Path.StartsWith("bots.strategy.")||d.Path.EndsWith(".leadQuality"));p.values.RemoveAll(v=>v.Path.StartsWith("bots.strategy.")||v.Path.EndsWith(".leadQuality"));}
            if(p.id=="unity-bot-perception-v1"){p.version=1;p.descriptors.RemoveAll(d=>d.Path.EndsWith(".pickupDelaySeconds"));p.values.RemoveAll(v=>v.Path.EndsWith(".pickupDelaySeconds"));}
            return p;
        }
        public void EnsureBotDescriptors()
        {
            var current=id=="unity-bot-behavior-v1"?CreateBotBehaviorDefault():id=="unity-bot-perception-v1"?CreateBotPerceptionDefault():null;
            if(current==null)return;valueIndex=null;version=current.version;
            foreach(var d in current.descriptors)if(FindDescriptor(d.Path)==null){descriptors.Add(UnityEngine.JsonUtility.FromJson<NumericDescriptor>(UnityEngine.JsonUtility.ToJson(d)));values.Add(new ProvingProfileValue{Path=d.Path,Value=current.Get(d.Path)});}
        }
        static void AddBotTactics(ProvingProfile p)
        {
            p.Add("bots.strategy.healUrgency","bot-behavior","Срочность лечения","Вес восстановления здоровья относительно открытия оружия.","ratio",1,5,.1f,2);
            p.Add("bots.strategy.intentHoldSeconds","bot-behavior","Устойчивость цели","Минимальное время полезной pickup цели до обычного пересмотра.","seconds",0f,5f,0.05f,0.8f);
            p.Add("bots.strategy.pickupRetrySeconds","bot-behavior","Повтор пути","Пауза перед повтором недостижимого pickup.","seconds",1f,60f,1f,12f);
            p.Add("bots.strategy.pickupTimeoutSeconds","bot-behavior","Время задачи","Предельное время подхода к pickup перед отказом.","seconds",2f,60f,1f,20f);
            p.Add("bots.strategy.routeCostPerMeter","bot-behavior","Цена маршрута","Снижение utility за метр фактического маршрута.","utility/m",0.01f,10f,0.01f,0.6f);
            p.Add("bots.strategy.pickupBaseUtility","bot-behavior","Сбор бонуса","Основной вес полезного pickup.","utility",1f,500f,1f,100f);
            p.Add("bots.strategy.combatUtility","bot-behavior","Вес боя","Стоимость ухода из видимого боя ради pickup.","utility",0f,500f,1f,75f);
            p.Add("bots.strategy.riskRadiusMeters","bot-behavior","Радиус угрозы","Расстояние от известного врага до pickup для штрафа риска.","meters",1f,50f,0.5f,12f);
            p.Add("bots.strategy.riskWeight","bot-behavior","Штраф угрозы","Штраф опасного маршрута от допустимой памяти.","utility",0f,500f,1f,70f);
            p.Add("bots.strategy.vulnerableWeight","bot-behavior","Уязвимость","Уменьшение стоимости выбора уязвимой цели.","meters",0f,100f,0.5f,15f);
            p.Add("bots.strategy.strongerRatio","bot-behavior","Крепкий противник","Отношение живучести для отхода.","ratio",1f,5f,0.05f,1.6f);
            p.Add("bots.strategy.weaponHoldSeconds","bot-behavior","Удержание оружия","Пауза между обычными сменами подходящего оружия.","seconds",0f,5f,0.05f,0.7f);
            p.Add("bots.strategy.weaponHysteresis","bot-behavior","Порог смены","Преимущество альтернативного оружия для смены.","ratio",1f,3f,0.05f,1.15f);
            p.Add("bots.strategy.switchPenalty","bot-behavior","Цена смены","Штраф utility переключения оружия.","ratio",0f,1f,0.05f,0.15f);
            p.Add("bots.strategy.rocketCloseMeters","bot-behavior","Близкий Pulse","Верхняя граница безопасного прицела в корпус.","meters",1f,30f,0.5f,10f);
            p.Add("bots.strategy.rocketLeadSeconds","bot-behavior","Предел упреждения","Предельный прогноз только наблюдаемого движения.","seconds",0f,3f,0.05f,1f);
            p.Add("bots.strategy.surfaceProbeMeters","bot-behavior","Поиск поверхности","Предельная вертикальная проба под наблюдаемыми ногами.","meters",0.1f,10f,0.1f,2f);
            p.Add("bots.strategy.impactToleranceMeters","bot-behavior","Допуск контакта","Допуск первого контакта около предполагаемой поверхности.","meters",0.05f,3f,0.05f,0.35f);
            p.Add("bots.strategy.bodySafetyMargin","bot-behavior","Запас участника","Расширение self/allied splash проверки.","meters",0f,3f,0.05f,0.5f);
            p.Add("bots.strategy.lowAmmoRatio","bot-behavior","Refill","Доля оставшегося запаса для полезного refill.","ratio",0f,1f,0.05f,0.55f);
            p.Add("bots.strategy.allyNeedWeight","bot-behavior","Потребность союзника","Уменьшение utility бонуса для более нуждающегося союзника.","ratio",0f,1f,0.05f,0.8f);
            p.Add("bots.strategy.weaponHorizonSeconds","bot-behavior","Окно атаки","Временное окно сравнения burst и непрерывного огня.","seconds",.1f,3,.05f,1f);
            p.Add("bots.strategy.weaponTimeSamples","bot-behavior","Прогноз контакта","Число временных проб наведения и контакта за окно оценки.","count",2,32,1,8);
            p.Add("bots.strategy.weaponSpreadSamples","bot-behavior","Прогноз разброса","Число равномерных проб диска spread; не меняет фактические выстрелы.","count",4,64,1,16);
            string[] ids={"easy","normal","hard"};float[] quality={.35f,.7f,1f};
            for(int i=0;i<ids.Length;i++)p.Add("bots."+ids[i]+".leadQuality","bot-behavior",ids[i]+" · Упреждение","Доля наблюдаемой скорости для упреждения.","ratio",0,1,.05f,quality[i]);
        }
    }
}
