using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MantaFlight
{
    public sealed class MantaBindingLabel : MonoBehaviour
    {
        public MantaInput input;
        public string actionName, title;
        public int index;
        public TextMeshProUGUI label;
        void Update() => label.text = title + " : " + input.actions.FindActionMap("Flight")[actionName].GetBindingDisplayString(index);
    }
}
