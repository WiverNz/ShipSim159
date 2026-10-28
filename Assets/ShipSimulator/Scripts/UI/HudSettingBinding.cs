using System;
using UnityEngine;

namespace ShipSimulator.UI
{
    public sealed class HudSettingBinding : MonoBehaviour
    {
        public Action OnDisposed;
        private void OnDestroy() => OnDisposed?.Invoke();
    }
}
