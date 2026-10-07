using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        void DrawHelpDiagram(Transform parent,bool pad)
        {
            foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            Label(parent,"diagram-title",pad?"ГЕЙМПАД · В БОЮ":"КЛАВИАТУРА И МЫШЬ · В БОЮ",26,new Vector2(.02f,.94f),new Vector2(.98f,1),TextAnchor.MiddleLeft,MenuGold);
            void Key(string text,float x,float y,float w=.055f,float h=.055f){var p=Panel(parent,"key-"+text,new Vector2(x,y),new Vector2(x+w,y+h),MenuCard);Label(p.transform,"key",text,17,Vector2.zero,Vector2.one,TextAnchor.MiddleCenter,MenuGold);}
            void Call(string text,float x,float y,float tx,float ty,bool right){float end=right?.76f:.24f;var line=new GameObject("callout",typeof(RectTransform),typeof(HelpCalloutGraphic));line.transform.SetParent(parent,false);Layout((RectTransform)line.transform,Vector2.zero,Vector2.one);line.GetComponent<HelpCalloutGraphic>().Set(new Vector2(x,y),new Vector2(end,ty+.025f));Label(parent,"binding",text,18,new Vector2(right?.77f:0,ty-.025f),new Vector2(right?1:.23f,ty+.065f),right?TextAnchor.MiddleLeft:TextAnchor.MiddleRight,Color.white);}
            if(pad)
            {
                var body=new GameObject("gamepad-body",typeof(RectTransform),typeof(HelpGamepadGraphic));body.transform.SetParent(parent,false);Layout((RectTransform)body.transform,new Vector2(.27f,.50f),new Vector2(.74f,.82f));
                Key("LB",.31f,.80f,.10f);Key("RB",.60f,.80f,.10f);Key("RT",.62f,.87f,.08f);
                Key("L",.34f,.68f,.08f,.08f);Key("R",.55f,.57f,.08f,.08f);
                Key("← ↑ → ↓",.40f,.56f,.12f);Key("View",.44f,.72f,.06f);Key("Menu",.52f,.72f,.07f);
                Key("Y",.64f,.75f);Key("B",.685f,.695f,.035f);Key("A",.64f,.64f);Key("X",.59f,.695f,.035f);
                Call("Предыдущее оружие\nLB / L1 / L",.35f,.83f,0,.86f,false);
                Call("Движение\nЛевый стик",.37f,.72f,0,.73f,false);
                Call("Таблица · удерживать\nView / Share / −",.47f,.74f,0,.61f,false);
                Call("Оружие: ← 1 · ↑ 2\n→ 3 · ↓ 4",.46f,.58f,0,.49f,false);
                Call("Огонь\nRT / R2 / ZR",.66f,.895f,0,.89f,true);
                Call("Следующее оружие\nRB / R1 / R",.65f,.83f,0,.79f,true);
                Call("Пауза\nMenu / Options / +",.55f,.745f,0,.69f,true);
                Call("Прыжок\nA / × / B (нижняя)",.66f,.665f,0,.59f,true);
                Call("Взгляд\nПравый стик",.59f,.61f,0,.49f,true);
            }
            else
            {
                Panel(parent,"keyboard-body",new Vector2(.27f,.56f),new Vector2(.66f,.87f),new Color32(48,65,82,255));
                Key("Esc",.28f,.80f);Key("Tab",.28f,.73f);Key("1  2  3  4",.35f,.80f,.24f);
                Key("Q",.35f,.73f);Key("W",.415f,.73f);Key("E",.48f,.73f);
                Key("A",.35f,.66f);Key("S",.415f,.66f);Key("D",.48f,.66f);Key("Space",.35f,.585f,.24f);
                Panel(parent,"mouse-body",new Vector2(.68f,.58f),new Vector2(.75f,.80f),new Color32(48,65,82,255));Key("L",.685f,.74f,.025f);Key("↕",.717f,.71f,.025f);
                Call("Пауза / назад\nEsc",.305f,.83f,0,.86f,false);Call("Таблица · удерживать\nTab",.30f,.76f,0,.74f,false);
                Call("Движение\nW A S D",.44f,.69f,0,.62f,false);Call("Прыжок\nПробел",.47f,.61f,0,.50f,false);
                Call("Выбрать оружие\n1 / 2 / 3 / 4",.48f,.83f,0,.86f,true);Call("Огонь\nЛевая кнопка мыши",.695f,.77f,0,.74f,true);
                Call("Смена оружия\nQ / E · колесо",.73f,.735f,0,.62f,true);Call("Взгляд\nДвижение мыши",.72f,.62f,0,.50f,true);
            }
            Label(parent,"weapons","1  Винтовка     2  Дробовик     3  Pulse     4  Резак",19,new Vector2(.08f,.40f),new Vector2(.92f,.46f),TextAnchor.MiddleCenter,MenuGold);
            Label(parent,"menu-title","В МЕНЮ И ПОДГОТОВКЕ",24,new Vector2(.02f,.32f),new Vector2(.98f,.39f),TextAnchor.MiddleLeft,MenuGold);
            string[] keys=pad?new[]{"L / D-pad","A / × / B","B / ○ / A","Y / △ / X"}:new[]{"↑ ↓ ← →","Enter","Esc / ⌫","Space"};
            string[] desc={"Выбор · ← / → меняют значение","Подтвердить · включить галочку","Назад / отмена","Присоединиться к матчу"};
            for(int i=0;i<4;i++){float y=.25f-i*.07f;Key(keys[i],.03f,y,.23f,.055f);Label(parent,"menu-binding-"+i,"→   "+desc[i],20,new Vector2(.30f,y),new Vector2(.98f,y+.055f),TextAnchor.MiddleLeft,Color.white);}
            foreach(var label in parent.GetComponentsInChildren<Text>()){label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=label.fontSize;}
        }
    }
    public sealed class HelpGamepadGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper m)
        {
            m.Clear();raycastTarget=false;var r=rectTransform.rect;
            Vector2[] points={new Vector2(.08f,0),new Vector2(0,.10f),new Vector2(.04f,.70f),new Vector2(.18f,.98f),new Vector2(.38f,1),new Vector2(.50f,.87f),new Vector2(.62f,1),new Vector2(.82f,.98f),new Vector2(.96f,.70f),new Vector2(1,.10f),new Vector2(.92f,0),new Vector2(.73f,.31f),new Vector2(.27f,.31f)};
            m.AddVert(r.center,new Color32(48,65,82,255),Vector2.zero);
            foreach(var p in points)m.AddVert(r.min+Vector2.Scale(p,r.size),new Color32(48,65,82,255),Vector2.zero);
            for(int i=0;i<points.Length;i++)m.AddTriangle(0,i+1,(i+1)%points.Length+1);
        }
    }
    public sealed class HelpCalloutGraphic:MaskableGraphic
    {
        Vector2 from,to;public void Set(Vector2 a,Vector2 b){from=a;to=b;raycastTarget=false;color=new Color32(231,187,114,220);SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper m){m.Clear();var r=rectTransform.rect;Vector2 a=r.min+Vector2.Scale(from,r.size),b=r.min+Vector2.Scale(to,r.size);var d=(b-a).normalized;var n=new Vector2(-d.y,d.x);int i=0;m.AddVert(a-n,color,Vector2.zero);m.AddVert(a+n,color,Vector2.zero);m.AddVert(b+n,color,Vector2.zero);m.AddVert(b-n,color,Vector2.zero);m.AddTriangle(i,i+1,i+2);m.AddTriangle(i,i+2,i+3);m.AddVert(b,color,Vector2.zero);m.AddVert(b-d*8+n*4,color,Vector2.zero);m.AddVert(b-d*8-n*4,color,Vector2.zero);m.AddTriangle(4,5,6);}
    }
}
