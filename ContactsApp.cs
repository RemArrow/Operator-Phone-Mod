using OperatorPhone.Data;
using UnityEngine;
using UnityEngine.UI;

namespace OperatorPhone.UI.Apps
{
    internal class ContactsApp : PhoneApp
    {
        public override string Title => "Contacts";
        public override string Glyph => "C";

        private RectTransform _content;
        private InputField _numberField;
        private InputField _nameField;
        private Text _status;

        protected override void Build(Transform root)
        {
            // Add row on top: number + nickname + save.
            var addRow = Ui.Rect("AddRow", root, Ui.Panel);
            var ar = addRow.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0f, 1f);
            ar.anchorMax = new Vector2(1f, 1f);
            ar.pivot = new Vector2(0.5f, 1f);
            ar.sizeDelta = new Vector2(0f, 104f);

            _numberField = Ui.TextInput("Number", addRow.transform, "201-4417");
            Place(_numberField.GetComponent<RectTransform>(), -6f);

            _nameField = Ui.TextInput("Name", addRow.transform, "nickname");
            Place(_nameField.GetComponent<RectTransform>(), -42f);

            var save = Ui.TextButton("Save", addRow.transform, "SAVE",
                Ui.AccentDark, Color.white, 12, Save);
            var sr = save.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(1f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(1f, 1f);
            sr.sizeDelta = new Vector2(64f, 66f);
            sr.anchoredPosition = new Vector2(-8f, -6f);

            _status = Ui.Label("Status", addRow.transform, "", 11,
                TextAnchor.MiddleLeft, Ui.Dim);
            var str_ = _status.GetComponent<RectTransform>();
            str_.anchorMin = new Vector2(0f, 0f);
            str_.anchorMax = new Vector2(1f, 0f);
            str_.pivot = new Vector2(0.5f, 0f);
            str_.sizeDelta = new Vector2(-16f, 24f);
            str_.anchoredPosition = new Vector2(0f, 2f);

            _content = Ui.ScrollList("List", root, out var scroll);
            var vr = scroll.GetComponent<RectTransform>();
            Ui.Fill(vr);
            vr.offsetMax = new Vector2(0f, -104f);

            ContactStore.OnChanged += Refresh;
        }

        private void Place(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-96f, 30f);
            rt.anchoredPosition = new Vector2(-40f, y);
        }

        private void Save()
        {
            var number = (_numberField.text ?? "").Trim();
            var name = (_nameField.text ?? "").Trim();

            if (!System.Text.RegularExpressions.Regex.IsMatch(number, @"^[2-9]\d{2}-\d{4}$"))
            {
                _status.text = "format: 201-4417";
                _status.color = Ui.Danger;
                return;
            }

            ContactStore.AddOrUpdate(number, name.Length > 0 ? name : null);
            _numberField.text = "";
            _nameField.text = "";
            _status.text = "saved";
            _status.color = Ui.Accent;
        }

        private void Refresh()
        {
            if (_content == null) return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Object.Destroy(_content.GetChild(i).gameObject);

            var any = false;
            foreach (var c in ContactStore.All)
            {
                any = true;
                var row = Ui.Rect("Contact", _content, Ui.Panel);
                Ui.FixedHeight(row, 52f);

                var label = string.IsNullOrEmpty(c.Name) ? c.Number : $"{c.Name}  ·  {c.Number}";
                var text = Ui.Label("Label", row.transform, label, 13,
                    TextAnchor.MiddleLeft, Ui.Text);
                var tr = text.GetComponent<RectTransform>();
                Ui.Fill(tr);
                tr.offsetMin = new Vector2(12f, 0f);
                tr.offsetMax = new Vector2(-60f, 0f);

                var del = Ui.TextButton("Del", row.transform, "X", Ui.Panel, Ui.Danger, 13,
                    () => ContactStore.Remove(c.Number));
                var dr = del.GetComponent<RectTransform>();
                dr.anchorMin = new Vector2(1f, 0f);
                dr.anchorMax = new Vector2(1f, 1f);
                dr.pivot = new Vector2(1f, 0.5f);
                dr.sizeDelta = new Vector2(44f, 0f);
            }

            if (!any)
            {
                var empty = Ui.Label("Empty", _content, "No contacts saved.", 13,
                    TextAnchor.MiddleCenter, Ui.Dim);
                Ui.FixedHeight(empty.gameObject, 60f);
            }
        }

        public override void OnOpened() => Refresh();
    }
}
