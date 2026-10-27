using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        private Text label;
        private Func<string> value;
        public static void Bind(Text label, Func<string> value)
        {
            var binding = label.GetComponent<LocalizedLabel>();
            if (binding == null) binding = label.gameObject.AddComponent<LocalizedLabel>();
            binding.label = label;
            binding.value = value;
            binding.Refresh();
        }
        public static void BindSource(Text label, string source)
        {
            if (!string.IsNullOrEmpty(source)) Bind(label, () => Localization.Source(source));
        }
        public static void Clear(Text label)
        {
            if (label == null) return;
            var binding = label.GetComponent<LocalizedLabel>();
            if (binding == null) return;
            binding.value = null;
            binding.enabled = false;
            Destroy(binding);
        }
        private void OnEnable() { Localization.Changed += Refresh; Refresh(); }
        private void OnDisable() => Localization.Changed -= Refresh;
        private void Refresh()
        {
            if (label != null && value != null) label.text = value();
        }
    }
}
