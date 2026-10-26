using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    public sealed class HudLayout : MonoBehaviour
    {
        private readonly List<RectTransform[]> groups = new List<RectTransform[]>();
        private readonly List<Button> tabs = new List<Button>();
        private Canvas canvas;
        private RectTransform toolbar, viewport, content, sidebar;
        private RectTransform instruments, tape, warnings;
        private Text help;
        private ScrollRect scroll;
        private int selected;
        private Vector2 previousSize;
        private bool dirty = true;
        private bool mapEnabled = true;
        public int SelectedGroup => selected;

        public void Build(Text helpText)
        {
            canvas = GetComponent<Canvas>();
            help = helpText;
            groups.Clear(); tabs.Clear();
            groups.Add(Find("TelegraphBlock", "RudderBlock"));
            groups.Add(Find("LoadingBlock", "MachineryBlock", "GroundTackleBlock"));
            groups.Add(Find("CameraBlock", "TimeScaleBlock"));
            groups.Add(Find("WeatherBlock"));
            groups.Add(Find("Objective", "MiniMap"));
            groups.Add(new[] { help.rectTransform });
            instruments = transform.Find("TopStatusBar") as RectTransform;
            tape = transform.Find("HeadingTape") as RectTransform;
            warnings = transform.Find("Warnings") as RectTransform;
            toolbar = UiLayout.Rect(transform, "Control tabs", 16, 140, 800, 44);
            viewport = UiLayout.Rect(transform, "Control drawer", 16, 200, 704, 380);
            content = UiLayout.Rect(viewport, "Controls", 0, 0, 700, 380);
            scroll = UiLayout.Scroll(viewport, content);
            sidebar = UiLayout.Rect(transform, "Navigation sidebar", 0, 0, 444, 620);
            var sideContent = UiLayout.Rect(sidebar, "Navigation", 0, 0, 444, 620);
            UiLayout.Scroll(sidebar, sideContent);
            string[] names = { "Helm", "Deck", "Camera", "Weather", "Map", "Help", "Settings" };
            HudIcon[] icons = { HudIcon.Rudder, HudIcon.Cargo, HudIcon.Camera, HudIcon.Wind,
                HudIcon.Map, HudIcon.Help, HudIcon.Settings };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var rect = UiLayout.Rect(toolbar, names[i], i * 114, 0, 108, 44);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = HudTheme.Rounded(8); image.type = Image.Type.Sliced;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                var icon = UiLayout.Rect(rect, "Icon", 8, 13, 18, 18).gameObject.AddComponent<Image>();
                icon.sprite = HudTheme.Icon(icons[i]); icon.raycastTarget = false;
                var text = UiLayout.Rect(rect, "Caption", 30, 4, 76, 36).gameObject.AddComponent<Text>();
                text.font = help.font; text.fontSize = 16; text.text = names[i];
                text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
                button.onClick.AddListener(() =>
                {
                    if (index == 6)
                    {
                        var menu = FindAnyObjectByType<VoyageMenu>();
                        if (menu != null) { menu.OpenPause(); menu.ShowSettings(); }
                    }
                    else Select(index);
                });
                tabs.Add(button);
            }
            help.alignment = TextAnchor.UpperLeft;
            help.fontSize = 19;
            help.text = help.text.Replace("   ", "\n");
            Select(0);
        }

        private RectTransform[] Find(params string[] names)
        {
            var result = new List<RectTransform>();
            foreach (string name in names)
                if (transform.Find(name) is RectTransform rect) result.Add(rect);
            return result.ToArray();
        }

        public void Select(int group)
        {
            selected = Mathf.Clamp(group, 0, groups.Count - 1);
            dirty = true;
            scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
            Refresh();
        }

        public void Invalidate() => dirty = true;
        public void RefreshLayout() => Refresh();

        public void ToggleMap()
        {
            if (previousSize.x < 1600) Select(selected == 4 ? 0 : 4);
            else { mapEnabled = !mapEnabled; dirty = true; }
        }

        public void ToggleHelp() => Select(selected == 5 ? 0 : 5);
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (canvas == null || viewport == null) return;
            Vector2 size = UiLayout.Apply(canvas);
            if (size == previousSize && !dirty) return;
            dirty = false; previousSize = size;
            bool twoRows = size.x < 1100;
            float headerHeight = twoRows ? 180 : 90;
            UiLayout.Place(instruments, 16, 12, size.x - 32, headerHeight);
            for (int i = 0; i < 4; i++)
            {
                var tile = instruments.Find("Instrument" + i) as RectTransform;
                int columns = twoRows ? 2 : 4;
                float width = (size.x - 40) / columns;
                UiLayout.Place(tile, (i % columns) * width, (i / columns) * 88, width - 4, 86);
                foreach (Transform child in tile)
                    if (child.name.StartsWith("Icon_")) child.gameObject.SetActive(false);
            }
            foreach (Transform child in instruments)
                if (child.name.StartsWith("Divider")) child.gameObject.SetActive(false);
            float y = headerHeight + 20;
            tape.gameObject.SetActive(!twoRows);
            UiLayout.Place(tape, (size.x - 700) * .5f, y, 700, 54);
            UiLayout.Place(warnings, (size.x - 660) * .5f, y + (twoRows ? 0 : 58), 660, 56);
            UiLayout.Place(toolbar, 16, size.y - 56, size.x - 32, 44);
            bool side = size.x >= 1600 && selected != 4 && mapEnabled;
            float height = Mathf.Max(100, Mathf.Min(420, size.y - y - (twoRows ? 136 : 194)));
            UiLayout.Place(viewport, 16, size.y - height - 68, 716, height);
            UiLayout.Place(sidebar, size.x - 460, y + 60, 444, size.y - y - 128);
            sidebar.gameObject.SetActive(side);
            var nav = (RectTransform)sidebar.GetChild(0);
            float total = 8, navY = 8;
            for (int g = 0; g < groups.Count; g++)
            {
                foreach (RectTransform panel in groups[g])
                {
                    bool visible = g == selected || (g == 4 && side);
                    panel.SetParent(g == 4 && side ? nav : content, false);
                    panel.gameObject.SetActive(visible);
                    if (!visible) continue;
                    float width = panel.sizeDelta.x, panelHeight = panel.sizeDelta.y;
                    if (g == 5)
                    {
                        width = 664;
                        panel.sizeDelta = new Vector2(width, 1000);
                        panelHeight = Mathf.Max(300, help.preferredHeight + 24);
                    }
                    if (g == 4 && side)
                    {
                        UiLayout.Place(panel, 8, navY, width, panelHeight);
                        navY += panelHeight + 12;
                    }
                    else
                    {
                        UiLayout.Place(panel, 12, total, width, panelHeight);
                        total += panelHeight + 12;
                    }
                }
            }
            content.sizeDelta = new Vector2(700, total);
            nav.sizeDelta = new Vector2(444, navY);
            if (selected == 5)
                help.rectTransform.sizeDelta = new Vector2(664, Mathf.Max(300, help.preferredHeight + 24));
            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].interactable = i != 1 || groups[1].Length > 0;
                tabs[i].GetComponent<Image>().color = i == selected ? HudTheme.ButtonActive : HudTheme.ButtonIdle;
            }
        }
    }
}
