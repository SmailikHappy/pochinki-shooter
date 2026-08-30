using UnityEngine;
using UnityEngine.UI;

public sealed class GameUI : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button readyButton;

    [Header("Ability Buttons")]
    [Tooltip("Каждая пара: кнопка + способность, которую она применяет. " +
             "Новая карточка — новый ScriptableObject-ассет, без правок этого класса.")]
    [SerializeField] private AbilityButtonBinding[] abilityButtons;

    [System.Serializable]
    private struct AbilityButtonBinding
    {
        public Button button;
        public PachinkoAbility ability;
    }

    private void Reset()
    {
        if (startButton == null)
        {
            startButton = GetComponentsInChildren<Button>(true)[0];
        }

        if (readyButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (buttons.Length > 1)
            {
                readyButton = buttons[1];
            }
        }
    }

    private void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);

        if (abilityButtons != null)
        {
            foreach (var binding in abilityButtons)
            {
                if (binding.button == null || binding.ability == null)
                    continue;

                PachinkoAbility ability = binding.ability; // локальная копия для замыкания
                binding.button.onClick.AddListener(() => ApplyAbility(ability));
            }
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);

        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnReadyClicked);
    }

    private void OnStartClicked()
    {
        if (GameHandler.instance == null)
            return;

        GameHandler.instance.StartGame(force: true);
    }

    private void OnReadyClicked()
    {
        Player player = ResolveBoundPlayer();
        if (player == null)
            return;

        if (readyButton != null)
            readyButton.gameObject.SetActive(false);

        if (GameHandler.instance != null)
            GameHandler.instance.MarkPlayerReady(player);
        else
            player.SetReady(true);
    }

    private void ApplyAbility(PachinkoAbility ability)
    {
        PachinkoField field = GetBoundPlayerField();
        ability.Apply(field);
    }

    /// <summary>
    /// Резолвится заново на каждый вызов, а не кэшируется — "текущий локальный
    /// игрок" может меняться в рантайме (debug-переключение клавишами 1-4,
    /// смена LocalUserId), кэш приводил к тому, что кнопки навсегда прилипали
    /// к тому игроку, который был локальным в момент первого клика.
    /// </summary>
    private PachinkoField GetBoundPlayerField()
    {
        Player player = ResolveBoundPlayer();
        if (player == null || GameHandler.instance == null)
            return null;

        GameHandler.instance.SpawnedFields.TryGetValue(player, out PachinkoField field);
        return field;
    }

    private Player ResolveBoundPlayer()
    {
        string localUserId = DiscordHandler.Instance?.LocalUserId;
        if (string.IsNullOrEmpty(localUserId) || GameHandler.instance == null)
            return null;

        var canons = GameHandler.instance.SpawnedCanons;
        if (canons == null)
            return null;

        foreach (Player player in canons.Keys)
        {
            if (player?.user?.UniqueId == localUserId)
                return player;
        }

        return null;
    }

    public void HideLobbyButtons()
    {
        if (startButton != null)
            startButton.gameObject.SetActive(false);

        if (readyButton != null)
            readyButton.gameObject.SetActive(false);
    }
}