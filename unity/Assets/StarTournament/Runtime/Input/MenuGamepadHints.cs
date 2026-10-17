using UnityEngine.InputSystem;
namespace StarTournament.ProvingGround
{
    // Physical menu positions remain authoritative; glyphs only describe them.
    public enum MenuPadButton { South, East, West, North, Start }
    public enum MenuPadFamily { Generic, Xbox, PlayStation, Switch }
    public static class MenuGamepadHints
    {
        public static MenuPadFamily Family(string layout,string product)
        {
            string identity=((layout??"")+" "+(product??"")).ToLowerInvariant();
            if(identity.Contains("dual")||identity.Contains("playstation"))return MenuPadFamily.PlayStation;
            if(identity.Contains("switch")||identity.Contains("nintendo"))return MenuPadFamily.Switch;
            if(identity.Contains("xinput")||identity.Contains("xbox"))return MenuPadFamily.Xbox;
            return MenuPadFamily.Generic;
        }
        static readonly string[][] Glyphs={
            new[]{"Нижняя","Правая","Левая","Верхняя","Start"},
            new[]{"A","B","X","Y","Menu"},
            new[]{"×","○","□","△","Options"},
            new[]{"B","A","Y","X","+"}
        };
        public static string Glyph(MenuPadFamily family,MenuPadButton button)=>Glyphs[(int)family][(int)button];
        public static string Label(string title,InputDevice device,bool active,MenuPadButton button)
        {
            if(!active||!(device is Gamepad)||!device.added||!device.enabled)return title;
            return title+" · "+Glyph(Family(device.layout,device.description.product),button);
        }
    }
}
