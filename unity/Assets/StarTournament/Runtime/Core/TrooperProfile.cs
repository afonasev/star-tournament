namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateTrooperDefault()
        {
            var p=new ProvingProfile{id="unity-trooper-presentation-v1",version=1};
            p.Add("animation.walkThreshold","trooper-animation","Порог шага","Минимальная фактическая горизонтальная скорость для walk.","meters-per-second",.01f,3,.01f,.1f);
            p.Add("animation.runThreshold","trooper-animation","Порог бега","Фактическая горизонтальная скорость переключения walk на run.","meters-per-second",3,15,.1f,6);
            p.Add("animation.blendSeconds","trooper-animation","Переход позы","Время сглаживания весов только визуальных клипов.","seconds",0,.5f,.01f,.12f);
            p.Add("animation.aimHoldSeconds","trooper-animation","Боевая стойка","Сколько удерживать aim после принятого выстрела.","seconds",.4f,5,.1f,1.6f);
            p.Add("view.x","trooper-view","Руки по горизонтали","Смещение всей модели рук и оружия относительно камеры.","meters",-.5f,.5f,.01f,.12f);
            p.Add("view.y","trooper-view","Руки по вертикали","Смещение feet-origin модели рук относительно камеры.","meters",-2,-.5f,.01f,-1.52f);
            p.Add("view.z","trooper-view","Руки по глубине","Смещение модели рук вперёд относительно камеры.","meters",-.5f,1,.01f,.15f);
            return p;
        }
    }
}
