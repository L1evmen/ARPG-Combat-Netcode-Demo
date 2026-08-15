using TMPro;
using UnityEngine;

namespace GameUI
{
    public class PropertyRowUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _propertyName;
        [SerializeField] private TextMeshProUGUI _propertyValue;
        [SerializeField] private TextMeshProUGUI _diffValue;

        public PropertyType PropertyType { get; private set; }

        public void Set(PropertyType type, int value)
        {
            PropertyType = type;
            if (_propertyName != null)
            {
                _propertyName.SetText(PropertyNameOf(type));
                _propertyName.ForceMeshUpdate();
                Debug.Log($"[Set] name='{_propertyName.text}' enabled={_propertyName.enabled} active={_propertyName.gameObject.activeInHierarchy} color={_propertyName.color} fontSize={_propertyName.fontSize} rt={_propertyName.rectTransform.rect}");
            }
            if (_propertyValue != null)
            {
                _propertyValue.SetText(value.ToString());
                _propertyValue.ForceMeshUpdate();
            }
            if (_diffValue != null) _diffValue.SetText("");
        }

        public void SetDiff(int diff)
        {
            if (_diffValue == null) return;
            if (diff > 0)
            {
                _diffValue.SetText($"(+{diff})");
                _diffValue.color = Color.green;
            }
            else if (diff < 0)
            {
                _diffValue.SetText($"({diff})");
                _diffValue.color = Color.red;
            }
            else
            {
                _diffValue.SetText("");
            }
        }

        private static string PropertyNameOf(PropertyType type) => type switch
        {
            PropertyType.HPValue => "生命值",
            PropertyType.EnergyValue => "能量值",
            PropertyType.MentalValue => "精神值",
            PropertyType.SpeedValue => "速度",
            PropertyType.AttackValue => "攻击力",
            _ => type.ToString()
        };
    }
}
