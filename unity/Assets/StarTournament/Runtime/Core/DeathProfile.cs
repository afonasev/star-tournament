namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string DeathPresentationId="unity-death-presentation-v1";
        public static ProvingProfile CreateDeathDefault()
        {
            var p=new ProvingProfile{id=DeathPresentationId,version=2};
            p.Add("corpse.shotgunSpeed","death-impulse","Дробовик","Добавочная скорость от смертельной дроби.","meters-per-second",0,15,0.1f,9);
            p.Add("corpse.rifleSpeed","death-impulse","Автомат","Добавочная скорость от смертельной пули.","meters-per-second",0,10,0.05f,3.75f);
            p.Add("corpse.cutterSpeed","death-impulse","Резак","Минимальный импульс в направлении луча.","meters-per-second",0,3,0.025f,0.375f);
            p.Add("corpse.rocketSpeed","death-impulse","Ракета","Максимальная добавочная скорость от эпицентра.","meters-per-second",0,20,0.1f,13.5f);
            p.Add("corpse.rocketFalloff","death-impulse","Спад взрыва","Степень уменьшения импульса по нормированной дистанции взрыва.","exponent",0.5f,3,0.1f,1);
            p.Add("corpse.mass","death-ragdoll","Масса сегмента","Базовая масса физического сегмента тела.","kilograms",1,15,0.5f,3);
            p.Add("corpse.torsoMassRatio","death-ragdoll","Масса корпуса","Множитель массы таза и груди относительно конечностей.","ratio",1,5,0.1f,3);
            p.Add("corpse.radiusRatio","death-ragdoll","Толщина прокси","Радиус коллизионной капсулы как доля длины сегмента.","ratio",0.1f,0.35f,0.01f,0.2f);
            p.Add("corpse.linearDamping","death-ragdoll","Затухание сдвига","Сопротивление перемещению тела после импульса.","per-second",0,3,0.05f,0.4f);
            p.Add("corpse.angularDamping","death-ragdoll","Затухание вращения","Сопротивление вращению конечностей.","per-second",0,10,0.1f,4);
            p.Add("corpse.swingDegrees","death-ragdoll","Свобода суставов","Предельное отклонение конечностей от позы смерти.","degrees",60,140,5,115);
            p.Add("corpse.twistDegrees","death-ragdoll","Скручивание","Допустимое осевое скручивание суставов.","degrees",5,80,5,35);
            p.Add("corpse.torsoSwingDegrees","death-ragdoll","Сгиб корпуса","Предельный сгиб груди и головы.","degrees",10,60,5,35);
            p.Add("corpse.maximumSpeed","death-ragdoll","Предел скорости","Ограничивает скорость сегментов при столкновениях.","meters-per-second",10,50,1,25);
            p.Add("corpse.maximumAngularSpeed","death-ragdoll","Предел вращения","Ограничивает быстрое вращение сегментов.","radians-per-second",2,30,1,12);
            p.Add("corpse.sleepThreshold","death-ragdoll","Порог покоя","Порог удельной энергии для штатного сна Rigidbody.","meters-squared-per-second-squared",0.001f,0.05f,0.001f,0.005f);
            p.Add("corpse.friction","death-ragdoll","Трение тела","Трение о пол и стены для затухания скольжения.","ratio",0,1,0.05f,0.8f);
            p.Add("corpse.projectionDistance","death-ragdoll","Допуск суставов","Максимальное расхождение перед коррекцией соединения.","meters",0.005f,0.1f,0.005f,0.025f);
            p.Add("corpse.depenetrationSpeed","death-ragdoll","Выход из пересечения","Предельная скорость устранения стартового пересечения с ареной.","meters-per-second",0.5f,8,0.1f,2);
            p.Add("corpse.gravity","death-ragdoll","Гравитация тела","Ускорение свободного падения тела в отдельной physics scene.","meters-per-second-squared",1,25,.1f,9.8f);
            p.Add("corpse.headRadiusScale","death-ragdoll","Объём головы","Множитель радиуса прокси головы относительно длины шейной кости.","ratio",1,4,.1f,3);
            p.Add("corpse.torsoRadiusScale","death-ragdoll","Объём корпуса","Множитель радиуса прокси таза и груди для контакта брони с поверхностью.","ratio",1,2,.1f,1.4f);
            return p;
        }
        public ProvingProfile BeforeDeathImpulseIncrease()
        {
            if(id!=DeathPresentationId||version<2)return this;
            var copy=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));copy.version=1;
            // Exact predecessor defaults keep shipped hashes and custom immutable revisions readable.
            foreach(var entry in new[]{("corpse.shotgunSpeed",6f), ("corpse.rifleSpeed",2.5f), ("corpse.cutterSpeed",.25f), ("corpse.rocketSpeed",9f)})
            {copy.Descriptor(entry.Item1).DefaultValue=entry.Item2;copy.Set(entry.Item1,entry.Item2);}
            copy.Descriptor("corpse.rifleSpeed").Step=.1f;copy.Descriptor("corpse.cutterSpeed").Step=.05f;
            return copy;
        }
    }
}
