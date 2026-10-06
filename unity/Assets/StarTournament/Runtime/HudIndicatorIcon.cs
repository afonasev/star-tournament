using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Native UI geometry is presentation-only; no font glyph or texture import decides the HUD icon shape.
    public sealed class HudIndicatorIcon : MaskableGraphic
    {
        public enum IconKind { Heart, Shield, Cartridges, Rifle, Shotgun, Rocket, Cutter }

        IconKind kind;
        public IconKind Kind
        {
            get => kind;
            set { if (kind == value) return; kind = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            switch (kind)
            {
                case IconKind.Heart:
                    FillPolygon(mesh, rect, new[] {
                        new Vector2(.50f,.08f), new Vector2(.14f,.39f), new Vector2(.06f,.54f),
                        new Vector2(.06f,.70f), new Vector2(.16f,.83f), new Vector2(.30f,.87f),
                        new Vector2(.42f,.82f), new Vector2(.50f,.72f), new Vector2(.58f,.82f),
                        new Vector2(.70f,.87f), new Vector2(.84f,.83f), new Vector2(.94f,.70f),
                        new Vector2(.94f,.54f), new Vector2(.86f,.39f)
                    });
                    break;
                case IconKind.Shield:
                    FillPolygon(mesh, rect, new[] {
                        new Vector2(.50f,.10f), new Vector2(.20f,.23f), new Vector2(.12f,.42f),
                        new Vector2(.12f,.77f), new Vector2(.50f,.90f), new Vector2(.88f,.77f),
                        new Vector2(.88f,.42f), new Vector2(.80f,.23f)
                    });
                    break;
                case IconKind.Cartridges:
                    // Two flat-topped outlined cartridges; the short strokes belong inside the icons.
                    Frame(mesh, rect, .12f, .10f, .43f, .90f);
                    Frame(mesh, rect, .57f, .10f, .88f, .90f);
                    Quad(mesh, rect, .12f, .24f, .43f, .31f);
                    Quad(mesh, rect, .57f, .24f, .88f, .31f);
                    break;
                case IconKind.Rifle:
                    Quad(mesh,rect,.04f,.42f,.20f,.59f); // stock
                    Quad(mesh,rect,.19f,.43f,.72f,.68f); // receiver and fore-end
                    Quad(mesh,rect,.71f,.51f,.97f,.59f); // long barrel
                    Quad(mesh,rect,.35f,.20f,.47f,.44f); // grip
                    Quad(mesh,rect,.43f,.72f,.58f,.78f); // raised sight
                    break;
                case IconKind.Cutter:
                    // Authored icon contour is a technical glyph, not a balance parameter.
                    FillPolygon(mesh,rect,new[]{new Vector2(.58f,.94f),new Vector2(.16f,.43f),new Vector2(.44f,.43f),new Vector2(.32f,.06f),new Vector2(.85f,.60f),new Vector2(.56f,.60f)});
                    break;
                case IconKind.Rocket:
                    FillPolygon(mesh,rect,new[]{new Vector2(.03f,.50f),new Vector2(.20f,.66f),new Vector2(.76f,.66f),new Vector2(.95f,.78f),new Vector2(.88f,.50f),new Vector2(.95f,.22f),new Vector2(.76f,.34f),new Vector2(.20f,.34f)});
                    break;
                case IconKind.Shotgun:
                    Quad(mesh,rect,.05f,.38f,.25f,.56f);
                    Quad(mesh,rect,.23f,.41f,.66f,.63f);
                    Quad(mesh,rect,.64f,.53f,.96f,.61f); // upper barrel
                    Quad(mesh,rect,.64f,.42f,.96f,.50f); // lower barrel
                    Quad(mesh,rect,.38f,.22f,.50f,.43f);
                    break;
            }
        }

        void FillPolygon(VertexHelper mesh, Rect rect, Vector2[] points)
        {
            int first = mesh.currentVertCount;
            foreach (var point in points) Add(mesh, rect, point.x, point.y);
            // These authored contours are star-shaped around their first point.
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }

        void Frame(VertexHelper mesh, Rect rect, float left, float bottom, float right, float top)
        {
            // Border is an icon construction ratio, not a gameplay or layout parameter.
            const float stroke = .09f;
            Quad(mesh, rect, left, bottom, right, bottom + stroke);
            Quad(mesh, rect, left, top - stroke, right, top);
            Quad(mesh, rect, left, bottom + stroke, left + stroke, top - stroke);
            Quad(mesh, rect, right - stroke, bottom + stroke, right, top - stroke);
        }

        void Quad(VertexHelper mesh, Rect rect, float left, float bottom, float right, float top)
        {
            int first = mesh.currentVertCount;
            Add(mesh, rect, left, bottom); Add(mesh, rect, left, top);
            Add(mesh, rect, right, top); Add(mesh, rect, right, bottom);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        void Add(VertexHelper mesh, Rect rect, float x, float y)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(rect.xMin + x * rect.width, rect.yMin + y * rect.height);
            mesh.AddVert(vertex);
        }
    }
}
