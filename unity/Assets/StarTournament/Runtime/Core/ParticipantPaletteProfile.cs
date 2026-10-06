namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string ParticipantPaletteId="unity-participant-presentation-v1";
        public static ProvingProfile CreateParticipantPaletteDefault()
        {
            var p=new ProvingProfile{id=ParticipantPaletteId,version=3};
            p.Add("participant.color.blue.r","participant-colors","Синий · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,69);
            p.Add("participant.color.blue.g","participant-colors","Синий · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,158);
            p.Add("participant.color.blue.b","participant-colors","Синий · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,255);
            p.Add("participant.color.magenta.r","participant-colors","Маджента · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,238);
            p.Add("participant.color.magenta.g","participant-colors","Маджента · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,107);
            p.Add("participant.color.magenta.b","participant-colors","Маджента · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,255);
            p.Add("participant.color.yellow.r","participant-colors","Жёлтый · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,255);
            p.Add("participant.color.yellow.g","participant-colors","Жёлтый · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,230);
            p.Add("participant.color.yellow.b","participant-colors","Жёлтый · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,65);
            p.Add("participant.color.orange.r","participant-colors","Оранжевый · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,255);
            p.Add("participant.color.orange.g","participant-colors","Оранжевый · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,173);
            p.Add("participant.color.orange.b","participant-colors","Оранжевый · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,53);
            p.Add("participant.color.cyan.r","participant-colors","Голубой · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,56);
            p.Add("participant.color.cyan.g","participant-colors","Голубой · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,213);
            p.Add("participant.color.cyan.b","participant-colors","Голубой · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,255);
            p.Add("participant.color.green.r","participant-colors","Зелёный · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,64);
            p.Add("participant.color.green.g","participant-colors","Зелёный · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,232);
            p.Add("participant.color.green.b","participant-colors","Зелёный · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,120);
            p.Add("participant.color.red.r","participant-colors","Красный · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,255);
            p.Add("participant.color.red.g","participant-colors","Красный · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,86);
            p.Add("participant.color.red.b","participant-colors","Красный · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,86);
            p.Add("participant.color.white.r","participant-colors","Белый · R","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,244);
            p.Add("participant.color.white.g","participant-colors","Белый · G","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,247);
            p.Add("participant.color.white.b","participant-colors","Белый · B","Канал цвета участника; применяется при новом матче. Frozen composition и Repeat сохраняют прежний цвет.","8-bit channel",0,255,1,255);
            p.Add("participant.surface.detailGain","participant-colors","Контраст панелей","Усиление яркости исходной фактуры цветных панелей; оттенок источника удаляется.","ratio",1,20,.1f,6);
            p.Add("participant.surface.maskStart","participant-colors","Начало цветовой зоны","Ниже этой яркости фактуры поверхность остаётся нейтральным графитом.","linear reflectance",0,.5f,.001f,.002f);
            p.Add("participant.surface.maskWidth","participant-colors","Переход цветовой зоны","Ширина плавного перехода от нейтральных швов к окрашенной панели.","linear reflectance",.001f,.5f,.001f,.006f);
            p.Add("participant.surface.panelFloor","participant-colors","Минимум яркости панелей","Нижняя яркость цвета крупной панели для читаемости всех восьми assigned цветов.","ratio",0,1,.05f,.65f);
            p.Add("participant.surface.shellStart","participant-colors","Маска бронепанелей","Дополнительный порог светлых панелей корпуса и шлема; тёмные участки остаются графитом.","linear reflectance",0,.5f,.001f,.09f);
            p.Add("participant.surface.shellWidth","participant-colors","Край бронепанели","Дополнительная ширина перехода assigned цвета на бронепанелях корпуса и шлема.","linear reflectance",.001f,.5f,.001f,.015f);
            p.Add("participant.surface.neutralPlateGain","participant-colors","Графит голеней","Яркость нейтральных керамических пластин на голенях.","ratio",0,1,.01f,.15f);
            p.Add("participant.surface.helmetStripeWidth","participant-colors","Панели шлема","Ширина цветной полосы и боковых участков в UV атласе шлема; остальная оболочка нейтральная.","UV ratio",.01f,.3f,.01f,.09f);
            return p;
        }
        internal ProvingProfile BeforeDiscreteIdentityPanels()
        {
            if(Id!=ParticipantPaletteId)return this;
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));p.version=2;
            string[] added={"participant.surface.shellStart","participant.surface.shellWidth","participant.surface.neutralPlateGain","participant.surface.helmetStripeWidth"};
            p.descriptors.RemoveAll(d=>System.Array.IndexOf(added,d.Path)>=0);
            p.values.RemoveAll(v=>System.Array.IndexOf(added,v.Path)>=0);return p;
        }
        internal ProvingProfile BeforeIdentitySurface()
        {
            if(Id!=ParticipantPaletteId)return this;
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));p.version=1;
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("participant.surface."));
            p.values.RemoveAll(v=>v.Path.StartsWith("participant.surface."));return p;
        }
        public void EnsureIdentitySurfaceDescriptors()
        {
            var current=CreateParticipantPaletteDefault();
            foreach(var descriptor in current.Descriptors)
                if(FindDescriptor(descriptor.Path)==null)
                {descriptors.Add(descriptor);values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});}
            version=current.Version;
        }
    }
}
