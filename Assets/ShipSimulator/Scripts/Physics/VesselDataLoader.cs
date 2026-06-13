using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    public sealed class VesselDataLoader : MonoBehaviour
    {
        [SerializeField] private TextAsset vesselJson;
        public VesselData Data { get; private set; }

        public void Configure(TextAsset json)
        {
            vesselJson = json;
            Data = null;
        }

        public VesselData Load()
        {
            if (vesselJson == null)
            {
                Debug.LogError("Vessel JSON is not assigned.", this);
                return null;
            }

            try
            {
                Data = JsonUtility.FromJson<VesselData>(vesselJson.text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Vessel JSON could not be parsed: {exception.Message}", this);
                Data = null;
                return null;
            }

            if (!VesselDataValidator.TryValidate(Data, out string error))
            {
                Debug.LogError($"Vessel data is invalid: {error}", this);
                Data = null;
                return null;
            }

            return Data;
        }
    }
}
