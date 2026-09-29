namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string ParticipantPaletteId="unity-participant-presentation-v1";
        public static ProvingProfile CreateParticipantPaletteDefault()
        {
            var p=new ProvingProfile{id=ParticipantPaletteId,version=1};
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
            return p;
        }
    }
}
