namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string BloodPresentationId="unity-blood-presentation-v1";
        public ProvingProfile BeforeBloodIntensityIncrease()
        {
            if(id!=BloodPresentationId||version<2)return this;
            var copy=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            copy.version=1;
            // Exact prior default registry is required to validate immutable Lab history hashes.
            copy.Descriptor("blood.drops").DefaultValue=10f;copy.Set("blood.drops",10f);
            return copy;
        }
        public static ProvingProfile CreateBloodDefault()
        {
            var p=new ProvingProfile{id=BloodPresentationId,version=2};
            p.Add("blood.enabled","blood-drops","Включено","Включает кровь и временные следы.","boolean",0f,1f,1f,1f);
            p.Add("blood.drops","blood-drops","Капли за попадание","Число капель в одном всплеске.","count",1f,24f,1f,15f);
            p.Add("blood.dropSize","blood-drops","Размер капли","Диаметр крупных капель; мелкие используют долю этого размера.","meters",0.005f,0.08f,0.001f,0.025f);
            p.Add("blood.speed","blood-drops","Скорость разлёта","Начальная скорость капель вдоль попадания.","meters-per-second",1f,15f,0.1f,7f);
            p.Add("blood.spreadDegrees","blood-drops","Угол разлёта","Угол конуса направленных брызг.","degrees",0f,60f,1f,22f);
            p.Add("blood.speedVariation","blood-drops","Разброс скорости","Доля уменьшения скорости отдельных капель.","ratio",0f,.8f,.05f,.35f);
            p.Add("blood.dropVariation","blood-drops","Разброс размеров","Доля уменьшения размера отдельных капель.","ratio",0f,.8f,.05f,.5f);
            p.Add("blood.dropElongation","blood-drops","Вытянутость капель","Длина капли относительно её диаметра.","ratio",1f,4f,.1f,2f);
            p.Add("blood.lift","blood-drops","Подброс","Вертикальная добавка начальной скорости капель.","meters-per-second",0f,4f,0.1f,0.6f);
            p.Add("blood.gravity","blood-drops","Гравитация капель","Ускорение падения визуальных капель.","meters-per-second-squared",1f,25f,0.1f,9.8f);
            p.Add("blood.flightSeconds","blood-drops","Время полёта","Предел времени полёта без контакта с поверхностью.","seconds",0.1f,3f,0.05f,1.2f);
            p.Add("blood.shotgunScale","blood-drops","Масштаб дробовика","Множитель размеров капель и следов дробовика.","ratio",0.5f,2f,0.05f,1.35f);
            p.Add("blood.rifleScale","blood-drops","Масштаб автомата","Множитель размеров капель и следов пули.","ratio",0.25f,1.5f,0.05f,0.75f);
            p.Add("blood.rocketScale","blood-drops","Масштаб взрыва","Множитель размеров капель и следов взрыва.","ratio",0.5f,2f,0.05f,1.4f);
            p.Add("blood.cutterScale","blood-drops","Масштаб резака","Множитель размеров капель и следов луча.","ratio",0.25f,1.5f,0.05f,0.55f);
            p.Add("blood.cutterInterval","blood-drops","Интервал резака","Минимальный промежуток между всплесками луча на одной цели.","seconds",0.05f,1f,0.05f,0.2f);
            p.Add("blood.maxDrops","blood-drops","Предел капель","Общий максимум летящих капель для всех камер.","count",24f,384f,8f,128f);
            p.Add("blood.markSize","blood-marks","Размер следа","Диаметр основного пятна от капли.","meters",0.02f,0.5f,0.01f,0.18f);
            p.Add("blood.markVariation","blood-marks","Вариация следов","Разброс размеров пятен относительно основного.","ratio",0f,0.8f,0.05f,0.45f);
            p.Add("blood.markSeconds","blood-marks","Время жизни следа","Полная длительность следа до исчезновения.","seconds",1f,120f,1f,30f);
            p.Add("blood.fadeFraction","blood-marks","Доля затухания","Доля времени жизни, отведённая плавному исчезновению.","ratio",0.05f,1f,0.05f,0.35f);
            p.Add("blood.maxMarks","blood-marks","Предел следов","Общий максимум пятен; новые заменяют самые старые.","count",16f,512f,8f,192f);
            p.Add("blood.red","blood-color","Красный","Красная составляющая свежей стилизованной крови.","ratio",0f,1f,0.01f,0.36f);
            p.Add("blood.green","blood-color","Зелёный","Зелёная составляющая свежей стилизованной крови.","ratio",0f,1f,0.005f,0.025f);
            p.Add("blood.blue","blood-color","Синий","Синяя составляющая свежей стилизованной крови.","ratio",0f,1f,0.005f,0.035f);
            p.Add("blood.opacity","blood-color","Непрозрачность","Начальная непрозрачность капель и следов.","ratio",0.1f,1f,0.05f,0.9f);
            p.Add("blood.dryDarkening","blood-color","Потемнение","Насколько темнеет след к концу жизни.","ratio",0f,0.8f,0.05f,0.3f);
            return p;
        }
    }
}
