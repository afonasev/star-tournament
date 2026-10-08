using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Editor
{
    // Render the production focus component in Editor, without a Player build.
    public static class ButtonFocusPreview
    {
        public static void Render()
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_FOCUS_PREVIEW");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Missing preview output path");
            var root=new GameObject("focus-preview");
            var skin=root.AddComponent<ProvingGround>();
            var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var gold=(Color32)typeof(ProvingGround).GetField("MenuGold",flags).GetValue(null);
            var card=(Color32)typeof(ProvingGround).GetField("MenuCard",flags).GetValue(null);
            var ink=(Color32)typeof(ProvingGround).GetField("MenuInk",flags).GetValue(null);
            var camera=root.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color32(11,19,29,255);camera.orthographic=true;
            var target=new RenderTexture(1200,800,24);camera.targetTexture=target;
            var canvas=new GameObject("canvas",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(root.transform);
            var ui=canvas.GetComponent<Canvas>();ui.renderMode=RenderMode.ScreenSpaceCamera;ui.worldCamera=camera;ui.planeDistance=1;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for(int row=0;row<3;row++)for(int focus=0;focus<2;focus++)
            {
                var go=new GameObject("button",typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(canvas.transform,false);
                var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                rect.sizeDelta=new Vector2(470,row==2?46:100);rect.anchoredPosition=new Vector2(focus==0?-280:280,230-row*220);
                typeof(ProvingGround).GetMethod("RoundRosterPanel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(skin,new object[]{go});
                go.GetComponent<Image>().color=row==1?card:gold;
                var text=new GameObject("label",typeof(RectTransform),typeof(Text));text.transform.SetParent(go.transform,false);
                var t=text.GetComponent<Text>();t.font=font;t.text=row==0?"НАЧАТЬ МАТЧ":row==1?"НАЗАД":"КОМПАКТНАЯ КНОПКА";t.fontSize=row==2?22:32;
                t.alignment=TextAnchor.MiddleCenter;t.color=row==1?Color.white:ink;
                var tr=(RectTransform)text.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
                var presentation=go.AddComponent<MenuPresentation>();presentation.Compact=false;presentation.SetFocused(focus==1);
            }
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());
            Debug.Log("STAR_BUTTON_FOCUS_PREVIEW_PASS "+output);
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
