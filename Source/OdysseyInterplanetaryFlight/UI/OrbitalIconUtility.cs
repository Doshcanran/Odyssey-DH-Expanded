
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public static class OrbitalIconUtility
    {
        public static void DrawNodeIcon(OrbitalNodeType type, Rect rect, bool highlighted = false)
        {
            Color accent;
            switch (type)
            {
                case OrbitalNodeType.Planet:
                    accent = new Color(0.35f, 0.75f, 1f);
                    DrawPlanet(rect, accent, highlighted);
                    break;
                case OrbitalNodeType.Station:
                    accent = new Color(0.75f, 0.95f, 0.75f);
                    DrawStation(rect, accent, highlighted);
                    break;
                case OrbitalNodeType.AsteroidBelt:
                    accent = new Color(0.70f, 0.70f, 0.70f);
                    DrawAsteroidBelt(rect, accent, highlighted);
                    break;
                case OrbitalNodeType.Asteroid:
                    accent = new Color(0.75f, 0.72f, 0.62f);
                    DrawAsteroid(rect, accent, highlighted);
                    break;
                default:
                    accent = Color.white;
                    DrawPlanet(rect, accent, highlighted);
                    break;
            }
        }

        public static void DrawShipIcon(Rect rect, Color color, bool highlighted = false)
        {
            GUI.BeginGroup(rect);
            float w = rect.width;
            float h = rect.height;

            Color body = highlighted ? Color.Lerp(color, Color.white, 0.35f) : color;
            Vector2 nose = new Vector2(w * 0.84f, h * 0.50f);
            Vector2 top = new Vector2(w * 0.25f, h * 0.20f);
            Vector2 bottom = new Vector2(w * 0.25f, h * 0.80f);
            Vector2 mid = new Vector2(w * 0.46f, h * 0.50f);

            Widgets.DrawLine(top, nose, body, highlighted ? 3f : 2f);
            Widgets.DrawLine(bottom, nose, body, highlighted ? 3f : 2f);
            Widgets.DrawLine(top, mid, body, highlighted ? 3f : 2f);
            Widgets.DrawLine(bottom, mid, body, highlighted ? 3f : 2f);

            Widgets.DrawLine(new Vector2(w * 0.16f, h * 0.28f), new Vector2(w * 0.36f, h * 0.42f), body, 2f);
            Widgets.DrawLine(new Vector2(w * 0.16f, h * 0.72f), new Vector2(w * 0.36f, h * 0.58f), body, 2f);

            Rect core = new Rect(w * 0.34f, h * 0.40f, w * 0.18f, h * 0.20f);
            Widgets.DrawBoxSolid(core, body);

            GUI.EndGroup();
        }

        private static void DrawPlanet(Rect rect, Color color, bool highlighted)
        {
            GUI.BeginGroup(rect);
            float d = Mathf.Min(rect.width, rect.height) * 0.72f;
            Rect body = CenterRect(rect.width, rect.height, d, d);
            DrawFilledCircle(body, color);
            DrawCircleOutline(body, highlighted ? 2.5f : 2f, Color.Lerp(color, Color.white, 0.45f));

            Rect ring = CenterRect(rect.width, rect.height, d * 1.20f, d * 0.34f);
            DrawEllipseOutline(ring, 1.5f, new Color(1f, 1f, 1f, highlighted ? 0.80f : 0.55f));
            GUI.EndGroup();
        }

        private static void DrawStation(Rect rect, Color color, bool highlighted)
        {
            GUI.BeginGroup(rect);
            float s = Mathf.Min(rect.width, rect.height) * 0.62f;
            Rect core = CenterRect(rect.width, rect.height, s, s);
            Widgets.DrawBoxSolid(core, new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.22f, 1f));
            DrawRectOutline(core, highlighted ? 2.5f : 2f, color);

            Vector2 c = new Vector2(rect.width / 2f, rect.height / 2f);
            float arm = s * 0.78f;
            Widgets.DrawLine(new Vector2(c.x - arm, c.y), new Vector2(c.x + arm, c.y), color, highlighted ? 3f : 2f);
            Widgets.DrawLine(new Vector2(c.x, c.y - arm), new Vector2(c.x, c.y + arm), color, highlighted ? 3f : 2f);

            float node = Mathf.Max(2f, s * 0.12f);
            Widgets.DrawBoxSolid(new Rect(c.x - arm - node * 0.5f, c.y - node * 0.5f, node, node), color);
            Widgets.DrawBoxSolid(new Rect(c.x + arm - node * 0.5f, c.y - node * 0.5f, node, node), color);
            Widgets.DrawBoxSolid(new Rect(c.x - node * 0.5f, c.y - arm - node * 0.5f, node, node), color);
            Widgets.DrawBoxSolid(new Rect(c.x - node * 0.5f, c.y + arm - node * 0.5f, node, node), color);
            GUI.EndGroup();
        }

        private static void DrawAsteroid(Rect rect, Color color, bool highlighted)
        {
            GUI.BeginGroup(rect);
            float s = Mathf.Min(rect.width, rect.height) * 0.70f;
            Vector2 c = new Vector2(rect.width / 2f, rect.height / 2f);

            Vector2 p1 = new Vector2(c.x, c.y - s * 0.52f);
            Vector2 p2 = new Vector2(c.x + s * 0.50f, c.y - s * 0.08f);
            Vector2 p3 = new Vector2(c.x + s * 0.34f, c.y + s * 0.46f);
            Vector2 p4 = new Vector2(c.x - s * 0.22f, c.y + s * 0.52f);
            Vector2 p5 = new Vector2(c.x - s * 0.56f, c.y + s * 0.10f);
            Vector2 p6 = new Vector2(c.x - s * 0.40f, c.y - s * 0.38f);

            float lw = highlighted ? 3f : 2f;
            Widgets.DrawLine(p1, p2, color, lw);
            Widgets.DrawLine(p2, p3, color, lw);
            Widgets.DrawLine(p3, p4, color, lw);
            Widgets.DrawLine(p4, p5, color, lw);
            Widgets.DrawLine(p5, p6, color, lw);
            Widgets.DrawLine(p6, p1, color, lw);

            Rect crater = new Rect(c.x - s * 0.12f, c.y - s * 0.06f, s * 0.18f, s * 0.18f);
            DrawFilledCircle(crater, new Color(0f, 0f, 0f, 0.20f));
            GUI.EndGroup();
        }

        private static void DrawAsteroidBelt(Rect rect, Color color, bool highlighted)
        {
            GUI.BeginGroup(rect);
            Rect ring = CenterRect(rect.width, rect.height, rect.width * 0.88f, rect.height * 0.44f);
            DrawEllipseOutline(ring, highlighted ? 2.5f : 2f, new Color(color.r, color.g, color.b, 0.85f));

            int rocks = 8;
            for (int i = 0; i < rocks; i++)
            {
                float t = (360f / rocks) * i + 14f;
                float rad = t * Mathf.Deg2Rad;
                float x = ring.x + ring.width * 0.5f + Mathf.Cos(rad) * (ring.width * 0.42f);
                float y = ring.y + ring.height * 0.5f + Mathf.Sin(rad) * (ring.height * 0.42f);
                float size = (i % 3 == 0) ? 4.5f : 3.5f;
                Widgets.DrawBoxSolid(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), color);
            }
            GUI.EndGroup();
        }

        private static Rect CenterRect(float width, float height, float innerW, float innerH)
        {
            return new Rect((width - innerW) * 0.5f, (height - innerH) * 0.5f, innerW, innerH);
        }

        private static void DrawRectOutline(Rect rect, float lineWidth, Color color)
        {
            Widgets.DrawLine(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), color, lineWidth);
            Widgets.DrawLine(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), color, lineWidth);
            Widgets.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), color, lineWidth);
            Widgets.DrawLine(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), color, lineWidth);
        }

        private static void DrawFilledCircle(Rect rect, Color color)
        {
            Vector2 c = rect.center;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f;
            int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float next = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector2 a = c;
                Vector2 b = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                Vector2 d = c + new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * r;
                Widgets.DrawLine(a, b, color, 1.5f);
                Widgets.DrawLine(b, d, color, 1.5f);
                Widgets.DrawLine(d, a, color, 1.5f);
            }
        }

        private static void DrawCircleOutline(Rect rect, float lineWidth, Color color)
        {
            int segments = 28;
            Vector2 c = rect.center;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 prev = c + new Vector2(r, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float ang = (Mathf.PI * 2f / segments) * i;
                Vector2 next = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                Widgets.DrawLine(prev, next, color, lineWidth);
                prev = next;
            }
        }

        private static void DrawEllipseOutline(Rect rect, float lineWidth, Color color)
        {
            int segments = 36;
            Vector2 c = rect.center;
            float rx = rect.width * 0.5f;
            float ry = rect.height * 0.5f;
            Vector2 prev = c + new Vector2(rx, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float ang = (Mathf.PI * 2f / segments) * i;
                Vector2 next = c + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
                Widgets.DrawLine(prev, next, color, lineWidth);
                prev = next;
            }
        }
    }
}
