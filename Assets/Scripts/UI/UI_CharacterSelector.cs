using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class UI_CharacterSelector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Character Data")]
    [SerializeField] private CharacterDatabase characterDB;

    [Header("Input Settings")]
    [SerializeField] private float inputThreshold = 0.5f;
    [SerializeField] private float inputCooldown = 0.3f;

    private int currentIndex = 0;
    private PlayerInput playerInput;

    private float lastInputTime = 0f;

    public CharacterData SelectedCharacter => characterDB.characters[currentIndex];

    private void Awake()
    {
        UpdateUI();
    }

    public void Init(PlayerInput player)
    {
        playerInput = player;

        if (playerInput.actions["Navigate"] != null)
            playerInput.actions["Navigate"].performed += OnNavigate;

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (playerInput != null && playerInput.actions["Navigate"] != null)
            playerInput.actions["Navigate"].performed -= OnNavigate;
    }

    private void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (Time.time - lastInputTime < inputCooldown) return;

        float x = ctx.ReadValue<Vector2>().x;

        if (x > inputThreshold)
        {
            NextCharacter();
            lastInputTime = Time.time;
        }
        else if (x < -inputThreshold)
        {
            PreviousCharacter();
            lastInputTime = Time.time;
        }
    }

    private void NextCharacter()
    {
        if (characterDB == null || characterDB.characters.Length == 0) return;

        currentIndex = (currentIndex + 1) % characterDB.characters.Length;
        UpdateUI();
    }

    private void PreviousCharacter()
    {
        if (characterDB == null || characterDB.characters.Length == 0) return;

        currentIndex--;
        if (currentIndex < 0)
            currentIndex = characterDB.characters.Length - 1;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (characterDB == null || characterDB.characters.Length == 0) return;

        var data = characterDB.characters[currentIndex];
        if (portraitImage != null) portraitImage.sprite = data.portrait;
        if (nameText != null) nameText.text = data.characterName;
    }
}
