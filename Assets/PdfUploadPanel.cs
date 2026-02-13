using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class PdfUploadPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;      // overlay quad (initially disabled)
    [SerializeField] private TMP_Text listText;         // the PDF name text on overlay
    [SerializeField] private TMP_Text pageNumberText;   // bottom text: "1/5"
    [SerializeField] private BoardSlideDeck boardDeck;

    [Header("Selection")]
    [SerializeField] private int selectedIndex = 0;

    private List<string> _decks = new();

    private void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);

        if (boardDeck)
        {
            boardDeck.OnPageChanged += RefreshPageNumber;
            boardDeck.OnDeckLoaded += RefreshPageNumber;
        }

        RefreshPageNumber();
    }

    private void OnDestroy()
    {
        if (boardDeck)
        {
            boardDeck.OnPageChanged -= RefreshPageNumber;
            boardDeck.OnDeckLoaded -= RefreshPageNumber;
        }
    }

    // Upload PDF button calls this
    public void ToggleOverlay()
    {
        if (!panelRoot) return;

        bool newState = !panelRoot.activeSelf;
        panelRoot.SetActive(newState);

        if (newState)
            RefreshDeckListAndText();
    }


    public void CloseOverlay()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    public void SelectCurrentPdfAndLoad()

    {
        Debug.Log("PdfUploadPanel: SelectCurrentPdfAndLoad() CLICKED");

        if (!boardDeck)
        {
            Debug.LogWarning("PdfUploadPanel: boardDeck not assigned.");
            return;
        }

        if (_decks.Count == 0)
        {
            Debug.LogWarning($"PdfUploadPanel: No decks found. Put folders in: {boardDeck.GetLibraryRootPath()}");
            return;
        }



        string chosen = _decks[selectedIndex];

        Debug.Log($"PdfUploadPanel: boardDeck = {(boardDeck ? boardDeck.name : "NULL")}");
        Debug.Log($"PdfUploadPanel: chosen = {chosen}");

        bool ok = boardDeck.LoadDeck(chosen);

        if (ok)
        {
            RefreshPageNumber();
            CloseOverlay();
        }
    }

    public void NextPage() => boardDeck?.NextPage();
    public void PrevPage() => boardDeck?.PrevPage();

    private void RefreshDeckListAndText()
    {
        if (!boardDeck) return;

        _decks = boardDeck.GetAvailableDeckNames();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, _decks.Count - 1));

        RefreshDeckNameTextOnly();
    }

    private void RefreshDeckNameTextOnly()
    {
        if (!listText) return;

        if (_decks.Count == 0)
        {
            listText.text =
                "No PDFs found.\n\n" +
                "Add decks here:\n" +
                (boardDeck ? boardDeck.GetLibraryRootPath() : "(assign boardDeck)") +
                "\n\nEach folder = one PDF deck\n(001.png, 002.png...)";
            return;
        }

        listText.text = _decks[selectedIndex];
    }

    private void RefreshPageNumber()
    {
        if (!pageNumberText) return;

        if (!boardDeck || boardDeck.PageCount <= 0)
        {
            pageNumberText.text = "-/-";
            return;
        }

        int current = boardDeck.CurrentPageIndex + 1;
        int total = boardDeck.PageCount;
        pageNumberText.text = $"{current}/{total}";
    }
}
