using TMPro;
using UnityEngine;

public class PachinkoCounterUI : MonoBehaviour
{
    [SerializeField] private PachinkoCounter counter;
    [SerializeField] private TMP_Text counterText;

    private void Awake()
    {
        if (counterText == null)
        {
            counterText = GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        if (counter != null)
        {
            counter.OnCounterChanged.AddListener(UpdateText);

            // Сразу показать текущее значение
            UpdateText(counter.CurrentValue);
        }
    }

    private void OnDisable()
    {
        if (counter != null)
        {
            counter.OnCounterChanged.RemoveListener(UpdateText);
        }
    }

    private void UpdateText(int value)
    {
        if (counterText != null)
        {
            counterText.text = value.ToString();
        }
    }
}