using UnityEngine;
using UnityEngine.UI;

namespace OperatorPhone.UI.Apps
{
    /// <summary>
    /// The app grid. Text glyphs rather than icon textures — no asset pipeline exists
    /// yet, and legibility beats prettiness until M4 brings real media handling.
    /// </summary>
    internal class HomeApp : PhoneApp
    {
        public override string Title => "Home";
        public override string Glyph => "";

        protected override void Build(Transform root)
        {
            var gridGo = Ui.Invisible("Grid", root);
            var gr = gridGo.GetComponent<RectTransform>();
            gr.anchorMin = new Vector2(0f, 1f);
            gr.anchorMax = new Vector2(1f, 1f);
            gr.pivot = new Vector2(0.5f, 1f);
            gr.anchoredPosition = new Vector2(0f, -24f);
            gr.sizeDelta = new Vector2(0f, 400f);

            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(76f, 88f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(24, 24, 0, 0);
            grid.childAlignment = TextAnchor.UpperLeft;

            foreach (var app in Host.Apps)
            {
                if (app == this) continue;
                AddIcon(gridGo.transform, app);
            }
        }

        private void AddIcon(Transform parent, PhoneApp app)
        {
            var cell = Ui.Invisible("Icon_" + app.Title, parent);

            var tile = Ui.Rect("Tile", cell.transform, Ui.Panel);
            var tr = tile.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 1f);
            tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(64f, 64f);

            var glyph = Ui.Label("Glyph", tile.transform, app.Glyph, 26,
                TextAnchor.MiddleCenter, Ui.Accent);
            Ui.Fill(glyph.GetComponent<RectTransform>());

            var name = Ui.Label("Name", cell.transform, app.Title, 11,
                TextAnchor.UpperCenter, Ui.Dim);
            var nr = name.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 0f);
            nr.anchorMax = new Vector2(1f, 0f);
            nr.pivot = new Vector2(0.5f, 0f);
            nr.sizeDelta = new Vector2(0f, 16f);
            nr.anchoredPosition = new Vector2(0f, 2f);

            var btn = tile.AddComponent<Button>();
            btn.targetGraphic = tile.GetComponent<Image>();
            var captured = app; // avoid closure over loop variable
            btn.onClick.AddListener((UnityEngine.Events.UnityAction)(() => Host.Push(captured)));
        }
    }
}
