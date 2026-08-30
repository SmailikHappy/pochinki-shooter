using System.Collections;
using TMPro;
using UnityEngine;

public class PachinkoCounterUI : MonoBehaviour
{
    [SerializeField] private PachinkoCounter counter;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Color eventColor = new Color(0.6f, 0.2f, 0.9f); // фиолетовый
    [SerializeField] private string eventLabel = "Event";
    [SerializeField, Min(0f)] private float eventDisplaySeconds = 3f;

    private Color _normalColor;
    private Coroutine _eventRoutine;

    private void Awake()
    {
        if (counterText == null)
            counterText = GetComponent<TMP_Text>();

        if (counterText != null)
            _normalColor = counterText.color;
    }

    private void OnEnable()
    {
        if (counter != null)
        {
            counter.OnCounterChanged.AddListener(UpdateText);
            counter.OnEventTriggered.AddListener(HandleEventTriggered);
            UpdateText(counter.CurrentValue);
        }
    }

    private void OnDisable()
    {
        if (counter != null)
        {
            counter.OnCounterChanged.RemoveListener(UpdateText);
            counter.OnEventTriggered.RemoveListener(HandleEventTriggered);
        }

        if (_eventRoutine != null)
        {
            StopCoroutine(_eventRoutine);
            _eventRoutine = null;
        }
    }

    private void HandleEventTriggered()
    {
        if (_eventRoutine != null)
            StopCoroutine(_eventRoutine);

        _eventRoutine = StartCoroutine(ShowEventLabel());
    }

    private IEnumerator ShowEventLabel()
    {
        if (counterText != null)
        {
            counterText.text = eventLabel;
            counterText.color = eventColor;
        }

        yield return new WaitForSeconds(eventDisplaySeconds);

        _eventRoutine = null;
        UpdateText(counter.CurrentValue); // возвращает актуальное число и обычный цвет
    }

    private void UpdateText(int value)
    {
        if (counterText == null)
            return;

        if (_eventRoutine != null)
            return; // не перебивать вспышку "Event", число не теряется — придёт после её окончания

        counterText.text = value.ToString();
        counterText.color = _normalColor;
    }
}