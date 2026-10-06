namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateBotPerceptionDefault()
        {
            var p = new ProvingProfile { id = "unity-bot-perception-v1", version = 2 };
            string[] ids = { "easy", "normal", "hard" }, labels = { "Салага", "Боец", "Ветеран" };
            float[] fov = { 110, 130, 150 }, memory = { 2, 4, 6 };
            for (int i = 0; i < ids.Length; i++)
            {
                p.Add("bots."+ids[i]+".fieldOfViewDegrees", "bots", labels[i]+" · Поле зрения", "Горизонтальный угол обнаружения врага при открытом LOS.", "degrees", 60, 180, 1, fov[i]);
                p.Add("bots."+ids[i]+".pickupDelaySeconds", "bots", labels[i]+" · Знание подборов", "Задержка глобальных сообщений появления и исчезновения без сведений о собравшем участнике.", "seconds", .05f, 5, .05f, new[]{1.2f,.5f,.15f}[i]);
                p.Add("bots."+ids[i]+".memorySeconds", "bots", labels[i]+" · Память", "Срок знания от исходного наблюдения, включая задержку сообщения.", "seconds", .5f, 15, .1f, memory[i]);
            }
            p.Add("bots.cooperation.communicationSeconds", "bots", "Сообщения команды", "Задержка передачи замеченной позиции врага.", "seconds", .1f, 2, .1f, .3f);
            return p;
        }
    }
}
