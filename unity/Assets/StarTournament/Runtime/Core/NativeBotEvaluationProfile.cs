namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateBotEvaluationDefault()
        {
            var p=new ProvingProfile{id="native-bot-evaluation-v1",version=1};
            p.Add("bots.evaluation.controlSeeds","bot-evaluation","Контрольные seed","Число независимых контрольных seed; tuning набор отделён.","count",8,100,1,8);
            p.Add("bots.evaluation.permutations","bot-evaluation","Перестановки","Количество перестановок мест и сторон для каждого seed.","count",2,8,1,2);
            p.Add("bots.evaluation.seconds","bot-evaluation","Длина оценки","Одинаковое окно нативной симуляции в каждом матче.","seconds",60,600,1,120);
            p.Add("bots.evaluation.scoreShare","bot-evaluation","Порог очков","Минимальная доля очков более сильной сложности.","ratio",.5f,1,.01f,.56f);
            p.Add("bots.evaluation.damageShare","bot-evaluation","Порог урона","Минимальная доля урона более сильной сложности.","ratio",.5f,1,.01f,.54f);
            p.Add("bots.evaluation.winShare","bot-evaluation","Порог побед","Минимальная доля выигранных оценочных окон; ничья весит половину.","ratio",.5f,1,.01f,.55f);
            p.Add("bots.evaluation.lower95","bot-evaluation","Нижняя граница","Нижняя 95% bootstrap граница доли урона по seed после перестановок.","ratio",.5f,1,.01f,.5f);
            p.Add("bots.evaluation.bootstrapSamples","bot-evaluation","Bootstrap","Число выборок uncertainty по независимым seed.","count",1000,100000,1000,10000);
            return p;
        }
    }
}
