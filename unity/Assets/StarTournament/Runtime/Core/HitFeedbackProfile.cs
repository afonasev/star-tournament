namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string HitFeedbackPresentationId="unity-hit-feedback-presentation-v1";
        public static ProvingProfile CreateHitFeedbackDefault()
        {
            var p=new ProvingProfile{id=HitFeedbackPresentationId,version=1};
            p.Add("hit.enabled","hit-reaction","Реакция корпуса","Включает визуальный отклик верхней части тела на урон здоровью и щиту.","boolean",0,1,1,1);
            p.Add("hit.degrees","hit-reaction","Наклон корпуса","Предельный наклон от направления удара; ноги и игровая позиция не меняются.","degrees",0,25,1,12);
            p.Add("hit.attackSeconds","hit-reaction","Начало толчка","Время быстрого отклонения корпуса после попадания.","seconds",.01f,.12f,.01f,.03f);
            p.Add("hit.returnSeconds","hit-reaction","Возврат корпуса","Время плавного возврата к базовой анимации.","seconds",.1f,.8f,.02f,.28f);
            p.Add("hit.interval","hit-reaction","Интервал реакции","Минимальный промежуток между толчками одной цели; ограничивает автомат и резак.","seconds",.05f,.5f,.01f,.15f);
            p.Add("hit.bloodSize","hit-body-blood","Размер пятна на бойце","Диаметр локального следа при потере здоровья.","meters",.04f,.5f,.01f,.24f);
            p.Add("hit.bloodSeconds","hit-body-blood","Время пятна на бойце","Длительность следа на коже до исчезновения.","seconds",1,30,1,8);
            p.Add("hit.bloodFadeFraction","hit-body-blood","Затухание пятна","Доля времени жизни, в течение которой пятно исчезает.","ratio",.1f,1,.05f,.5f);
            p.Add("hit.bloodInterval","hit-body-blood","Интервал пятен","Минимальный промежуток между новыми пятнами на одной цели.","seconds",.05f,1,.05f,.2f);
            p.Add("hit.maxBloodMarks","hit-body-blood","Предел пятен","Общее число прикреплённых пятен в матче, независимо от камер.","count",8,96,8,32);
            p.Add("hit.maxBloodMarksPerParticipant","hit-body-blood","Пятна на одном бойце","Предел пятен на коже одного бойца для сохранения читаемости его цвета.","count",1,12,1,6);
            p.Add("hit.shieldSize","hit-shield","Размер вспышки щита","Диаметр синего контактного свечения на бойце.","meters",.06f,.6f,.02f,.3f);
            p.Add("hit.shieldSeconds","hit-shield","Время вспышки щита","Короткое свечение при уроне поглощённом щитом.","seconds",.04f,.4f,.02f,.16f);
            p.Add("hit.shieldInterval","hit-shield","Интервал вспышек","Минимальный промежуток между вспышками на одной цели.","seconds",.02f,.3f,.02f,.06f);
            p.Add("hit.shieldIntensity","hit-shield","Яркость щита","Интенсивность синего свечения контакта.","ratio",.5f,6,.25f,3);
            p.Add("hit.shieldRed","hit-shield","Красный в свечении щита","Красная составляющая контактной вспышки.","ratio",0,1,.005f,.035f);
            p.Add("hit.shieldGreen","hit-shield","Зелёный в свечении щита","Зелёная составляющая контактной вспышки.","ratio",0,1,.01f,.28f);
            p.Add("hit.shieldBlue","hit-shield","Синий в свечении щита","Синяя составляющая контактной вспышки.","ratio",0,1,.01f,1);
            p.Add("hit.maxShieldFlashes","hit-shield","Предел вспышек","Общее число вспышек в матче, независимо от камер.","count",8,64,8,16);
            return p;
        }
    }
}
